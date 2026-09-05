using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundUnaryExpression : BoundExpression
  {
    public BoundUnaryOperator Operator { get; }
    public BoundExpression Operand { get; }
    public override TypeSymbol Type => Operator.Type;

    public BoundUnaryExpression(
        BoundUnaryOperator @operator,
        BoundExpression operand)
    {
      Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
      Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }
  }
}
