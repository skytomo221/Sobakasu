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
    internal sealed class MemberAccessBinder : BinderComponent
    {
        internal MemberAccessBinder(BindingSession session) : base(session)
        {
        }

        internal BoundExpression BindMemberAccessExpression(MemberAccessExpressionSyntax syntax, TypeSymbol expectedType = null)
        {
            return Session.MemberAccessBinder.BindMemberAccessExpression(
                syntax, Session.ExpressionBinder.BindExpression(syntax.Expression), expectedType);
        }

        internal BoundExpression BindMemberAccessExpression(
            MemberAccessExpressionSyntax syntax,
            BoundExpression receiver,
            TypeSymbol expectedType = null)
        {
            var memberName = syntax.MemberName;
            if (receiver.Type == TypeSymbol.Error)
            {
                return new BoundMemberAccessExpression(receiver, memberName, null, TypeSymbol.Error);
            }

            if ((receiver.Type.AggregateKind == UserAggregateKind.Struct || receiver.Type.AggregateKind == UserAggregateKind.Tuple) && receiver.Type.TryGetAggregateField(memberName, out var aggregateField))
            {
                if (receiver.Type.IsExternalBinding && aggregateField.ExternalMemberName != null)
                {
                    return Session.ExternResolver.BindResolvedExternalMember(
                        receiver.Type, receiver, aggregateField.ExternalMemberName,
                        ExternMemberKind.Getter, null, syntax.Name.Span);
                }
                return new BoundAggregateFieldAccessExpression(receiver, aggregateField);
            }

            if (receiver.Type.AggregateKind == UserAggregateKind.Tuple && int.TryParse(memberName, out var tupleIndex))
            {
                Session.Diagnostics.ReportTupleIndexOutOfRange(syntax.Name.Span, receiver.Type.Name, tupleIndex, receiver.Type.TupleElementTypes.Count);
                return BoundErrorExpression.Instance;
            }

            if (Session.NameResolver.GetReferencedSymbol(receiver) is TypeSymbol enumType && enumType.AggregateKind == UserAggregateKind.Enum)
            {
                if (!enumType.TryGetEnumVariant(memberName, out var variant))
                {
                    Session.Diagnostics.ReportUnknownEnumVariant(syntax.Name.Span, enumType.Name, memberName);
                    return BoundErrorExpression.Instance;
                }

                if (variant.VariantKind == EnumVariantKind.Unit)
                {
                    if (enumType.IsExternalBinding && variant.ExternalMemberName != null)
                    {
                        if (Session.NetworkSendBinder.TryBindExternalEnumConstant(enumType, variant.ExternalMemberName, syntax.Name.Span, out var enumConstant))
                            return enumConstant;
                        Session.Diagnostics.ReportUnknownExternalMember(syntax.Name.Span, enumType.RuntimeQualifiedName, variant.ExternalMemberName);
                        return BoundErrorExpression.Instance;
                    }
                    if (enumType.IsGenericDefinition)
                    {
                        var substitutions = new Dictionary<TypeSymbol, TypeSymbol>();
                        Session.GenericInference.SeedInferenceFromExpectedType(enumType, expectedType, substitutions);
                        if (!Session.GenericInference.CompleteTypeArgumentInference(enumType, substitutions, Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), out var constructed) || !constructed.TryGetEnumVariant(memberName, out variant))
                        {
                            return BoundErrorExpression.Instance;
                        }
                    }

                    return new BoundEnumConstructionExpression(variant, Array.Empty<BoundAggregateFieldInitializer>());
                }

                Session.Diagnostics.ReportEnumVariantRequiresPayload(syntax.Name.Span, enumType.Name, variant.Name);
                return BoundErrorExpression.Instance;
            }

            if (receiver.Type.TypeKind == TypeKind.Array && string.Equals(memberName, "length", StringComparison.Ordinal))
            {
                return Session.CallExpressionBinder.BindArrayLengthExpression(receiver, syntax.Name.Span);
            }

            var memberSymbol = Session.MemberResolver.LookupMember(receiver, memberName, syntax.Name.Span, out var memberDiagnosticReported);
            if (memberSymbol == null)
            {
                if (!memberDiagnosticReported)
                {
                    Session.Diagnostics.ReportUndefinedMember(syntax.Name.Span, Session.NameResolver.GetReceiverDisplayName(receiver), memberName);
                }

                return new BoundMemberAccessExpression(receiver, memberName, null, TypeSymbol.Error);
            }

            if (memberSymbol is MethodGroupSymbol methodGroup)
                return Session.NameExpressionBinder.BindImplicitUserMethodCall(syntax, receiver, methodGroup);
            if (memberSymbol is FunctionGroupSymbol functionGroup)
                return Session.CallExpressionBinder.BindImplicitFunctionGroupCall(syntax.Name.Span, functionGroup);
            if (memberSymbol is ConstantSymbol constantSymbol)
            {
                Session.ConstantDependencyAnalyzer.EnsureConstantBound(constantSymbol, syntax.Name.Span);
                return new BoundNameExpression(memberName, constantSymbol, constantSymbol.Type);
            }

            return new BoundMemberAccessExpression(receiver, memberName, memberSymbol, Session.NameResolver.GetExpressionType(memberSymbol));
        }

        internal bool TryBindExternalStructFieldAssignment(
            AssignmentExpressionSyntax syntax,
            MemberAccessExpressionSyntax targetSyntax,
            BoundExpression receiver,
            out BoundExpression result)
        {
            result = null;
            if (receiver.Type == TypeSymbol.Error ||
                receiver.Type.AggregateKind != UserAggregateKind.Struct ||
                !receiver.Type.IsExternalBinding ||
                !receiver.Type.TryGetAggregateField(targetSyntax.MemberName, out var field) ||
                field.ExternalMemberName == null)
                return false;

            if (syntax.OperatorToken.Kind != SyntaxKind.EqualsToken)
            {
                Session.Diagnostics.ReportInvalidCompoundAssignmentTarget(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target));
                result = BoundErrorExpression.Instance;
                return true;
            }

            var value = Session.ExpressionBinder.BindExpression(syntax.Expression, field.Type);
            if (value.Type == TypeSymbol.Error)
            {
                result = BoundErrorExpression.Instance;
                return true;
            }
            if (!Session.ConversionClassifier.CanAssignToLocal(field.Type, value.Type))
                Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), field.Type.Name, value.Type.Name);
            result = Session.ExternResolver.BindResolvedExternalMember(
                receiver.Type, receiver, field.ExternalMemberName,
                ExternMemberKind.Setter, value, targetSyntax.Name.Span);
            return true;
        }
    }
}
