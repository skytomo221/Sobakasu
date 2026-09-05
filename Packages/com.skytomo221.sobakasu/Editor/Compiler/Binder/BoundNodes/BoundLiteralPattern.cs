using System;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundLiteralPattern : BoundPattern
  {
    public BoundLiteralExpression Literal { get; }
    public BoundBinaryOperator ComparisonOperator { get; }

    public BoundLiteralPattern(
        BoundLiteralExpression literal,
        BoundBinaryOperator comparisonOperator,
        TextSpan span)
        : base(span)
    {
      Literal = literal ?? throw new ArgumentNullException(nameof(literal));
      ComparisonOperator = comparisonOperator;
    }
  }
}
