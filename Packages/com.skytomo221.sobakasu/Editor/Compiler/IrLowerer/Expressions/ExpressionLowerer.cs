using System;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class ExpressionLowerer : LoweringComponent
    {
        public ExpressionLowerer(LoweringSession session) : base(session) { }
        internal IrValue Lower(
            BoundExpression expression,
            LoweringContext context,
            TypeSymbol expectedType = null)
        {
            switch (expression)
            {
                case BoundLiteralExpression literalExpression:
                    return LowerLiteralExpression(literalExpression, expectedType);

                case BoundArrayLiteralExpression arrayLiteralExpression:
                    return Session.ArrayLowerer.LowerArrayLiteralExpression(arrayLiteralExpression, context);

                case BoundArrayRepeatExpression arrayRepeatExpression:
                    return Session.ArrayLowerer.LowerArrayRepeatExpression(arrayRepeatExpression, context);

                case BoundElementAccessExpression elementAccessExpression:
                    return Session.ArrayLowerer.LowerElementAccessExpression(elementAccessExpression, context);

                case BoundElementAssignmentExpression elementAssignmentExpression:
                    return Session.AssignmentLowerer.LowerElementAssignmentExpression(elementAssignmentExpression, context);

                case BoundArrayLengthExpression arrayLengthExpression:
                    return Session.ArrayLowerer.LowerArrayLengthExpression(arrayLengthExpression, context);

                case BoundStructConstructionExpression structConstructionExpression:
                    return Session.AggregateLowerer.LowerStructConstructionExpression(structConstructionExpression, context);

                case BoundTupleExpression tupleExpression:
                    return Session.AggregateLowerer.LowerTupleExpression(tupleExpression, context);

                case BoundEnumConstructionExpression enumConstructionExpression:
                    return Session.AggregateLowerer.LowerEnumConstructionExpression(enumConstructionExpression, context);

                case BoundMaybeExternBindingExpression maybeExternBindingExpression:
                    return Session.AggregateLowerer.LowerMaybeExternBindingExpression(
                        maybeExternBindingExpression,
                        context);

                case BoundAggregateFieldAccessExpression fieldAccessExpression:
                    return Session.AggregateLowerer.LowerAggregateFieldAccessExpression(fieldAccessExpression, context);

                case BoundAggregateFieldAssignmentExpression fieldAssignmentExpression:
                    return Session.AssignmentLowerer.LowerAggregateFieldAssignmentExpression(fieldAssignmentExpression, context);

                case BoundNameExpression nameExpression
                  when nameExpression.Symbol is LocalVariableSymbol local:
                    return context.GetLocalStorage(local);

                case BoundNameExpression nameExpression
                  when nameExpression.Symbol is StateVariableSymbol state:
                    return context.GetVariableStorage(state);

                case BoundNameExpression nameExpression
                  when nameExpression.Symbol is ConstantSymbol constant:
                    return new IrConstantValue(
                        constant.ConstantValue,
                        constant.Type,
                        constant.InitializerSpan);

                case BoundNameExpression nameExpression
                  when nameExpression.Symbol is ParameterSymbol parameter:
                    return context.GetParameterStorage(parameter);

                case BoundUnaryExpression unaryExpression:
                    return Session.OperatorLowerer.LowerUnaryExpression(unaryExpression, context);

                case BoundBinaryExpression binaryExpression:
                    return binaryExpression.Operator.IsShortCircuit
                        ? Session.OperatorLowerer.LowerShortCircuitBinaryExpression(binaryExpression, context)
                        : Session.OperatorLowerer.LowerEagerBinaryExpression(binaryExpression, context);

                case BoundCallExpression callExpression:
                    return Session.CallLowerer.LowerCallExpression(callExpression, context, preserveResult: true);

                case BoundUserFunctionCallExpression functionCallExpression:
                    return Session.CallLowerer.LowerUserFunctionCallExpression(
                        functionCallExpression,
                        context,
                        preserveResult: true);

                case BoundBlockExpression blockExpression:
                    return LowerBlockExpression(blockExpression, context);

                case BoundIfExpression ifExpression:
                    return Session.ControlFlowLowerer.LowerIfExpression(ifExpression, context);

                case BoundMatchExpression matchExpression:
                    return Session.ControlFlowLowerer.LowerMatchExpression(matchExpression, context);

                case BoundWhileExpression whileExpression:
                    return Session.ControlFlowLowerer.LowerWhileExpression(whileExpression, context);

                case BoundLoopExpression loopExpression:
                    return Session.ControlFlowLowerer.LowerLoopExpression(loopExpression, context);

                case BoundAssignmentExpression assignmentExpression:
                    return Session.AssignmentLowerer.Lower(assignmentExpression, context);

                case BoundErrorExpression:
                    Diagnostics.ReportLoweringError(
                        "Cannot lower expression that already contains semantic errors.");
                    return null;
            }

            Diagnostics.ReportLoweringError(
                $"Unsupported bound expression '{expression.GetType().Name}'.");
            return null;
        }

        internal IrValue LowerBlockExpression(
            BoundBlockExpression expression,
            LoweringContext context)
        {
            LowerBlock(expression.Block, context);
            if (context.CurrentBlock.Terminator != null)
                return null;

            if (expression.TrailingExpression == null)
            {
                return expression.Type == TypeSymbol.Unit
                    ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
                    : null;
            }

            if (expression.Type == TypeSymbol.Unit)
            {
                LowerExpressionForEffect(expression.TrailingExpression, context);
                return new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>());
            }

            return Lower(expression.TrailingExpression, context, expression.Type);
        }

        private IrValue LowerLiteralExpression(
            BoundLiteralExpression literalExpression,
            TypeSymbol expectedType)
        {
            return new IrConstantValue(
                literalExpression.Value,
                literalExpression.Type,
                literalExpression.Span);
        }

    }
}
