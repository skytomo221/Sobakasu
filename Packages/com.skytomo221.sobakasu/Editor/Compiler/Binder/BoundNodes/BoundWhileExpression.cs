using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundWhileExpression : BoundExpression
  {
    public LoopSymbol Loop { get; }
    public BoundExpression Condition { get; }
    public BoundBlockExpression Body { get; }
    public override TypeSymbol Type => TypeSymbol.Unit;

    public BoundWhileExpression(
        LoopSymbol loop,
        BoundExpression condition,
        BoundBlockExpression body)
    {
      Loop = loop ?? throw new ArgumentNullException(nameof(loop));
      Condition = condition ?? throw new ArgumentNullException(nameof(condition));
      Body = body ?? throw new ArgumentNullException(nameof(body));
    }
  }
}
