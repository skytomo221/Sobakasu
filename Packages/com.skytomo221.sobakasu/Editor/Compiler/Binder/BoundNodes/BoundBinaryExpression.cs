using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundBinaryExpression : BoundExpression
    {
        public BoundExpression Left { get; }
        public BoundBinaryOperator Operator { get; }
        public BoundExpression Right { get; }
        public override TypeSymbol Type => Operator.Type;

        public BoundBinaryExpression(
            BoundExpression left,
            BoundBinaryOperator @operator,
            BoundExpression right)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }
    }
}
