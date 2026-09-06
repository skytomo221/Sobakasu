using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class AggregateConstantValue
    {
        public TypeSymbol Type { get; }
        public IReadOnlyList<object> Leaves { get; }

        public AggregateConstantValue(TypeSymbol type, IReadOnlyList<object> leaves)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Leaves = leaves ?? throw new ArgumentNullException(nameof(leaves));
        }
    }
}
