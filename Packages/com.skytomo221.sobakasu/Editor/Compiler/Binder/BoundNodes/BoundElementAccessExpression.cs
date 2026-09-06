using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundElementAccessExpression : BoundExpression
    {
        public BoundExpression Array { get; }
        public BoundExpression Index { get; }
        public ArrayIntrinsicSymbols Intrinsics { get; }
        public IReadOnlyList<ArrayIntrinsicSymbols> AggregateLeafIntrinsics { get; }
        public override TypeSymbol Type { get; }

        public BoundElementAccessExpression(
            BoundExpression array,
            BoundExpression index,
            ArrayIntrinsicSymbols intrinsics,
            IReadOnlyList<ArrayIntrinsicSymbols> aggregateLeafIntrinsics = null)
        {
            Array = array ?? throw new ArgumentNullException(nameof(array));
            Index = index ?? throw new ArgumentNullException(nameof(index));
            Intrinsics = intrinsics;
            AggregateLeafIntrinsics = aggregateLeafIntrinsics;
            Type = array.Type.ElementType;
        }
    }
}
