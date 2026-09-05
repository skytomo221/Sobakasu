using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundNetworkSendStatement : BoundStatement
  {
    public NetworkReceiveSymbol Receiver { get; }
    public IReadOnlyList<BoundExpression> Arguments { get; }
    public BoundExpression Target { get; }
    public TypeSymbol CurrentBehaviourType { get; }
    public string ExternSignature { get; }

    public BoundNetworkSendStatement(
        NetworkReceiveSymbol receiver,
        IReadOnlyList<BoundExpression> arguments,
        BoundExpression target,
        TypeSymbol currentBehaviourType,
        string externSignature)
    {
      Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
      Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
      Target = target ?? throw new ArgumentNullException(nameof(target));
      CurrentBehaviourType = currentBehaviourType ??
          throw new ArgumentNullException(nameof(currentBehaviourType));
      ExternSignature = externSignature ??
          throw new ArgumentNullException(nameof(externSignature));
    }
  }
}
