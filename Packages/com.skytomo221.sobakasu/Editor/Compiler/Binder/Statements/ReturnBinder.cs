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
    internal sealed class ReturnBinder : BinderComponent
    {
        internal ReturnBinder(BindingSession session) : base(session)
        {
        }

        internal BoundReturnStatement BindReturnStatement(ReturnStatementSyntax syntax)
        {
            if (Session.Body.CurrentReturnType == TypeSymbol.Unit)
            {
                if (syntax.Expression != null)
                {
                    var expression = Session.ExpressionBinder.BindExpression(syntax.Expression, TypeSymbol.Unit);
                    if (expression.Type != TypeSymbol.Error && expression.Type != TypeSymbol.Never && expression.Type != TypeSymbol.Unit)
                    {
                        Session.Diagnostics.ReportReturnValueNotAllowed(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), Session.Body.CurrentEventName);
                    }

                    return new BoundReturnStatement(expression);
                }

                return new BoundReturnStatement(null);
            }

            if (syntax.Expression == null)
            {
                Session.Diagnostics.ReportReturnValueRequired(syntax.ReturnKeyword.Span, Session.Body.CurrentEventName, Session.Body.CurrentReturnType.Name);
                return new BoundReturnStatement(BoundErrorExpression.Instance);
            }

            var returnExpression = Session.ExpressionBinder.BindExpression(syntax.Expression, Session.Body.CurrentReturnType);
            Session.Body.SawValueReturn = true;
            if (returnExpression.Type != TypeSymbol.Error && returnExpression.Type != TypeSymbol.Never && !Session.ConversionClassifier.CanAssignToLocal(Session.Body.CurrentReturnType, returnExpression.Type))
            {
                Session.Diagnostics.ReportReturnTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), Session.Body.CurrentReturnType.Name, returnExpression.Type.Name);
            }

            return new BoundReturnStatement(returnExpression);
        }
    }
}
