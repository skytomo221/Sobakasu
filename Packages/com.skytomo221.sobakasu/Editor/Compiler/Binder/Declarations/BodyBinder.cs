using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BodyBinder : BinderComponent
    {
        internal BodyBinder(BindingSession session) : base(session)
        {
        }

        internal BoundFunctionDeclaration BindFunctionDeclaration(
            FunctionDeclarationSyntax syntax,
            FunctionSymbol functionSymbol)
        {
            if (syntax.IsExternalBinding)
            {
                if (!Session.Callables.ExternalBindingExpressions.TryGetValue(
                        functionSymbol,
                        out var expression))
                {
                    Session.ExternDeclarationBinder.BindExternalFunctionSignature(syntax, functionSymbol);
                    Session.Callables.ExternalBindingExpressions.TryGetValue(functionSymbol, out expression);
                }

                expression ??= BoundErrorExpression.Instance;
                BoundStatement statement = functionSymbol.ReturnType == TypeSymbol.Unit
                    ? new BoundExpressionStatement(expression)
                    : new BoundReturnStatement(expression);
                return new BoundFunctionDeclaration(
                    functionSymbol,
                    new BoundBlockStatement(new[] { statement }));
            }

            var body = BindFunctionBody(syntax.Body, functionSymbol, out var sawValueReturn);
            if (functionSymbol.ReturnType != TypeSymbol.Unit &&
                !sawValueReturn &&
                Session.BlockBinder.GetBlockFallthroughType(body) != TypeSymbol.Never)
            {
                Session.Diagnostics.ReportReturnValueRequired(
                    functionSymbol.SourceSpan,
                    functionSymbol.Name,
                    functionSymbol.ReturnType.Name);
            }

            return new BoundFunctionDeclaration(functionSymbol, body);
        }

        internal BoundBlockStatement BindFunctionBody(
            BlockStatementSyntax syntax,
            FunctionSymbol functionSymbol,
            out bool sawValueReturn)
        {
            var previousBody = Session.Body;
            Session.Body = new BodyBindingContext
            {
                Scope = new BoundScope(previousBody.Scope),
                CurrentReturnType = functionSymbol.ReturnType,
                CurrentEventName = functionSymbol.Name,
                CurrentType = functionSymbol.ContainingType,
                CurrentFunction = functionSymbol,
                NextDestructuringTemporaryId = previousBody.NextDestructuringTemporaryId
            };

            foreach (var parameter in functionSymbol.Parameters)
                Session.Body.Scope.DeclareParameter(parameter);
            if (functionSymbol.SelfParameter != null)
                Session.Body.Scope.DeclareParameter(functionSymbol.SelfParameter);

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
