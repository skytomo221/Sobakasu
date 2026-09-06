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
    internal sealed class AssignmentExpressionBinder : BinderComponent
    {
        internal AssignmentExpressionBinder(BindingSession session) : base(session)
        {
        }

        internal BoundExpression BindAssignmentExpression(AssignmentExpressionSyntax syntax)
        {
            if (syntax.Target is ElementAccessExpressionSyntax elementAccessSyntax)
                return Session.AssignmentExpressionBinder.BindElementAssignmentExpression(syntax, elementAccessSyntax);
            if (syntax.Target is MemberAccessExpressionSyntax memberAccessSyntax)
            {
                var receiver = Session.ExpressionBinder.BindExpression(memberAccessSyntax.Expression);
                if (Session.MemberAccessBinder.TryBindExternalStructFieldAssignment(syntax, memberAccessSyntax, receiver, out var externalAssignment))
                    return externalAssignment;
                var boundTarget = Session.MemberAccessBinder.BindMemberAccessExpression(memberAccessSyntax, receiver);
                if (boundTarget is BoundAggregateFieldAccessExpression aggregateTarget)
                    return Session.AssignmentExpressionBinder.BindAggregateFieldAssignmentExpression(syntax, aggregateTarget);
                Session.ExpressionBinder.BindExpression(syntax.Expression);
                if (boundTarget.Type != TypeSymbol.Error)
                {
                    if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
                    {
                        Session.Diagnostics.ReportInvalidAssignmentTarget(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), Session.OperatorResolver.GetAssignmentTargetDisplayText(syntax.Target));
                    }
                    else
                    {
                        Session.Diagnostics.ReportInvalidCompoundAssignmentTarget(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target));
                    }
                }

                return BoundErrorExpression.Instance;
            }

            var targetSpan = Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target);
            if (syntax.Target is not NameExpressionSyntax nameExpressionSyntax)
            {
                Session.ExpressionBinder.BindExpression(syntax.Expression);
                if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
                {
                    Session.Diagnostics.ReportInvalidAssignmentTarget(targetSpan, Session.OperatorResolver.GetAssignmentTargetDisplayText(syntax.Target));
                }
                else
                {
                    Session.Diagnostics.ReportInvalidCompoundAssignmentTarget(targetSpan);
                }

                return BoundErrorExpression.Instance;
            }

            var name = nameExpressionSyntax.Name;
            VariableSymbol variable = Session.NameResolver.LookupLocal(name);
            if (variable == null && Session.Declarations.StateSymbols.TryGetValue(name, out var stateVariable))
                variable = stateVariable;
            if (variable == null)
            {
                Session.ExpressionBinder.BindExpression(syntax.Expression);
                var resolvedSymbol = Session.NameResolver.ResolveVisibleSymbol(name, targetSpan, out var resolutionHadDiagnostic);
                if (resolutionHadDiagnostic)
                    return BoundErrorExpression.Instance;
                if (resolvedSymbol != null)
                {
                    if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
                    {
                        Session.Diagnostics.ReportInvalidAssignmentTarget(targetSpan, name);
                    }
                    else
                    {
                        Session.Diagnostics.ReportInvalidCompoundAssignmentTarget(targetSpan);
                    }
                }
                else
                {
                    Session.Diagnostics.ReportUndefinedName(targetSpan, name);
                }

                return BoundErrorExpression.Instance;
            }

            if (!variable.IsMutable)
            {
                if (variable is StateVariableSymbol)
                {
                    Session.Diagnostics.ReportCannotAssignToImmutableState(targetSpan, name);
                }
                else
                {
                    Session.Diagnostics.ReportCannotAssignToImmutableLocal(targetSpan, name);
                }
            }

            var expression = Session.ExpressionBinder.BindExpression(syntax.Expression, syntax.OperatorToken.Kind == SyntaxKind.EqualsToken ? variable.Type : null);
            if (expression.Type == TypeSymbol.Error)
                return BoundErrorExpression.Instance;
            if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
            {
                if (!Session.ConversionClassifier.CanAssignToLocal(variable.Type, expression.Type))
                {
                    Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), variable.Type.Name, expression.Type.Name);
                }

                return new BoundAssignmentExpression(variable, expression);
            }

            var binarySyntaxKind = Session.OperatorResolver.GetBinaryOperatorKindForCompoundAssignment(syntax.OperatorToken.Kind);
            if (binarySyntaxKind == null)
            {
                Session.Diagnostics.ReportInvalidCompoundAssignmentTarget(targetSpan);
                return BoundErrorExpression.Instance;
            }

            var left = new BoundNameExpression(name, variable, variable.Type);
            var valueExpression = Session.OperatorExpressionBinder.BindUserDefinedOperatorCall(
                binarySyntaxKind.Value,
                left,
                expression,
                isUnary: false,
                Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
            if (valueExpression == null)
            {
                Session.Diagnostics.ReportUnsupportedBinaryOperator(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), Session.OperatorResolver.GetOperatorText(binarySyntaxKind.Value), variable.Type.Name, expression.Type.Name);
                return BoundErrorExpression.Instance;
            }

            if (!Session.ConversionClassifier.CanAssignToLocal(variable.Type, valueExpression.Type))
            {
                Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), variable.Type.Name, valueExpression.Type.Name);
            }

            return new BoundAssignmentExpression(variable, valueExpression);
        }

        internal BoundExpression BindAggregateFieldAssignmentExpression(AssignmentExpressionSyntax syntax, BoundAggregateFieldAccessExpression target)
        {
            var rootVariable = Session.AssignmentExpressionBinder.GetAggregateAssignmentRootVariable(target);
            var targetsArrayElement = Session.AssignmentExpressionBinder.ContainsAggregateArrayElement(target);
            if (!targetsArrayElement && rootVariable != null && !rootVariable.IsMutable)
            {
                if (rootVariable is StateVariableSymbol)
                {
                    Session.Diagnostics.ReportCannotAssignToImmutableState(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), rootVariable.Name);
                }
                else
                {
                    Session.Diagnostics.ReportCannotAssignToImmutableLocal(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), rootVariable.Name);
                }
            }

            var value = Session.ExpressionBinder.BindExpression(syntax.Expression, syntax.OperatorToken.Kind == SyntaxKind.EqualsToken ? target.Type : null);
            if (value.Type == TypeSymbol.Error)
                return BoundErrorExpression.Instance;
            if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
            {
                if (!Session.ConversionClassifier.CanAssignToLocal(target.Type, value.Type))
                {
                    Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), target.Type.Name, value.Type.Name);
                }

                return new BoundAggregateFieldAssignmentExpression(target, value);
            }

            if (target.Type.UsesFlattenedAggregateStorage || target.Type.TypeKind == TypeKind.Array && target.Type.ElementType?.UsesFlattenedAggregateStorage == true)
            {
                Session.Diagnostics.ReportInvalidCompoundAssignmentTarget(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target));
                return BoundErrorExpression.Instance;
            }

            var binaryKind = Session.OperatorResolver.GetBinaryOperatorKindForCompoundAssignment(syntax.OperatorToken.Kind);
            var operatorCall = binaryKind.HasValue
                ? Session.OperatorExpressionBinder.BindUserDefinedOperatorCall(
                    binaryKind.Value,
                    target,
                    value,
                    isUnary: false,
                    Session.BinderSyntaxFacts.GetExpressionSpan(syntax))
                : null;
            if (operatorCall is not BoundUserFunctionCallExpression userOperator)
            {
                if (operatorCall == null)
                    Session.Diagnostics.ReportUnsupportedBinaryOperator(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), binaryKind.HasValue ? Session.OperatorResolver.GetOperatorText(binaryKind.Value) : syntax.OperatorToken.Text, target.Type.Name, value.Type.Name);
                return BoundErrorExpression.Instance;
            }

            if (!Session.ConversionClassifier.CanAssignToLocal(target.Type, userOperator.Type))
            {
                Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), target.Type.Name, userOperator.Type.Name);
                return BoundErrorExpression.Instance;
            }

            return new BoundAggregateFieldAssignmentExpression(
                target,
                value,
                userOperator.Function);
        }

        internal VariableSymbol GetAggregateAssignmentRootVariable(BoundExpression expression)
        {
            while (expression is BoundAggregateFieldAccessExpression fieldAccess)
                expression = fieldAccess.Receiver;
            return expression is BoundNameExpression name ? name.Symbol as VariableSymbol : null;
        }

        internal bool ContainsAggregateArrayElement(BoundExpression expression)
        {
            while (expression is BoundAggregateFieldAccessExpression fieldAccess)
                expression = fieldAccess.Receiver;
            return expression is BoundElementAccessExpression;
        }

        internal BoundExpression BindElementAccessExpression(ElementAccessExpressionSyntax syntax)
        {
            var array = Session.ExpressionBinder.BindExpression(syntax.Expression);
            if (array.Type == TypeSymbol.Error)
                return BoundErrorExpression.Instance;
            if (array.Type.TypeKind != TypeKind.Array)
            {
                Session.Diagnostics.ReportIndexTargetIsNotArray(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), array.Type.Name);
                return BoundErrorExpression.Instance;
            }

            ArrayIntrinsicSymbols intrinsics = null;
            if (!Session.ExpressionBinder.IsAggregateStorageType(array.Type) && !Session.Environment.ExternCatalog.TryGetArrayIntrinsics(array.Type, out intrinsics, out var reason))
            {
                Session.Diagnostics.ReportArrayTypeNotAvailable(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), array.Type.Name, reason);
                return BoundErrorExpression.Instance;
            }

            var indexType = intrinsics?.IndexType ?? TypeSymbol.I32;
            var index = Session.ExpressionBinder.BindExpression(syntax.Index, indexType);
            if (index.Type != TypeSymbol.Error && index.Type != indexType)
            {
                Session.Diagnostics.ReportInvalidArrayIndexType(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Index), indexType.Name, index.Type.Name);
                return BoundErrorExpression.Instance;
            }

            return new BoundElementAccessExpression(array, index, intrinsics, Session.ExpressionBinder.GetAggregateArrayIntrinsics(array.Type));
        }

        internal BoundExpression BindElementAssignmentExpression(AssignmentExpressionSyntax syntax, ElementAccessExpressionSyntax targetSyntax)
        {
            var target = Session.AssignmentExpressionBinder.BindElementAccessExpression(targetSyntax);
            if (target is not BoundElementAccessExpression elementTarget)
                return BoundErrorExpression.Instance;
            var value = Session.ExpressionBinder.BindExpression(syntax.Expression, syntax.OperatorToken.Kind == SyntaxKind.EqualsToken ? elementTarget.Type : null);
            if (value.Type == TypeSymbol.Error)
                return BoundErrorExpression.Instance;
            if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
            {
                if (!Session.ConversionClassifier.CanAssignToLocal(elementTarget.Type, value.Type))
                {
                    Session.Diagnostics.ReportArrayElementAssignmentTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression), elementTarget.Type.Name, value.Type.Name);
                }

                return new BoundElementAssignmentExpression(elementTarget, value);
            }

            var binaryKind = Session.OperatorResolver.GetBinaryOperatorKindForCompoundAssignment(syntax.OperatorToken.Kind);
            var operatorCall = binaryKind.HasValue
                ? Session.OperatorExpressionBinder.BindUserDefinedOperatorCall(
                    binaryKind.Value,
                    elementTarget,
                    value,
                    isUnary: false,
                    Session.BinderSyntaxFacts.GetExpressionSpan(syntax))
                : null;
            if (operatorCall is not BoundUserFunctionCallExpression userOperator)
            {
                if (operatorCall == null)
                    Session.Diagnostics.ReportUnsupportedArrayElementCompoundAssignment(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), syntax.OperatorToken.Text, elementTarget.Type.Name, value.Type.Name);
                return BoundErrorExpression.Instance;
            }

            if (!Session.ConversionClassifier.CanAssignToLocal(elementTarget.Type, userOperator.Type))
            {
                Session.Diagnostics.ReportArrayElementAssignmentTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), elementTarget.Type.Name, userOperator.Type.Name);
                return BoundErrorExpression.Instance;
            }

            return new BoundElementAssignmentExpression(
                elementTarget,
                value,
                userOperator.Function);
        }
    }
}
