using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundTupleExpression : BoundExpression
    {
        public IReadOnlyList<BoundExpression> Elements { get; }
        public override TypeSymbol Type { get; }

        public BoundTupleExpression(
            TypeSymbol type,
            IReadOnlyList<BoundExpression> elements)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Elements = elements ?? throw new ArgumentNullException(nameof(elements));
        }
    }
}
