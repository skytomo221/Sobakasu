using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class ControlFlowLowerer : LoweringComponent
    {
        public ControlFlowLowerer(LoweringSession session) : base(session) { }
        internal IrValue LowerIfExpression(
            BoundIfExpression expression,
            LoweringContext context)
        {
            var condition = LowerValueExpression(
                expression.Condition,
                context,
                TypeSymbol.Bool);
            if (condition == null)
                return null;

            var thenBlock = context.CreateBlock("if_then");
            var elseBlock = expression.ElseExpression == null
                ? null
                : context.CreateBlock("if_else");
            var mergeBlock = expression.Type == TypeSymbol.Never
                ? null
                : context.CreateBlock("if_merge");
            IrStorage result = null;
            if (expression.Type != TypeSymbol.Unit &&
                expression.Type != TypeSymbol.Never)
            {
                result = context.CreateTemporary(expression.Type);
            }

            context.TerminateWithCondition(
                condition,
                thenBlock.Label,
                elseBlock?.Label ?? mergeBlock.Label);

            context.SwitchTo(thenBlock);
            var thenValue = Session.ExpressionLowerer.LowerBlockExpression(expression.ThenExpression, context);
            CompleteIfBranch(thenValue, result, mergeBlock, context);

            if (elseBlock != null)
            {
                context.SwitchTo(elseBlock);
                var elseValue = LowerValueExpression(
                    expression.ElseExpression,
                    context,
                    expression.Type);
                CompleteIfBranch(elseValue, result, mergeBlock, context);
            }

            if (mergeBlock == null)
                return null;

            context.SwitchTo(mergeBlock);
            return expression.Type == TypeSymbol.Unit
                ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
                : result;
        }

        private void CompleteIfBranch(
            IrValue value,
            IrStorage result,
            IrBasicBlock mergeBlock,
            LoweringContext context)
        {
            if (context.CurrentBlock.Terminator != null)
                return;

            if (result != null && value != null)
                context.EmitCopy(result, value);

            context.TerminateWithJump(mergeBlock.Label);
        }

        internal IrValue LowerMatchExpression(
            BoundMatchExpression expression,
            LoweringContext context)
        {
            var loweredScrutinee = LowerValueExpression(
                expression.Expression,
                context,
                expression.Expression.Type);
            if (loweredScrutinee == null)
                return null;

            var scrutinee = context.CreateTemporary(expression.Expression.Type);
            context.EmitCopy(scrutinee, loweredScrutinee);

            var mergeBlock = expression.Type == TypeSymbol.Never
                ? null
                : context.CreateBlock("match_merge");
            IrStorage result = null;
            if (expression.Type != TypeSymbol.Unit &&
                expression.Type != TypeSymbol.Never)
            {
                result = context.CreateTemporary(expression.Type);
            }

            foreach (var arm in expression.Arms)
            {
                if (!arm.IsReachable || arm.Pattern is BoundInvalidPattern)
                    continue;

                var armBlock = context.CreateBlock("match_arm");
                IrBasicBlock nextTestBlock = null;
                if (arm.Pattern is BoundWildcardPattern)
                {
                    context.TerminateWithJump(armBlock.Label);
                }
                else
                {
                    nextTestBlock = context.CreateBlock("match_test");
                    var condition = LowerMatchPatternCondition(
                        arm.Pattern,
                        scrutinee,
                        expression.Expression.Type,
                        context);
                    if (condition == null)
                        return null;
                    context.TerminateWithCondition(
                        condition,
                        armBlock.Label,
                        nextTestBlock.Label);
                }

                context.SwitchTo(armBlock);
                if (arm.Pattern is BoundEnumVariantPattern enumPattern)
                {
                    EmitMatchPatternBindings(
                        enumPattern,
                        scrutinee,
                        expression.Expression.Type,
                        context);
                }

                IrValue armValue = null;
                if (arm.Expression.Type == TypeSymbol.Unit)
                    LowerExpressionForEffect(arm.Expression, context);
                else
                    armValue = LowerValueExpression(arm.Expression, context, expression.Type);

                CompleteMatchArm(armValue, result, mergeBlock, context);
                if (nextTestBlock == null)
                    break;
                context.SwitchTo(nextTestBlock);
            }

            if (context.CurrentBlock.Terminator == null)
            {
                if (mergeBlock != null)
                    context.TerminateWithJump(mergeBlock.Label);
                else
                    context.TerminateWithJump(context.CurrentBlock.Label);
            }

            if (mergeBlock == null)
                return null;

            context.SwitchTo(mergeBlock);
            return expression.Type == TypeSymbol.Unit
                ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
                : result;
        }

        internal IrValue LowerMatchPatternCondition(
            BoundPattern pattern,
            IrStorage scrutinee,
            TypeSymbol scrutineeType,
            LoweringContext context)
        {
            IrValue left;
            IrConstantValue right;
            BoundBinaryOperator comparison;
            if (pattern is BoundLiteralPattern literalPattern)
            {
                left = scrutinee;
                right = new IrConstantValue(
                    literalPattern.Literal.Value,
                    literalPattern.Literal.Type,
                    literalPattern.Literal.Span);
                comparison = literalPattern.ComparisonOperator;
            }
            else if (pattern is BoundEnumVariantPattern enumPattern)
            {
                left = GetEnumTagValue(scrutinee, scrutineeType);
                right = new IrConstantValue(enumPattern.Variant.Tag, TypeSymbol.I32);
                comparison = enumPattern.TagComparisonOperator;
            }
            else
            {
                return null;
            }

            if (left == null || comparison == null)
            {
                Diagnostics.ReportLoweringError(
                    "Resolved match pattern has no comparison operation.");
                return null;
            }

            var result = context.CreateTemporary(TypeSymbol.Bool);
            context.Emit(new IrExternCallInstruction(
                comparison.ExternSignature,
                new[] { left, right },
                result));
            return result;
        }

        private IrValue GetEnumTagValue(IrValue value, TypeSymbol enumType)
        {
            var leaves = GetAggregateLeaves(value);
            var descriptors = AggregateLayout.GetLeaves(enumType);
            for (var index = 0; index < descriptors.Count && index < leaves.Count; index++)
            {
                if (descriptors[index].IsEnumTag)
                    return leaves[index];
            }
            return null;
        }

        private void EmitMatchPatternBindings(
            BoundEnumVariantPattern pattern,
            IrValue scrutinee,
            TypeSymbol enumType,
            LoweringContext context)
        {
            foreach (var binding in pattern.Bindings)
            {
                var source = Session.AggregateLowerer.ProjectEnumVariantField(
                    scrutinee,
                    enumType,
                    pattern.Variant,
                    binding.Field);
                if (source == null)
                {
                    Diagnostics.ReportLoweringError(
                        $"Resolved match binding '{binding.Variable.Name}' has no payload storage.");
                    continue;
                }
                context.EmitCopy(context.GetLocalStorage(binding.Variable), source);
            }
        }

        private void CompleteMatchArm(
            IrValue value,
            IrStorage result,
            IrBasicBlock mergeBlock,
            LoweringContext context)
        {
            if (context.CurrentBlock.Terminator != null)
                return;

            if (result != null && value != null)
                context.EmitCopy(result, value);

            if (mergeBlock != null)
                context.TerminateWithJump(mergeBlock.Label);
            else
                context.TerminateWithJump(context.CurrentBlock.Label);
        }

        internal IrValue LowerWhileExpression(
          BoundWhileExpression expression,
          LoweringContext context)
        {
            var conditionBlock = context.CreateBlock("while_condition");

            context.TerminateWithJump(conditionBlock.Label);
            context.SwitchTo(conditionBlock);
            var condition = LowerValueExpression(
                expression.Condition,
                context,
                TypeSymbol.Bool);
            if (condition == null)
                return null;

            var bodyBlock = context.CreateBlock("while_body");
            var exitBlock = context.CreateBlock("while_exit");
            context.TerminateWithCondition(
                condition,
                bodyBlock.Label,
                exitBlock.Label);

            context.PushLoop(new LoopLoweringFrame(
                expression.Loop,
                exitBlock.Label,
                conditionBlock.Label,
                bodyBlock.Label,
                null));
            try
            {
                context.SwitchTo(bodyBlock);
                Session.ExpressionLowerer.LowerBlockExpression(expression.Body, context);
                if (context.CurrentBlock.Terminator == null)
                    context.TerminateWithJump(conditionBlock.Label);
            }
            finally
            {
                context.PopLoop(expression.Loop);
            }

            context.SwitchTo(exitBlock);
            return new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>());
        }

        internal IrValue LowerLoopExpression(
            BoundLoopExpression expression,
            LoweringContext context)
        {
            var bodyBlock = context.CreateBlock("loop_body");
            var hasExit = expression.Type != TypeSymbol.Never;
            var exitBlock = hasExit
                ? context.CreateBlock("loop_exit")
                : null;
            IrStorage result = null;
            if (expression.Type != TypeSymbol.Unit &&
                expression.Type != TypeSymbol.Never)
            {
                result = context.CreateTemporary(expression.Type);
            }

            context.TerminateWithJump(bodyBlock.Label);
            context.PushLoop(new LoopLoweringFrame(
                expression.Loop,
                exitBlock?.Label,
                bodyBlock.Label,
                bodyBlock.Label,
                result));
            try
            {
                context.SwitchTo(bodyBlock);
                Session.ExpressionLowerer.LowerBlockExpression(expression.Body, context);
                if (context.CurrentBlock.Terminator == null)
                    context.TerminateWithJump(bodyBlock.Label);
            }
            finally
            {
                context.PopLoop(expression.Loop);
            }

            if (exitBlock == null)
                return null;

            context.SwitchTo(exitBlock);
            return expression.Type == TypeSymbol.Unit
                ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
                : result;
        }

        internal void LowerBreakStatement(
            BoundBreakStatement statement,
            LoweringContext context)
        {
            var loop = context.FindLoop(statement.Target);
            if (loop == null || string.IsNullOrEmpty(loop.BreakTarget))
            {
                Diagnostics.ReportLoweringError(
                    "Resolved break target is not active during lowering.");
                return;
            }

            if (statement.Expression != null)
            {
                if (statement.Expression.Type == TypeSymbol.Unit)
                {
                    LowerExpressionForEffect(statement.Expression, context);
                    if (context.CurrentBlock.Terminator != null)
                        return;

                    context.TerminateWithJump(loop.BreakTarget);
                    return;
                }

                var value = LowerValueExpression(
                    statement.Expression,
                    context,
                    statement.Expression.Type);
                if (value == null || context.CurrentBlock.Terminator != null)
                    return;

                if (loop.ResultStorage == null)
                {
                    Diagnostics.ReportLoweringError(
                        "Value-producing break does not have a loop result slot.");
                    return;
                }

                context.EmitCopy(loop.ResultStorage, value);
            }

            context.TerminateWithJump(loop.BreakTarget);
        }

        internal void LowerContinueStatement(
            BoundContinueStatement statement,
            LoweringContext context)
        {
            var loop = context.FindLoop(statement.Target);
            if (loop == null)
            {
                Diagnostics.ReportLoweringError(
                    "Resolved continue target is not active during lowering.");
                return;
            }

            context.TerminateWithJump(loop.ContinueTarget);
        }

        internal void LowerRedoStatement(
            BoundRedoStatement statement,
            LoweringContext context)
        {
            var loop = context.FindLoop(statement.Target);
            if (loop == null)
            {
                Diagnostics.ReportLoweringError(
                    "Resolved redo target is not active during lowering.");
                return;
            }

            context.TerminateWithJump(loop.RedoTarget);
        }

        internal void LowerReturnStatement(
            BoundReturnStatement statement,
            LoweringContext context)
        {
            if (context.IsInsideInlineFunction)
            {
                LowerInlineFunctionReturnStatement(statement, context);
                return;
            }

            if (statement.Expression == null)
            {
                context.CurrentBlock.SetTerminator(new IrReturnTerminator());
                return;
            }

            if (statement.Expression.Type == TypeSymbol.Unit)
            {
                LowerExpressionForEffect(statement.Expression, context);
                if (context.CurrentBlock.Terminator == null)
                    context.CurrentBlock.SetTerminator(new IrReturnTerminator());
                return;
            }

            var value = LowerValueExpression(
                statement.Expression,
                context,
                context.EntryReturnType);
            if (value == null)
                return;

            if (string.IsNullOrEmpty(context.ReturnValueStorageName))
            {
                Diagnostics.ReportLoweringError(
                    $"Entry point '{context.EntrySourceName}' has a non-void return without a Udon return slot.");
                return;
            }

            context.EmitCopy(
                new IrReturnValueStorage(context.ReturnValueStorageName),
                value);
            context.CurrentBlock.SetTerminator(new IrReturnTerminator());
        }

        private void LowerInlineFunctionReturnStatement(
            BoundReturnStatement statement,
            LoweringContext context)
        {
            if (statement.Expression == null)
            {
                context.MarkInlineEndIncoming();
                context.TerminateWithJump(context.CurrentInlineEndLabel);
                return;
            }

            if (statement.Expression.Type == TypeSymbol.Unit)
            {
                LowerExpressionForEffect(statement.Expression, context);
                if (context.CurrentBlock.Terminator == null)
                {
                    context.MarkInlineEndIncoming();
                    context.TerminateWithJump(context.CurrentInlineEndLabel);
                }
                return;
            }

            var resultStorage = context.CurrentInlineResultStorage;
            if (resultStorage == null)
            {
                Diagnostics.ReportLoweringError(
                    $"Function '{context.CurrentInlineFunction.Name}' returned a value without a result slot.");
                return;
            }

            var value = LowerValueExpression(
                statement.Expression,
                context,
                context.CurrentInlineFunction.ReturnType);
            if (value == null)
                return;

            context.EmitCopy(resultStorage, value);
            context.MarkInlineEndIncoming();
            context.TerminateWithJump(context.CurrentInlineEndLabel);
        }

    }
}
