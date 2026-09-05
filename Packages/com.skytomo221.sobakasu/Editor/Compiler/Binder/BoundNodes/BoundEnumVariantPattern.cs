using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundEnumVariantPattern : BoundPattern
  {
    public EnumVariantSymbol Variant { get; }
    public IReadOnlyList<BoundPatternBinding> Bindings { get; }
    public BoundBinaryOperator TagComparisonOperator { get; }

    public BoundEnumVariantPattern(
        EnumVariantSymbol variant,
        IReadOnlyList<BoundPatternBinding> bindings,
        BoundBinaryOperator tagComparisonOperator,
        TextSpan span)
        : base(span)
    {
      Variant = variant ?? throw new ArgumentNullException(nameof(variant));
      Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
      TagComparisonOperator = tagComparisonOperator;
    }
  }
}
