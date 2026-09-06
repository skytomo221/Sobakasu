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
    internal sealed class LocalDeclarationBinder : BinderComponent
    {
        internal LocalDeclarationBinder(BindingSession session) : base(session)
        {
        }

        internal BoundStatement BindVariableDeclarationStatement(VariableDeclarationStatementSyntax syntax)
        {
            var patternSpan = Session.LocalDeclarationBinder.GetBindingPatternSpan(syntax.Pattern);
            var namePattern = syntax.Pattern as NameBindingPatternSyntax;
            var variableName = namePattern?.Identifier.Text ?? string.Empty;
            var declaredType = syntax.TypeClause != null ? Session.TypeResolver.BindTypeClause(syntax.TypeClause) : null;
            if (syntax.Initializer == null)
            {
                Session.Diagnostics.ReportMissingVariableInitializer(patternSpan, variableName);
                return Session.LocalDeclarationBinder.CreateErrorVariableDeclaration(variableName, patternSpan);
            }

            var initializer = Session.ExpressionBinder.BindExpression(syntax.Initializer, declaredType);
            var variableType = declaredType;
            if (variableType == null)
            {
                variableType = initializer.Type;
            }
            else if (!Session.ConversionClassifier.CanAssignToLocal(variableType, initializer.Type))
            {
                Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Initializer), variableType.Name, initializer.Type.Name);
            }

            if (variableType == null || variableType == TypeSymbol.Error)
                return Session.LocalDeclarationBinder.CreateErrorVariableDeclaration(variableName, patternSpan);
            if (namePattern == null || namePattern.IsDiscard)
            {
                var temporary = new LocalVariableSymbol($"__tuple_pattern_{Session.Body.NextDestructuringTemporaryId++}", variableType, false, patternSpan);
                var statements = new List<BoundStatement>
        {
          new BoundVariableDeclarationStatement(temporary, initializer)
        };
                var root = new BoundNameExpression(temporary.Name, temporary, temporary.Type);
                Session.LocalDeclarationBinder.BindDestructuringPattern(syntax.Pattern, variableType, root, syntax.MutKeyword != null, new HashSet<string>(StringComparer.Ordinal), statements);
                return new BoundBlockStatement(statements);
            }

            var local = new LocalVariableSymbol(variableName, variableType, syntax.MutKeyword != null, namePattern.Identifier.Span);
            Session.Body.Scope?.Declare(local);
            return new BoundVariableDeclarationStatement(local, initializer);
        }

        internal void BindDestructuringPattern(BindingPatternSyntax pattern, TypeSymbol valueType, BoundExpression value, bool isMutable, ISet<string> names, ICollection<BoundStatement> statements)
        {
            if (pattern is NameBindingPatternSyntax name)
            {
                if (name.IsDiscard)
                    return;
                var variableName = name.Identifier.Text ?? string.Empty;
                if (!names.Add(variableName))
                {
                    Session.Diagnostics.ReportDuplicatePatternBinding(name.Identifier.Span, variableName);
                    return;
                }

                var local = new LocalVariableSymbol(variableName, valueType, isMutable, name.Identifier.Span);
                Session.Body.Scope?.Declare(local);
                statements.Add(new BoundVariableDeclarationStatement(local, value));
                return;
            }

            if (pattern is not TupleBindingPatternSyntax tuplePattern)
                return;
            if (valueType.TypeKind != TypeKind.Tuple)
            {
                Session.Diagnostics.ReportTuplePatternRequiresTuple(Session.LocalDeclarationBinder.GetBindingPatternSpan(tuplePattern), valueType.Name);
                return;
            }

            if (tuplePattern.Elements.Count != valueType.TupleElementTypes.Count)
            {
                Session.Diagnostics.ReportTuplePatternArity(Session.LocalDeclarationBinder.GetBindingPatternSpan(tuplePattern), valueType.Name, valueType.TupleElementTypes.Count, tuplePattern.Elements.Count);
            }

            var count = Math.Min(tuplePattern.Elements.Count, valueType.TupleElementTypes.Count);
            for (var index = 0; index < count; index++)
            {
                var field = valueType.AggregateFields[index];
                Session.LocalDeclarationBinder.BindDestructuringPattern(tuplePattern.Elements[index], field.Type, new BoundAggregateFieldAccessExpression(value, field), isMutable, names, statements);
            }
        }

        internal TextSpan GetBindingPatternSpan(BindingPatternSyntax pattern)
        {
            if (pattern is NameBindingPatternSyntax name)
                return name.Identifier.Span;
            if (pattern is TupleBindingPatternSyntax tuple)
            {
                return TextSpan.FromBounds(tuple.OpenParenToken.Span.Start, tuple.CloseParenToken.Span.End);
            }

            return default;
        }

        internal BoundVariableDeclarationStatement CreateErrorVariableDeclaration(string variableName, TextSpan declarationSpan)
        {
            return new BoundVariableDeclarationStatement(new LocalVariableSymbol(variableName, TypeSymbol.Error, false, declarationSpan), BoundErrorExpression.Instance);
        }
    }
}
