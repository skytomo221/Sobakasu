using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BlockBinder : BinderComponent
    {
        internal BlockBinder(BindingSession session) : base(session)
        {
        }

        internal BoundBlockStatement BindBlockStatement(BlockStatementSyntax syntax)
        {
            var statements = new List<BoundStatement>();
            var parentScope = Session.Body.Scope;
            Session.Body.Scope = new BoundScope(parentScope);
            try
            {
                foreach (var statement in syntax.Statements)
                    statements.Add(Session.StatementBinder.BindStatement(statement));
                if (syntax.TrailingExpression != null)
                    Session.BlockBinder.BindTrailingExpression(syntax.TrailingExpression, statements);
            }
            finally
            {
                Session.Body.Scope = parentScope;
            }

            return new BoundBlockStatement(statements);
        }

        internal BoundBlockExpression BindBlockExpression(BlockStatementSyntax syntax, TypeSymbol expectedType = null)
        {
            var statements = new List<BoundStatement>();
            BoundExpression trailingExpression = null;
            var parentScope = Session.Body.Scope;
            Session.Body.Scope = new BoundScope(parentScope);
            try
            {
                foreach (var statement in syntax.Statements)
                    statements.Add(Session.StatementBinder.BindStatement(statement));
                if (syntax.TrailingExpression != null)
                    trailingExpression = Session.ExpressionBinder.BindExpression(syntax.TrailingExpression, expectedType);
            }
            finally
            {
                Session.Body.Scope = parentScope;
            }

            var block = new BoundBlockStatement(statements);
            var type = trailingExpression?.Type ?? Session.BlockBinder.GetBlockFallthroughType(block);
            return new BoundBlockExpression(block, trailingExpression, type);
        }

        internal TypeSymbol GetBlockFallthroughType(BoundBlockStatement block)
        {
            if (block.Statements.Count == 0)
                return TypeSymbol.Unit;
            var lastStatement = block.Statements[^1];
            if (lastStatement is BoundReturnStatement || lastStatement is BoundBreakStatement || lastStatement is BoundContinueStatement || lastStatement is BoundRedoStatement)
            {
                return TypeSymbol.Never;
            }

            if (lastStatement is BoundExpressionStatement expressionStatement && expressionStatement.Expression.Type == TypeSymbol.Never)
            {
                return TypeSymbol.Never;
            }

            if (lastStatement is BoundBlockStatement nestedBlock)
                return Session.BlockBinder.GetBlockFallthroughType(nestedBlock);
            return TypeSymbol.Unit;
        }

        internal void BindTrailingExpression(ExpressionSyntax syntax, IList<BoundStatement> statements)
        {
            var expression = Session.ExpressionBinder.BindExpression(syntax, Session.Body.CurrentReturnType == TypeSymbol.Unit ? null : Session.Body.CurrentReturnType);
            if (Session.Body.CurrentReturnType == TypeSymbol.Unit)
            {
                if (expression.Type != TypeSymbol.Error && expression.Type != TypeSymbol.Unit && expression.Type != TypeSymbol.Never)
                {
                    Session.Diagnostics.ReportReturnValueNotAllowed(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), Session.Body.CurrentEventName);
                }

                statements.Add(new BoundExpressionStatement(expression));
                return;
            }

            Session.Body.SawValueReturn = true;
            if (expression.Type != TypeSymbol.Error && expression.Type != TypeSymbol.Never && !Session.ConversionClassifier.CanAssignToLocal(Session.Body.CurrentReturnType, expression.Type))
            {
                Session.Diagnostics.ReportReturnTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), Session.Body.CurrentReturnType.Name, expression.Type.Name);
            }

            statements.Add(new BoundReturnStatement(expression));
        }
    }
}
