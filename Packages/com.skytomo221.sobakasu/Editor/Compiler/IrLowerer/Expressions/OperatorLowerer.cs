using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class OperatorLowerer : LoweringComponent
    {
        public OperatorLowerer(LoweringSession session) : base(session) { }
        internal IrValue LowerUnaryExpression(
            BoundUnaryExpression expression,
            LoweringContext context)
        {
            var operand = LowerValueExpression(
                expression.Operand,
                context,
                expression.Operator.OperandType);
            if (operand == null)
                return null;

            var result = context.CreateTemporary(expression.Type);
            context.Emit(new IrExternCallInstruction(
                expression.Operator.ExternSignature,
                new[] { operand },
                result));
            return result;
        }

        internal IrValue LowerEagerBinaryExpression(
            BoundBinaryExpression expression,
            LoweringContext context)
        {
            var left = LowerValueExpression(
                expression.Left,
                context,
                expression.Operator.LeftType);
            if (left == null)
                return null;

            var right = LowerValueExpression(
                expression.Right,
                context,
                expression.Operator.RightType);
            if (right == null)
                return null;

            var result = context.CreateTemporary(expression.Type);
            context.Emit(new IrExternCallInstruction(
                expression.Operator.ExternSignature,
                new[] { left, right },
                result));
            return result;
        }

        internal IrValue LowerShortCircuitBinaryExpression(
            BoundBinaryExpression expression,
            LoweringContext context)
        {
            var left = LowerValueExpression(expression.Left, context, TypeSymbol.Bool);
            if (left == null)
                return null;

            var rhsBlock = context.CreateBlock("logical_rhs");
            var shortCircuitBlock = context.CreateBlock("logical_short");
            var mergeBlock = context.CreateBlock("logical_merge");
            var result = context.CreateTemporary(TypeSymbol.Bool);

            if (expression.Operator.Kind == BoundBinaryOperatorKind.LogicalAnd)
            {
                context.TerminateWithCondition(left, rhsBlock.Label, shortCircuitBlock.Label);
            }
            else
            {
                context.TerminateWithCondition(left, shortCircuitBlock.Label, rhsBlock.Label);
            }

            context.SwitchTo(shortCircuitBlock);
            context.EmitCopy(
                result,
                new IrConstantValue(
                    expression.Operator.Kind == BoundBinaryOperatorKind.LogicalOr,
                    TypeSymbol.Bool,
                    null));
            context.TerminateWithJump(mergeBlock.Label);

            context.SwitchTo(rhsBlock);
            var right = LowerValueExpression(expression.Right, context, TypeSymbol.Bool);
            if (right == null)
                return null;

            context.EmitCopy(result, right);
            context.TerminateWithJump(mergeBlock.Label);

            context.SwitchTo(mergeBlock);
            return result;
        }

    }
}
