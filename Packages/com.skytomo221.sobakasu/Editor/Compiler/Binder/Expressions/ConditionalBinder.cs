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
    internal sealed class ConditionalBinder : BinderComponent
    {
        internal ConditionalBinder(BindingSession session) : base(session)
        {
        }

        internal BoundExpression BindIfExpression(IfExpressionSyntax syntax, TypeSymbol expectedType = null)
        {
            var condition = Session.ExpressionBinder.BindExpression(syntax.Condition);
            Session.ConditionalBinder.RequireBoolCondition(condition, syntax.Condition, "if");
            var thenExpression = Session.BlockBinder.BindBlockExpression(syntax.ThenBlock, expectedType);
            BoundExpression elseExpression = null;
            if (syntax.ElseExpression != null)
                elseExpression = Session.ExpressionBinder.BindExpression(syntax.ElseExpression, expectedType);
            TypeSymbol resultType;
            if (elseExpression == null)
            {
                resultType = TypeSymbol.Unit;
                if (thenExpression.Type != TypeSymbol.Error && thenExpression.Type != TypeSymbol.Unit && thenExpression.Type != TypeSymbol.Never)
                {
                    Session.Diagnostics.ReportIfValueRequiresElse(Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
                }
            }
            else
            {
                resultType = Session.ConditionalBinder.UnifyIfBranchTypes(thenExpression.Type, elseExpression.Type, Session.BinderSyntaxFacts.GetExpressionSpan(syntax.ElseExpression));
            }

            return new BoundIfExpression(condition, thenExpression, elseExpression, resultType);
        }

        internal BoundExpression BindMatchExpression(MatchExpressionSyntax syntax, TypeSymbol expectedType)
        {
            var expression = Session.ExpressionBinder.BindExpression(syntax.Expression);
            var arms = new List<BoundMatchArm>();
            var coveredEnumTags = new HashSet<int>();
            var coveredLiterals = new HashSet<object>();
            var coveredTrue = false;
            var coveredFalse = false;
            var coveredAll = expression.Type == TypeSymbol.Never;
            var sawReachableNever = false;
            TypeSymbol resultType = null;
            foreach (var armSyntax in syntax.Arms)
            {
                var parentScope = Session.Body.Scope;
                Session.Body.Scope = new BoundScope(parentScope);
                BoundPattern pattern;
                BoundExpression armExpression;
                bool isReachable;
                try
                {
                    pattern = Session.ConditionalBinder.BindPattern(armSyntax.Pattern, expression.Type);
                    isReachable = Session.ConditionalBinder.AnalyzeMatchPatternCoverage(pattern, expression.Type, coveredEnumTags, coveredLiterals, ref coveredTrue, ref coveredFalse, ref coveredAll);
                    if (!isReachable)
                        Session.Diagnostics.ReportUnreachableMatchArm(Session.BinderSyntaxFacts.GetPatternSpan(armSyntax.Pattern));
                    armExpression = Session.ExpressionBinder.BindExpression(armSyntax.Expression, expectedType);
                }
                finally
                {
                    Session.Body.Scope = parentScope;
                }

                arms.Add(new BoundMatchArm(pattern, armExpression, isReachable));
                if (!isReachable || pattern is BoundInvalidPattern)
                    continue;
                if (armExpression.Type == TypeSymbol.Never)
                {
                    sawReachableNever = true;
                    continue;
                }

                if (armExpression.Type == TypeSymbol.Error)
                {
                    resultType = TypeSymbol.Error;
                    continue;
                }

                if (resultType == null)
                {
                    resultType = armExpression.Type;
                    continue;
                }

                if (resultType != TypeSymbol.Error && resultType != armExpression.Type)
                {
                    Session.Diagnostics.ReportMatchArmTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(armSyntax.Expression), resultType.Name, armExpression.Type.Name);
                    resultType = TypeSymbol.Error;
                }
            }

            if (expression.Type != TypeSymbol.Error && expression.Type != TypeSymbol.Never && !coveredAll)
            {
                Session.ConditionalBinder.ReportNonExhaustiveMatch(syntax, expression.Type, coveredEnumTags, coveredTrue, coveredFalse);
            }

            if (resultType == null)
                resultType = sawReachableNever || expression.Type == TypeSymbol.Never ? TypeSymbol.Never : TypeSymbol.Error;
            return new BoundMatchExpression(expression, arms, resultType);
        }

        internal BoundPattern BindPattern(PatternSyntax syntax, TypeSymbol scrutineeType)
        {
            if (syntax is WildcardPatternSyntax wildcard)
                return new BoundWildcardPattern(wildcard.UnderscoreToken.Span);
            if (syntax is LiteralPatternSyntax literal)
                return Session.ConditionalBinder.BindLiteralPattern(literal, scrutineeType);
            if (syntax is EnumVariantPatternSyntax enumVariant)
                return Session.ConditionalBinder.BindEnumVariantPattern(enumVariant, scrutineeType);
            return new BoundInvalidPattern(Session.BinderSyntaxFacts.GetPatternSpan(syntax));
        }

        internal BoundPattern BindLiteralPattern(LiteralPatternSyntax syntax, TypeSymbol scrutineeType)
        {
            BoundExpression expression = syntax.LiteralToken.Kind switch
            {
                SyntaxKind.String => Session.OperatorExpressionBinder.BindStringLiteralExpression(new StringLiteralExpressionSyntax(syntax.LiteralToken)),
                SyntaxKind.Int8Literal or SyntaxKind.UInt8Literal or SyntaxKind.Int16Literal or SyntaxKind.UInt16Literal or SyntaxKind.Int32Literal or SyntaxKind.UInt32Literal or SyntaxKind.Int64Literal or SyntaxKind.UInt64Literal => Session.OperatorExpressionBinder.BindIntegerLiteralExpression(new IntegerLiteralExpressionSyntax(syntax.LiteralToken)),
                SyntaxKind.CharacterLiteral => Session.OperatorExpressionBinder.BindCharacterLiteralExpression(new CharacterLiteralExpressionSyntax(syntax.LiteralToken)),
                SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword => Session.OperatorExpressionBinder.BindBooleanLiteralExpression(new BooleanLiteralExpressionSyntax(syntax.LiteralToken)),
                _ => BoundErrorExpression.Instance
            };
            if (expression is not BoundLiteralExpression literal)
                return new BoundInvalidPattern(syntax.LiteralToken.Span);
            if (scrutineeType != TypeSymbol.Error && literal.Type != scrutineeType)
            {
                Session.Diagnostics.ReportLiteralPatternTypeMismatch(syntax.LiteralToken.Span, scrutineeType.Name, literal.Type.Name);
                return new BoundInvalidPattern(syntax.LiteralToken.Span);
            }

            var comparison = scrutineeType == TypeSymbol.Error ? null : Session.OperatorResolver.BindAbiBinaryOperator(SyntaxKind.EqualsEqualsToken, scrutineeType, literal.Type, syntax.LiteralToken.Span);
            if (comparison == null && scrutineeType != TypeSymbol.Error)
                return new BoundInvalidPattern(syntax.LiteralToken.Span);
            return new BoundLiteralPattern(literal, comparison, syntax.LiteralToken.Span);
        }

        internal BoundPattern BindEnumVariantPattern(EnumVariantPatternSyntax syntax, TypeSymbol scrutineeType)
        {
            var span = Session.BinderSyntaxFacts.GetPatternSpan(syntax);
            if (scrutineeType.AggregateKind != UserAggregateKind.Enum || scrutineeType.IsExternalBinding)
            {
                if (scrutineeType != TypeSymbol.Error)
                {
                    Session.Diagnostics.ReportEnumPatternRequiresMatchingEnum(span, scrutineeType.Name);
                }

                return new BoundInvalidPattern(span);
            }

            var patternType = Session.ConditionalBinder.BindPatternEnumType(syntax.EnumType);
            if (patternType == TypeSymbol.Error)
                return new BoundInvalidPattern(span);
            var expectedDefinition = scrutineeType.GenericDefinition ?? scrutineeType;
            var patternDefinition = patternType.GenericDefinition ?? patternType;
            var patternName = $"{syntax.EnumType.GetText()}.{syntax.VariantIdentifier.Text}";
            if (patternType.AggregateKind != UserAggregateKind.Enum || !ReferenceEquals(expectedDefinition, patternDefinition))
            {
                Session.Diagnostics.ReportEnumVariantBelongsToDifferentEnum(span, patternName, scrutineeType.Name);
                return new BoundInvalidPattern(span);
            }

            var variantName = syntax.VariantIdentifier.Text ?? string.Empty;
            if (!scrutineeType.TryGetEnumVariant(variantName, out var variant))
            {
                Session.Diagnostics.ReportUnknownEnumVariant(syntax.VariantIdentifier.Span, scrutineeType.Name, variantName);
                return new BoundInvalidPattern(span);
            }

            var bindings = new List<BoundPatternBinding>();
            var bindingNames = new HashSet<string>(StringComparer.Ordinal);
            var valid = true;
            if (syntax is EnumUnitVariantPatternSyntax)
            {
                if (variant.VariantKind == EnumVariantKind.Tuple)
                {
                    Session.Diagnostics.ReportMatchTuplePatternArity(span, scrutineeType.Name, variant.Name, variant.Fields.Count, 0);
                    valid = false;
                }
                else if (variant.VariantKind == EnumVariantKind.Struct)
                {
                    Session.Diagnostics.ReportEnumPatternFormMismatch(span, patternName, "struct");
                    valid = false;
                }
            }
            else if (syntax is EnumTupleVariantPatternSyntax tuplePattern)
            {
                if (variant.VariantKind != EnumVariantKind.Tuple)
                {
                    Session.Diagnostics.ReportEnumPatternFormMismatch(span, patternName, variant.VariantKind == EnumVariantKind.Struct ? "struct" : "unit");
                    valid = false;
                }
                else
                {
                    if (tuplePattern.Bindings.Count != variant.Fields.Count)
                    {
                        Session.Diagnostics.ReportMatchTuplePatternArity(span, scrutineeType.Name, variant.Name, variant.Fields.Count, tuplePattern.Bindings.Count);
                        valid = false;
                    }

                    var count = Math.Min(tuplePattern.Bindings.Count, variant.Fields.Count);
                    for (var index = 0; index < count; index++)
                    {
                        Session.ConditionalBinder.AddPatternBinding(tuplePattern.Bindings[index], variant.Fields[index], bindingNames, bindings);
                    }
                }
            }
            else if (syntax is EnumStructVariantPatternSyntax structPattern)
            {
                if (variant.VariantKind != EnumVariantKind.Struct)
                {
                    Session.Diagnostics.ReportEnumPatternFormMismatch(span, patternName, variant.VariantKind == EnumVariantKind.Tuple ? "tuple" : "unit");
                    valid = false;
                }
                else
                {
                    var seenFields = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var fieldSyntax in structPattern.Fields)
                    {
                        if (!fieldSyntax.IsSupported)
                        {
                            valid = false;
                            continue;
                        }

                        var fieldName = fieldSyntax.Identifier.Text ?? string.Empty;
                        if (!variant.TryGetField(fieldName, out var field))
                        {
                            Session.Diagnostics.ReportUnknownStructVariantPatternField(fieldSyntax.Identifier.Span, patternName, fieldName);
                            valid = false;
                            continue;
                        }

                        if (!seenFields.Add(fieldName))
                        {
                            Session.Diagnostics.ReportDuplicateStructVariantPatternField(fieldSyntax.Identifier.Span, patternName, fieldName);
                            valid = false;
                            continue;
                        }

                        Session.ConditionalBinder.AddPatternBinding(fieldSyntax, field, bindingNames, bindings);
                    }

                    foreach (var field in variant.Fields)
                    {
                        if (seenFields.Contains(field.Name))
                            continue;
                        Session.Diagnostics.ReportMissingStructVariantPatternField(span, patternName, field.Name);
                        valid = false;
                    }
                }
            }

            if (!valid)
                return new BoundInvalidPattern(span);
            var comparison = Session.OperatorResolver.BindAbiBinaryOperator(SyntaxKind.EqualsEqualsToken, TypeSymbol.I32, TypeSymbol.I32, span);
            if (comparison == null)
                return new BoundInvalidPattern(span);
            return new BoundEnumVariantPattern(variant, bindings, comparison, span);
        }

        internal TypeSymbol BindPatternEnumType(TypeSyntax syntax)
        {
            if (syntax.Parts.Count > 1 && Session.TypeResolver.TryResolveModuleType(syntax, out var moduleType))
            {
                return moduleType;
            }

            if (Session.TypeResolver.TryResolveTypeNameQuiet(syntax.GetNameText(), syntax.GetSpan(), out var type))
            {
                return type;
            }

            Session.Diagnostics.ReportUnknownType(syntax.GetSpan(), syntax.GetNameText());
            return TypeSymbol.Error;
        }

        internal void AddPatternBinding(PatternBindingSyntax syntax, AggregateFieldSymbol field, ISet<string> bindingNames, ICollection<BoundPatternBinding> bindings)
        {
            if (!syntax.IsSupported || syntax.IsWildcard)
                return;
            var name = syntax.Identifier.Text ?? string.Empty;
            if (!bindingNames.Add(name))
            {
                Session.Diagnostics.ReportDuplicatePatternBinding(syntax.Identifier.Span, name);
                return;
            }

            var variable = new LocalVariableSymbol(name, field.Type, isMutable: false, syntax.Identifier.Span);
            Session.Body.Scope?.Declare(variable);
            bindings.Add(new BoundPatternBinding(field, variable));
        }

        internal bool AnalyzeMatchPatternCoverage(BoundPattern pattern, TypeSymbol scrutineeType, ISet<int> coveredEnumTags, ISet<object> coveredLiterals, ref bool coveredTrue, ref bool coveredFalse, ref bool coveredAll)
        {
            if (coveredAll)
                return false;
            if (pattern is BoundInvalidPattern)
                return true;
            if (pattern is BoundWildcardPattern)
            {
                coveredAll = true;
                return true;
            }

            if (pattern is BoundEnumVariantPattern enumPattern)
            {
                if (!coveredEnumTags.Add(enumPattern.Variant.Tag))
                    return false;
                if (scrutineeType.AggregateKind == UserAggregateKind.Enum && coveredEnumTags.Count == scrutineeType.EnumVariants.Count)
                {
                    coveredAll = true;
                }

                return true;
            }

            if (pattern is BoundLiteralPattern literalPattern)
            {
                if (!coveredLiterals.Add(literalPattern.Literal.Value))
                    return false;
                if (scrutineeType == TypeSymbol.Bool && literalPattern.Literal.Value is bool value)
                {
                    if (value)
                        coveredTrue = true;
                    else
                        coveredFalse = true;
                    if (coveredTrue && coveredFalse)
                        coveredAll = true;
                }
            }

            return true;
        }

        internal void ReportNonExhaustiveMatch(MatchExpressionSyntax syntax, TypeSymbol scrutineeType, ISet<int> coveredEnumTags, bool coveredTrue, bool coveredFalse)
        {
            var missing = new List<string>();
            if (scrutineeType.AggregateKind == UserAggregateKind.Enum)
            {
                foreach (var variant in scrutineeType.EnumVariants)
                {
                    if (!coveredEnumTags.Contains(variant.Tag))
                        missing.Add($"`{scrutineeType.Name}.{variant.Name}`");
                }
            }
            else if (scrutineeType == TypeSymbol.Bool)
            {
                if (!coveredTrue)
                    missing.Add("`true`");
                if (!coveredFalse)
                    missing.Add("`false`");
            }
            else
            {
                missing.Add("`_`");
            }

            Session.Diagnostics.ReportNonExhaustiveMatch(syntax.CloseBraceToken.Span, string.Join(", ", missing));
        }

        internal void RequireBoolCondition(BoundExpression condition, ExpressionSyntax syntax, string constructName)
        {
            if (condition.Type == TypeSymbol.Error || condition.Type == TypeSymbol.Bool || condition.Type == TypeSymbol.Never)
            {
                return;
            }

            Session.Diagnostics.ReportConditionRequiresBool(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), constructName, condition.Type.Name);
        }

        internal TypeSymbol UnifyIfBranchTypes(TypeSymbol thenType, TypeSymbol elseType, TextSpan elseSpan)
        {
            if (thenType == TypeSymbol.Error || elseType == TypeSymbol.Error)
                return TypeSymbol.Error;
            if (thenType == TypeSymbol.Never)
                return elseType;
            if (elseType == TypeSymbol.Never)
                return thenType;
            if (thenType == elseType)
                return thenType;
            Session.Diagnostics.ReportIfBranchTypeMismatch(elseSpan, thenType.Name, elseType.Name);
            return TypeSymbol.Error;
        }
    }
}
