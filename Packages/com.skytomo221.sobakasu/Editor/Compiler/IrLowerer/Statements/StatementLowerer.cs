using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class StatementLowerer : LoweringComponent
    {
        public StatementLowerer(LoweringSession session)
            : base(session)
        {
        }

        internal void LowerBlock(BoundBlockStatement block, LoweringContext context)
        {
            foreach (var statement in block.Statements)
            {
                if (context.CurrentBlock.Terminator != null)
                    break;

                LowerStatement(statement, context);
            }
        }

        private void LowerStatement(BoundStatement statement, LoweringContext context)
        {
            if (statement is BoundBlockStatement blockStatement)
            {
                LowerBlock(blockStatement, context);
                return;
            }

            if (statement is BoundVariableDeclarationStatement variableDeclarationStatement)
            {
                var source = Session.ExpressionLowerer.Lower(
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
                Session.NetworkLowerer.LowerSend(sendStatement, context);
                return;
            }

            if (statement is BoundReturnStatement returnStatement)
            {
                Session.ControlFlowLowerer.LowerReturnStatement(returnStatement, context);
                return;
            }

            if (statement is BoundBreakStatement breakStatement)
            {
                Session.ControlFlowLowerer.LowerBreakStatement(breakStatement, context);
                return;
            }

            if (statement is BoundContinueStatement continueStatement)
            {
                Session.ControlFlowLowerer.LowerContinueStatement(continueStatement, context);
                return;
            }

            if (statement is BoundRedoStatement redoStatement)
            {
                Session.ControlFlowLowerer.LowerRedoStatement(redoStatement, context);
                return;
            }

            if (statement is BoundExpressionStatement expressionStatement)
            {
                LowerExpressionStatement(expressionStatement, context);
                return;
            }

            Session.Diagnostics.ReportLoweringError(
                $"Unsupported bound statement '{statement.GetType().Name}'.");
        }

        private void LowerExpressionStatement(
            BoundExpressionStatement statement,
            LoweringContext context)
        {
            LowerExpressionForEffect(statement.Expression, context);
        }

        internal void LowerExpressionForEffect(
            BoundExpression expression,
            LoweringContext context)
        {
            if (expression is BoundErrorExpression)
            {
                Session.Diagnostics.ReportLoweringError(
                    "Cannot lower expression that already contains semantic errors.");
                return;
            }

            if (expression is BoundCallExpression callExpression)
            {
                Session.CallLowerer.LowerCallExpression(callExpression, context, preserveResult: false);
                return;
            }

            if (expression is BoundUserFunctionCallExpression functionCallExpression)
            {
                Session.CallLowerer.LowerUserFunctionCallExpression(functionCallExpression, context, preserveResult: false);
                return;
            }

            Session.ExpressionLowerer.Lower(expression, context, expression.Type);
        }

    }
}
