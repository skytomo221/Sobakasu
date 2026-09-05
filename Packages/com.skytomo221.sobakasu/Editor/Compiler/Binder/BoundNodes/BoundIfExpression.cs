using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundIfExpression : BoundExpression
  {
    public BoundExpression Condition { get; }
    public BoundBlockExpression ThenExpression { get; }
    public BoundExpression ElseExpression { get; }
    public override TypeSymbol Type { get; }

    public BoundIfExpression(
        BoundExpression condition,
        BoundBlockExpression thenExpression,
        BoundExpression elseExpression,
        TypeSymbol type)
    {
      Condition = condition ?? throw new ArgumentNullException(nameof(condition));
      ThenExpression = thenExpression ?? throw new ArgumentNullException(nameof(thenExpression));
      ElseExpression = elseExpression;
      Type = type ?? throw new ArgumentNullException(nameof(type));
    }
  }
}
