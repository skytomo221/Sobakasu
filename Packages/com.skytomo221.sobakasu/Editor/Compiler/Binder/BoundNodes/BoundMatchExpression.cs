using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundMatchExpression : BoundExpression
  {
    public BoundExpression Expression { get; }
    public IReadOnlyList<BoundMatchArm> Arms { get; }
    public override TypeSymbol Type { get; }

    public BoundMatchExpression(
        BoundExpression expression,
        IReadOnlyList<BoundMatchArm> arms,
        TypeSymbol type)
    {
      Expression = expression ?? throw new ArgumentNullException(nameof(expression));
      Arms = arms ?? throw new ArgumentNullException(nameof(arms));
      Type = type ?? throw new ArgumentNullException(nameof(type));
    }
  }
}
