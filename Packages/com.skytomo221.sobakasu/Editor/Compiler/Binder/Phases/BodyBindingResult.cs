using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BodyBindingResult
  {
    internal BodyBindingResult(
        IReadOnlyList<BoundFunctionDeclaration> functions,
        IReadOnlyList<BoundEventDeclaration> events,
        IReadOnlyList<BoundNetworkReceiveDeclaration> networkReceivers)
    {
      Functions = functions;
      Events = events;
      NetworkReceivers = networkReceivers;
    }

    internal IReadOnlyList<BoundFunctionDeclaration> Functions { get; }
    internal IReadOnlyList<BoundEventDeclaration> Events { get; }
    internal IReadOnlyList<BoundNetworkReceiveDeclaration> NetworkReceivers { get; }
  }
}
