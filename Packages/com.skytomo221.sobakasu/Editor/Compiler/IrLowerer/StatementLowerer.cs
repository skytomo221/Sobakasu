using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class StatementLowerer
    {
        private readonly IrLoweringEngine _engine;

        public StatementLowerer(IrLoweringEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        internal void LowerBlock(BoundBlockStatement block, EventLoweringContext context)
        {
            foreach (var statement in block.Statements)
            {
                if (context.CurrentBlock.Terminator != null)
                    break;

                LowerStatement(statement, context);
            }
        }

        private void LowerStatement(BoundStatement statement, EventLoweringContext context)
        {
            if (statement is BoundBlockStatement blockStatement)
            {
                LowerBlock(blockStatement, context);
                return;
            }

            if (statement is BoundVariableDeclarationStatement variableDeclarationStatement)
            {
                var source = _engine.LowerValueExpression(
                    variableDeclarationStatement.Initializer,
                    context,
                    variableDeclarationStatement.Variable.Type);
                if (source == null)
                    return;

                context.EmitCopy(
                    context.GetLocalStorage(variableDeclarationStatement.Variable),
                    source);
                return;
            }

            if (statement is BoundNetworkSendStatement sendStatement)
            {
                LowerNetworkSendStatement(sendStatement, context);
                return;
            }

            if (statement is BoundReturnStatement returnStatement)
            {
                _engine.LowerReturnStatement(returnStatement, context);
                return;
            }

            if (statement is BoundBreakStatement breakStatement)
            {
                _engine.LowerBreakStatement(breakStatement, context);
                return;
            }

            if (statement is BoundContinueStatement continueStatement)
            {
                _engine.LowerContinueStatement(continueStatement, context);
                return;
            }

            if (statement is BoundRedoStatement redoStatement)
            {
                _engine.LowerRedoStatement(redoStatement, context);
                return;
            }

            if (statement is BoundExpressionStatement expressionStatement)
            {
                LowerExpressionStatement(expressionStatement, context);
                return;
            }

            _engine.Diagnostics.ReportLoweringError(
                $"Unsupported bound statement '{statement.GetType().Name}'.");
        }

        private void LowerNetworkSendStatement(
            BoundNetworkSendStatement statement,
            EventLoweringContext context)
        {
            var physicalArguments = new List<IrValue>();
            for (var index = 0; index < statement.Arguments.Count; index++)
            {
                var expectedType = index < statement.Receiver.Parameters.Count
                    ? statement.Receiver.Parameters[index].Type
                    : statement.Arguments[index].Type;
                var value = _engine.LowerValueExpression(
                    statement.Arguments[index],
                    context,
                    expectedType);
                if (value == null)
                    return;

                if (expectedType.UsesFlattenedAggregateStorage &&
                    (expectedType.AggregateKind == UserAggregateKind.Struct ||
                     expectedType.AggregateKind == UserAggregateKind.Tuple))
                    physicalArguments.AddRange(IrLoweringEngine.GetAggregateLeaves(value));
                else
                    physicalArguments.Add(value);
            }

            var target = _engine.LowerValueExpression(
                statement.Target,
                context,
                statement.Target.Type);
            if (target == null)
                return;

            var arguments = new List<IrValue>(physicalArguments.Count + 3)
      {
        new IrThisValue(statement.CurrentBehaviourType),
        target,
        new IrConstantValue(
            statement.Receiver.ExportName,
            TypeSymbol.String,
            statement.Receiver.SourceSpan)
      };
            arguments.AddRange(physicalArguments);
            context.Emit(new IrExternCallInstruction(
                statement.ExternSignature,
                arguments,
                null));
        }

        private void LowerExpressionStatement(
            BoundExpressionStatement statement,
            EventLoweringContext context)
        {
            LowerExpressionForEffect(statement.Expression, context);
        }

        internal void LowerExpressionForEffect(
            BoundExpression expression,
            EventLoweringContext context)
        {
            if (expression is BoundErrorExpression)
            {
                _engine.Diagnostics.ReportLoweringError(
                    "Cannot lower expression that already contains semantic errors.");
                return;
            }

            if (expression is BoundCallExpression callExpression)
            {
                _engine.LowerCallExpression(callExpression, context, preserveResult: false);
                return;
            }

            if (expression is BoundUserFunctionCallExpression functionCallExpression)
            {
                _engine.LowerUserFunctionCallExpression(functionCallExpression, context, preserveResult: false);
                return;
            }

            _engine.LowerValueExpression(expression, context, expression.Type);
        }

    }
}
