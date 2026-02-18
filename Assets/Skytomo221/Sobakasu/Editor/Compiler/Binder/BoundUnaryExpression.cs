using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundUnaryExpression : BoundExpression
  {
    public BoundUnaryOperator Op { get; }
    public BoundExpression Operand { get; }

    public BoundUnaryExpression(BoundUnaryOperator op, BoundExpression operand)
        : base(op.ResultType)
    {
      Op = op;
      Operand = operand;
    }
  }
}
