using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundAggregateFieldAccessExpression : BoundExpression
    {
        public BoundExpression Receiver { get; }
        public AggregateFieldSymbol Field { get; }
        public override TypeSymbol Type => Field.Type;

        public BoundAggregateFieldAccessExpression(
            BoundExpression receiver,
            AggregateFieldSymbol field)
        {
            Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            Field = field ?? throw new ArgumentNullException(nameof(field));
        }
    }
}
