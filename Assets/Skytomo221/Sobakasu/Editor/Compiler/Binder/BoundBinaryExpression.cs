using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundBinaryExpression : BoundExpression
  {
    public BoundExpression Left { get; }
    public BoundBinaryOperator Op { get; }
    public BoundExpression Right { get; }

    public BoundBinaryExpression(BoundExpression left, BoundBinaryOperator op, BoundExpression right)
        : base(op.ResultType)
    {
      Left = left;
      Op = op;
      Right = right;
    }
  }
}
