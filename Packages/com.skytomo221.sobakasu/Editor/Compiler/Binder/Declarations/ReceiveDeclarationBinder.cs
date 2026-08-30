using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class ReceiveDeclarationBinder : BinderComponent
  {
    internal ReceiveDeclarationBinder(BindingSession session) : base(session)
    {
    }

    internal BoundNetworkReceiveDeclaration Bind(
        ReceiveDeclarationSyntax syntax,
        NetworkReceiveSymbol receiveSymbol)
    {
      var previousBody = Session.Body;
      Session.Body = new BodyBindingContext
      {
        Scope = new BoundScope(previousBody.Scope),
        CurrentReturnType = TypeSymbol.Unit,
        CurrentEventName = receiveSymbol.Name,
        NextDestructuringTemporaryId = previousBody.NextDestructuringTemporaryId
      };

      foreach (var parameter in receiveSymbol.Parameters)
        Session.Body.Scope.DeclareParameter(parameter);

      try
      {
        return new BoundNetworkReceiveDeclaration(
            receiveSymbol,
            Session.BlockBinder.BindBlockStatement(syntax.Body));
      }
      finally
      {
        previousBody.NextDestructuringTemporaryId = Session.Body.NextDestructuringTemporaryId;
        Session.Body = previousBody;
      }
    }
  }
}
