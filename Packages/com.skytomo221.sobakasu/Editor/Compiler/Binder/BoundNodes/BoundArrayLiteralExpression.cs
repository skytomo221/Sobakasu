using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundArrayLiteralExpression : BoundExpression
    {
        public IReadOnlyList<BoundExpression> Elements { get; }
        public TypeSymbol ElementType { get; }
        public ArrayIntrinsicSymbols Intrinsics { get; }
        public IReadOnlyList<ArrayIntrinsicSymbols> AggregateLeafIntrinsics { get; }
        public override TypeSymbol Type { get; }

        public BoundArrayLiteralExpression(
            IReadOnlyList<BoundExpression> elements,
            TypeSymbol arrayType,
            ArrayIntrinsicSymbols intrinsics,
            IReadOnlyList<ArrayIntrinsicSymbols> aggregateLeafIntrinsics = null)
        {
            Elements = elements;
            Type = arrayType ?? throw new ArgumentNullException(nameof(arrayType));
            ElementType = arrayType.ElementType;
            Intrinsics = intrinsics;
            AggregateLeafIntrinsics = aggregateLeafIntrinsics;
        }
    }
}
