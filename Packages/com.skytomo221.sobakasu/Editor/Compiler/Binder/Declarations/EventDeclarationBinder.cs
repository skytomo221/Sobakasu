using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class EventDeclarationBinder : BinderComponent
    {
        internal EventDeclarationBinder(BindingSession session) : base(session)
        {
        }

        internal BoundEventDeclaration Bind(
            EventDeclarationSyntax syntax,
            ISet<string> declaredEvents)
        {
            var eventName = syntax.Identifier.Text ?? "";
            EventCatalog.TryGet(eventName, out var definition);
            if (definition == null)
            {
                Session.Diagnostics.ReportUnknownEvent(syntax.Identifier.Span, eventName);
                definition = CreateErrorEventDefinition(eventName);
            }
            else if (definition.SupportLevel == EventSupportLevel.PendingSignature ||
                     definition.SupportLevel == EventSupportLevel.Unsupported)
            {
                Session.Diagnostics.ReportUnsupportedEventSignature(syntax.Identifier.Span, eventName);
            }

            if (!declaredEvents.Add(eventName))
                Session.Diagnostics.ReportDuplicateEvent(syntax.Identifier.Span, eventName);
            if (Session.Callables.NetworkEntrypointNames.Contains(definition.UdonName))
            {
                Session.Diagnostics.ReportNetworkEntrypointCollision(
                    syntax.Identifier.Span,
                    definition.UdonName);
            }

            var parameters = BindParameters(syntax, definition);
            var returnType = BindReturnType(syntax, definition);
            if (!string.IsNullOrWhiteSpace(definition.Requirement))
            {
                Session.Diagnostics.ReportEventRequiresComponent(
                    syntax.Identifier.Span,
                    eventName,
                    definition.Requirement);
            }

            var eventSymbol = new BoundEventSymbol(
                eventName,
                definition.UdonName,
                returnType,
                parameters,
                definition.Category,
                definition.Requirement,
                definition.SupportLevel,
                syntax.Identifier.Span,
                definition.ReturnValueStorageName);
            var body = BindBody(syntax.Body, eventSymbol, out var sawValueReturn);
            if (eventSymbol.ReturnType != TypeSymbol.Unit &&
                !sawValueReturn &&
                Session.BlockBinder.GetBlockFallthroughType(body) != TypeSymbol.Never)
            {
                Session.Diagnostics.ReportReturnValueRequired(
                    syntax.Identifier.Span,
                    eventName,
                    eventSymbol.ReturnType.Name);
            }

            return new BoundEventDeclaration(eventSymbol, body);
        }

        private static EventDefinition CreateErrorEventDefinition(string eventName)
        {
            return new EventDefinition(
                eventName,
                eventName,
                "_invalid_event",
                EventCategory.UdonInput,
                TypeSymbol.Unit,
                Array.Empty<EventParameterDefinition>(),
                null,
                EventSupportLevel.Unsupported);
        }

        private IReadOnlyList<ParameterSymbol> BindParameters(
            EventDeclarationSyntax syntax,
            EventDefinition definition)
        {
            var parameters = new List<ParameterSymbol>();
            var seenParameterNames = new HashSet<string>(StringComparer.Ordinal);
            if (definition.SupportLevel == EventSupportLevel.Supported &&
                syntax.Parameters.Count != definition.Parameters.Count)
            {
                Session.Diagnostics.ReportEventParameterCountMismatch(
                    syntax.Identifier.Span,
                    definition.SourceName,
                    definition.Parameters.Count,
                    syntax.Parameters.Count);
            }

            for (var index = 0; index < syntax.Parameters.Count; index++)
            {
                var parameterSyntax = syntax.Parameters[index];
                var parameterName = parameterSyntax.Identifier.Text ?? string.Empty;
                if (!seenParameterNames.Add(parameterName))
                {
                    Session.Diagnostics.ReportDuplicateParameterName(
                        parameterSyntax.Identifier.Span,
                        parameterName);
                }

                var parameterType = Session.TypeResolver.BindTypeSyntax(parameterSyntax.Type);
                var udonStorageName = parameterName;
                if (definition.SupportLevel == EventSupportLevel.Supported &&
                    index < definition.Parameters.Count)
                {
                    var expectedParameter = definition.Parameters[index];
                    udonStorageName = expectedParameter.UdonStorageName;
                    if (parameterType != TypeSymbol.Error && parameterType != expectedParameter.Type)
                    {
                        Session.Diagnostics.ReportEventParameterTypeMismatch(
                            parameterSyntax.Type.GetSpan(),
                            definition.SourceName,
                            index,
                            expectedParameter.Type.Name,
                            parameterType.Name);
                    }
                }

                parameters.Add(new ParameterSymbol(
                    parameterName,
                    parameterType,
                    index,
                    udonStorageName,
                    parameterSyntax.Identifier.Span));
            }

            return parameters;
        }

        private TypeSymbol BindReturnType(
            EventDeclarationSyntax syntax,
            EventDefinition definition)
        {
            if (syntax.ReturnTypeAnnotation == null)
            {
                if (definition.ReturnType != TypeSymbol.Unit &&
                    definition.SupportLevel == EventSupportLevel.Supported)
                {
                    Session.Diagnostics.ReportEventReturnTypeRequired(
                        syntax.Identifier.Span,
                        definition.SourceName,
                        definition.ReturnType.Name);
                }

                return definition.ReturnType;
            }

            var declaredReturnType = Session.TypeResolver.BindTypeClause(syntax.ReturnTypeAnnotation);
            if (definition.SupportLevel != EventSupportLevel.Supported)
                return declaredReturnType;

            if (declaredReturnType != TypeSymbol.Error && declaredReturnType != definition.ReturnType)
            {
                Session.Diagnostics.ReportEventReturnTypeMismatch(
                    syntax.ReturnTypeAnnotation.Type.GetSpan(),
                    definition.SourceName,
                    definition.ReturnType.Name,
                    declaredReturnType.Name);
            }

            return definition.ReturnType;
        }

        private BoundBlockStatement BindBody(
            BlockStatementSyntax syntax,
            BoundEventSymbol eventSymbol,
            out bool sawValueReturn)
        {
            var previousBody = Session.Body;
            Session.Body = new BodyBindingContext
            {
                Scope = new BoundScope(previousBody.Scope),
                CurrentReturnType = eventSymbol.ReturnType,
                CurrentEventName = eventSymbol.SourceName,
                NextDestructuringTemporaryId = previousBody.NextDestructuringTemporaryId
            };

            foreach (var parameter in eventSymbol.Parameters)
                Session.Body.Scope.DeclareParameter(parameter);

            try
            {
                var body = Session.BlockBinder.BindBlockStatement(syntax);
                sawValueReturn = Session.Body.SawValueReturn;
                return body;
            }
            finally
            {
                previousBody.NextDestructuringTemporaryId = Session.Body.NextDestructuringTemporaryId;
                Session.Body = previousBody;
            }
        }
    }
}
