using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundNetworkReceiveDeclaration : BoundNode
  {
    public NetworkReceiveSymbol ReceiveSymbol { get; }
    public BoundBlockStatement Body { get; }

    public BoundNetworkReceiveDeclaration(
        NetworkReceiveSymbol receiveSymbol,
        BoundBlockStatement body)
    {
      ReceiveSymbol = receiveSymbol ??
          throw new ArgumentNullException(nameof(receiveSymbol));
      Body = body ?? throw new ArgumentNullException(nameof(body));
    }
  }
}
