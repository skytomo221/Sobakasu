using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundBlockExpression : BoundExpression
    {
        public BoundBlockStatement Block { get; }
        public BoundExpression TrailingExpression { get; }
        public override TypeSymbol Type { get; }

        public BoundBlockExpression(
            BoundBlockStatement block,
            BoundExpression trailingExpression,
            TypeSymbol type)
        {
            Block = block ?? throw new ArgumentNullException(nameof(block));
            TrailingExpression = trailingExpression;
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }
    }
}
