using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundMaybeExternBindingExpression : BoundExpression
    {
        public BoundCallExpression RawExpression { get; }
        public ExternMaybeOutputProjection Projection { get; }
        public ExternMethodSymbol ValidityMethod => Projection.ValidityMethod;
        public EnumVariantSymbol JustVariant => Projection.JustVariant;
        public EnumVariantSymbol NothingVariant => Projection.NothingVariant;
        public override TypeSymbol Type => Projection.Type;

        public BoundMaybeExternBindingExpression(
            BoundCallExpression rawExpression,
            ExternMaybeOutputProjection projection)
        {
            RawExpression = rawExpression ??
                throw new ArgumentNullException(nameof(rawExpression));
            Projection = projection ?? throw new ArgumentNullException(nameof(projection));
        }
    }
}
