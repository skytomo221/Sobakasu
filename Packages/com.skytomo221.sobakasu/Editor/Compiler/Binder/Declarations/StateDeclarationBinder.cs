using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class StateDeclarationBinder : BinderComponent
    {
        internal StateDeclarationBinder(BindingSession session) : base(session)
        {
        }

        internal IReadOnlyList<StateDeclarationSyntax> CollectStateDeclarations(IReadOnlyList<StateDeclarationSyntax> members)
        {
            var uniqueDeclarations = new List<StateDeclarationSyntax>();
            foreach (var member in members)
            {
                var stateDeclaration = member;
                var stateName = stateDeclaration.Identifier.Text ?? string.Empty;
                if (Session.Declarations.StateSymbols.ContainsKey(stateName))
                {
                    Session.Diagnostics.ReportDuplicateState(stateDeclaration.Identifier.Span, stateName);
                    continue;
                }

                if (Session.Callables.BehaviorFunctionGroups.TryGetValue(stateName, out var behaviorFunctions))
                {
                    foreach (var function in behaviorFunctions.Functions)
                    {
                        if (function.RequiresStateCapability && function.Parameters.Count == 0)
                        {
                            Session.Diagnostics.ReportStateMemberMethodNameConflict(stateDeclaration.Identifier.Span, stateName);
                            break;
                        }
                    }
                }

                var ordinal = uniqueDeclarations.Count;
                var symbol = new StateVariableSymbol(stateName, TypeSymbol.Error, false, null, null, stateDeclaration.Identifier.Span, stateDeclaration.Identifier.Span, ordinal);
                symbol.Documentation = DocumentationComment.FromSyntax(stateDeclaration.Documentation);
                Session.Declarations.StateSymbols.Add(stateName, symbol);
                uniqueDeclarations.Add(stateDeclaration);
            }

            return uniqueDeclarations;
        }

        internal IReadOnlyList<BoundStateDeclaration> BindStateDeclarations(IReadOnlyList<StateDeclarationSyntax> uniqueDeclarations)
        {
            var states = new List<BoundStateDeclaration>(uniqueDeclarations.Count);
            for (var ordinal = 0; ordinal < uniqueDeclarations.Count; ordinal++)
            {
                var boundState = Session.StateDeclarationBinder.BindStateDeclaration(uniqueDeclarations[ordinal], ordinal);
                Session.Declarations.StateSymbols[boundState.StateSymbol.Name] = boundState.StateSymbol;
                states.Add(boundState);
            }

            return states;
        }

        internal BoundStateDeclaration BindStateDeclaration(StateDeclarationSyntax syntax, int ordinal)
        {
            var stateName = syntax.Identifier.Text ?? string.Empty;
            var declaredType = syntax.TypeClause != null ? Session.TypeResolver.BindTypeClause(syntax.TypeClause) : null;
            var synchronizationMode = Session.StateDeclarationBinder.BindSynchronizationMode(syntax.SynchronizationModifier);
            if (syntax.FieldKeyword != null)
            {
                if (syntax.PubKeyword == null)
                    Session.Diagnostics.ReportFieldRequiresPublic(syntax.FieldKeyword.Span);
                if (declaredType == null)
                    Session.Diagnostics.ReportFieldRequiresExplicitType(syntax.FieldKeyword.Span);
                var publicStateType = declaredType ?? TypeSymbol.Error;
                Session.StateDeclarationBinder.ValidateStateMetadata(
                    syntax,
                    stateName,
                    publicStateType,
                    synchronizationMode);
                var publicStateSymbol = new StateVariableSymbol(
                    stateName,
                    publicStateType,
                    syntax.PubKeyword != null,
                    synchronizationMode,
                    null,
                    syntax.Identifier.Span,
                    syntax.Identifier.Span,
                    ordinal);
                publicStateSymbol.Documentation = DocumentationComment.FromSyntax(syntax.Documentation);
                return new BoundStateDeclaration(publicStateSymbol, null);
            }

            if (syntax.Initializer == null)
            {
                Session.Diagnostics.ReportMissingStateInitializer(syntax.Identifier.Span, stateName);
                return Session.StateDeclarationBinder.CreateErrorStateDeclaration(syntax, ordinal, synchronizationMode);
            }

            var initializer = Session.ExpressionBinder.BindExpression(syntax.Initializer, declaredType);
            var stateType = declaredType;
            if (stateType == null)
            {
                if (initializer.Type == TypeSymbol.Error)
                {
                    Session.Diagnostics.ReportCannotInferStateType(syntax.Identifier.Span, stateName);
                    stateType = TypeSymbol.Error;
                }
                else
                {
                    stateType = initializer.Type;
                }
            }
            else if (!Session.ConversionClassifier.CanAssignToLocal(stateType, initializer.Type))
            {
                Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Initializer), stateType.Name, initializer.Type.Name);
            }

            Session.StateDeclarationBinder.ValidateStateMetadata(
                syntax,
                stateName,
                stateType,
                synchronizationMode);

            var hasUnsupportedObjectInitializer = stateType == TypeSymbol.Object && initializer.Type != TypeSymbol.Error && Session.ConversionClassifier.CanAssignToLocal(stateType, initializer.Type);
            object initialValue = null;
            var hasConstantValue = !hasUnsupportedObjectInitializer && Session.ConstantEvaluator.TryEvaluateStateConstant(initializer, stateType, out initialValue);
            if (!hasConstantValue)
            {
                if (hasUnsupportedObjectInitializer)
                {
                    Session.Diagnostics.ReportUnsupportedObjectStateInitializer(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Initializer), stateName);
                }
                else
                {
                    Session.Diagnostics.ReportStateInitializerMustBeConstant(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Initializer), stateName);
                }
            }

            var stateSymbol = new StateVariableSymbol(stateName, stateType ?? TypeSymbol.Error, syntax.PubKeyword != null, synchronizationMode, initialValue, syntax.Identifier.Span, Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Initializer), ordinal);
            stateSymbol.Documentation = DocumentationComment.FromSyntax(syntax.Documentation);
            return new BoundStateDeclaration(stateSymbol, initializer);
        }

        private void ValidateStateMetadata(
            StateDeclarationSyntax syntax,
            string stateName,
            TypeSymbol stateType,
            StateSynchronizationMode? synchronizationMode)
        {
            if (stateType?.IsConstructedGenericType == true &&
                stateType.IsExternalBinding &&
                !Session.Environment.ExternCatalog.IsAbiTypeAvailable(stateType))
            {
                Session.Diagnostics.ReportExternalTypeNotExposed(
                    syntax.Identifier.Span,
                    stateType.RuntimeQualifiedName);
            }
            if (synchronizationMode.HasValue && stateType != TypeSymbol.Error && Session.ExpressionBinder.IsAggregateStorageType(stateType))
            {
                foreach (var leaf in AggregateLayout.GetLeaves(stateType))
                {
                    if (Session.Environment.ExternCatalog.IsSynchronizationSupported(leaf.Type, synchronizationMode.Value))
                    {
                        continue;
                    }

                    Session.Diagnostics.ReportUnsupportedAggregateSynchronization(syntax.SynchronizationModifier.ModeToken?.Span ?? syntax.SynchronizationModifier.SyncKeyword.Span, stateType.Name, leaf.PathText, leaf.Type.Name, StateSynchronizationModeFacts.GetSourceName(synchronizationMode.Value));
                }
            }
            else if (synchronizationMode.HasValue && stateType != TypeSymbol.Error && !Session.Environment.ExternCatalog.IsSynchronizationSupported(stateType, synchronizationMode.Value))
            {
                Session.Diagnostics.ReportUnsupportedStateSynchronization(syntax.SynchronizationModifier.ModeToken?.Span ?? syntax.SynchronizationModifier.SyncKeyword.Span, stateName, StateSynchronizationModeFacts.GetSourceName(synchronizationMode.Value), stateType.Name);
            }

            if (syntax.PubKeyword != null && stateType?.TypeKind == TypeKind.Array && !Session.ExpressionBinder.IsAggregateStorageType(stateType) && !Session.Environment.ExternCatalog.IsPublicArrayType(stateType))
            {
                Session.Diagnostics.ReportPublicArrayTypeNotAvailable(syntax.Identifier.Span, stateType.Name);
            }

            if (syntax.PubKeyword != null && stateType != TypeSymbol.Error && Session.ExpressionBinder.IsAggregateStorageType(stateType))
            {
                foreach (var leaf in AggregateLayout.GetLeaves(stateType))
                {
                    if (leaf.Type.TypeKind != TypeKind.Array || Session.Environment.ExternCatalog.IsPublicArrayType(leaf.Type))
                    {
                        continue;
                    }

                    Session.Diagnostics.ReportInvalidAggregateArrayLeafAbi(syntax.Identifier.Span, stateType.Name, leaf.PathText, leaf.Type.Name, "The installed SDK cannot expose this typed array in the Inspector.");
                }
            }

        }

        internal BoundStateDeclaration CreateErrorStateDeclaration(StateDeclarationSyntax syntax, int ordinal, StateSynchronizationMode? synchronizationMode)
        {
            var stateName = syntax.Identifier.Text ?? string.Empty;
            var symbol = new StateVariableSymbol(stateName, TypeSymbol.Error, syntax.PubKeyword != null, synchronizationMode, null, syntax.Identifier.Span, syntax.Identifier.Span, ordinal);
            symbol.Documentation = DocumentationComment.FromSyntax(syntax.Documentation);
            return new BoundStateDeclaration(symbol, BoundErrorExpression.Instance);
        }

        internal StateSynchronizationMode? BindSynchronizationMode(SynchronizationModifierSyntax syntax)
        {
            if (syntax == null || syntax.Mode == SynchronizationModeSyntaxKind.Invalid)
                return null;
            return syntax.Mode switch
            {
                SynchronizationModeSyntaxKind.None => StateSynchronizationMode.None,
                SynchronizationModeSyntaxKind.Linear => StateSynchronizationMode.Linear,
                SynchronizationModeSyntaxKind.Smooth => StateSynchronizationMode.Smooth,
                _ => null
            };
        }
    }
}
