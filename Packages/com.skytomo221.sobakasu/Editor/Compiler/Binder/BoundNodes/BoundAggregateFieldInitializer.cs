using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundAggregateFieldInitializer
  {
    public AggregateFieldSymbol Field { get; }
    public BoundExpression Expression { get; }

    public BoundAggregateFieldInitializer(
        AggregateFieldSymbol field,
        BoundExpression expression)
    {
      Field = field ?? throw new ArgumentNullException(nameof(field));
      Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }
  }
}
