using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundArrayLengthExpression : BoundExpression
    {
        public BoundExpression Array { get; }
        public ArrayIntrinsicSymbols Intrinsics { get; }
        public IReadOnlyList<ArrayIntrinsicSymbols> AggregateLeafIntrinsics { get; }
        public override TypeSymbol Type => TypeSymbol.I32;

        public BoundArrayLengthExpression(
            BoundExpression array,
            ArrayIntrinsicSymbols intrinsics,
            IReadOnlyList<ArrayIntrinsicSymbols> aggregateLeafIntrinsics = null)
        {
            Array = array ?? throw new ArgumentNullException(nameof(array));
            Intrinsics = intrinsics;
            AggregateLeafIntrinsics = aggregateLeafIntrinsics;
        }
    }
}
