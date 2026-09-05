using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundStructConstructionExpression : BoundExpression
  {
    public IReadOnlyList<BoundAggregateFieldInitializer> Initializers { get; }
    public override TypeSymbol Type { get; }

    public BoundStructConstructionExpression(
        TypeSymbol type,
        IReadOnlyList<BoundAggregateFieldInitializer> initializers)
    {
      Type = type ?? throw new ArgumentNullException(nameof(type));
      Initializers = initializers ?? throw new ArgumentNullException(nameof(initializers));
    }
  }
}
