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
    internal sealed class LoopBinder : BinderComponent
    {
        internal LoopBinder(BindingSession session) : base(session)
        {
        }

        internal BoundStatement BindBreakStatement(BreakStatementSyntax syntax)
        {
            var expression = syntax.Expression == null ? null : Session.ExpressionBinder.BindExpression(syntax.Expression);
            var target = Session.LoopBinder.ResolveLoopTarget(syntax.Label, syntax.BreakKeyword, "break");
            if (target == null)
                return new BoundExpressionStatement(BoundErrorExpression.Instance);
            if (target.Symbol.IsWhile && expression != null)
            {
                Session.Diagnostics.ReportBreakValueTargetsWhile(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression));
                return new BoundBreakStatement(target.Symbol, expression);
            }

            if (!target.Symbol.IsWhile)
                Session.LoopBinder.RegisterLoopBreak(target, expression, syntax);
            return new BoundBreakStatement(target.Symbol, expression);
        }

        internal BoundStatement BindContinueStatement(ContinueStatementSyntax syntax)
        {
            var target = Session.LoopBinder.ResolveLoopTarget(syntax.Label, syntax.ContinueKeyword, "continue");
            if (target == null)
                return new BoundExpressionStatement(BoundErrorExpression.Instance);
            return new BoundContinueStatement(target.Symbol);
        }

        internal BoundStatement BindRedoStatement(RedoStatementSyntax syntax)
        {
            var target = Session.LoopBinder.ResolveLoopTarget(syntax.Label, syntax.RedoKeyword, "redo");
            if (target == null)
                return new BoundExpressionStatement(BoundErrorExpression.Instance);
            return new BoundRedoStatement(target.Symbol);
        }

        internal LoopBindingContext ResolveLoopTarget(SyntaxToken label, SyntaxToken keyword, string statementName)
        {
            if (Session.Body.LoopContexts.Count == 0)
            {
                Session.Diagnostics.ReportJumpOutsideLoop(keyword.Span, statementName);
                return null;
            }

            if (label == null)
                return Session.Body.LoopContexts[^1];
            var labelName = Session.LoopBinder.GetLabelName(label);
            for (var index = Session.Body.LoopContexts.Count - 1; index >= 0; index--)
            {
                if (string.Equals(Session.Body.LoopContexts[index].Symbol.Label, labelName, StringComparison.Ordinal))
                {
                    return Session.Body.LoopContexts[index];
                }
            }

            Session.Diagnostics.ReportUnknownLoopLabel(label.Span, labelName);
            return null;
        }

        internal void RegisterLoopBreak(LoopBindingContext target, BoundExpression expression, BreakStatementSyntax syntax)
        {
            if (expression != null && (expression.Type == TypeSymbol.Error || expression.Type == TypeSymbol.Never))
            {
                return;
            }

            target.HasReachableBreak = true;
            var breakKind = expression == null ? LoopBreakKind.Empty : LoopBreakKind.Value;
            if (target.BreakKind == LoopBreakKind.None)
            {
                target.BreakKind = breakKind;
                target.BreakType = expression?.Type;
                return;
            }

            if (target.BreakKind != breakKind)
            {
                Session.Diagnostics.ReportMixedLoopBreakValues(syntax.BreakKeyword.Span, target.Symbol.Label);
                return;
            }

            if (breakKind == LoopBreakKind.Value && target.BreakType != expression.Type)
            {
                Session.Diagnostics.ReportLoopBreakTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), target.BreakType.Name, expression.Type.Name);
            }
        }

        internal BoundExpression BindWhileExpression(WhileExpressionSyntax syntax)
        {
            var condition = Session.ExpressionBinder.BindExpression(syntax.Condition);
            Session.ConditionalBinder.RequireBoolCondition(condition, syntax.Condition, "while");
            var context = Session.LoopBinder.EnterLoop(syntax.Label, isWhile: true, syntax.WhileKeyword.Span);
            BoundBlockExpression body;
            try
            {
                body = Session.BlockBinder.BindBlockExpression(syntax.Body);
            }
            finally
            {
                Session.LoopBinder.ExitLoop(context);
            }

            return new BoundWhileExpression(context.Symbol, condition, body);
        }

        internal BoundExpression BindLoopExpression(LoopExpressionSyntax syntax)
        {
            var context = Session.LoopBinder.EnterLoop(syntax.Label, isWhile: false, syntax.LoopKeyword.Span);
            BoundBlockExpression body;
            try
            {
                body = Session.BlockBinder.BindBlockExpression(syntax.Body);
            }
            finally
            {
                Session.LoopBinder.ExitLoop(context);
            }

            var resultType = !context.HasReachableBreak ? TypeSymbol.Never : context.BreakKind == LoopBreakKind.Value ? context.BreakType ?? TypeSymbol.Error : TypeSymbol.Unit;
            return new BoundLoopExpression(context.Symbol, body, resultType);
        }

        internal LoopBindingContext EnterLoop(LoopLabelSyntax labelSyntax, bool isWhile, TextSpan keywordSpan)
        {
            var label = labelSyntax == null ? null : Session.LoopBinder.GetLabelName(labelSyntax.LabelToken);
            if (!string.IsNullOrEmpty(label))
            {
                foreach (var activeLoop in Session.Body.LoopContexts)
                {
                    if (!string.Equals(activeLoop.Symbol.Label, label, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Session.Diagnostics.ReportDuplicateLoopLabel(labelSyntax.LabelToken.Span, label);
                    break;
                }
            }

            var span = labelSyntax?.LabelToken.Span ?? keywordSpan;
            var context = new LoopBindingContext(new LoopSymbol(label, isWhile, span));
            Session.Body.LoopContexts.Add(context);
            return context;
        }

        internal void ExitLoop(LoopBindingContext context)
        {
            if (Session.Body.LoopContexts.Count == 0 || !ReferenceEquals(Session.Body.LoopContexts[^1], context))
            {
                throw new InvalidOperationException("Loop binding contexts became unbalanced.");
            }

            Session.Body.LoopContexts.RemoveAt(Session.Body.LoopContexts.Count - 1);
        }

        internal string GetLabelName(SyntaxToken token)
        {
            if (token?.Value is string value)
                return value;
            var text = token?.Text ?? string.Empty;
            return text.Length > 0 && text[0] == '\'' ? text.Substring(1) : text;
        }
    }
}
