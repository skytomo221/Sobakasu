using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundEnumConstructionExpression : BoundExpression
    {
        public EnumVariantSymbol Variant { get; }
        public IReadOnlyList<BoundAggregateFieldInitializer> Initializers { get; }
        public override TypeSymbol Type => Variant.ContainingType;

        public BoundEnumConstructionExpression(
            EnumVariantSymbol variant,
            IReadOnlyList<BoundAggregateFieldInitializer> initializers)
        {
            Variant = variant ?? throw new ArgumentNullException(nameof(variant));
            Initializers = initializers ?? throw new ArgumentNullException(nameof(initializers));
        }
    }
}
