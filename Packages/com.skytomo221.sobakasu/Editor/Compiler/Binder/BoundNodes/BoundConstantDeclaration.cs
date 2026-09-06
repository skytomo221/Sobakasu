using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundConstantDeclaration : BoundNode
    {
        public ConstantSymbol ConstantSymbol { get; }
        public BoundExpression Initializer { get; }

        public BoundConstantDeclaration(
            ConstantSymbol constantSymbol,
            BoundExpression initializer)
        {
            ConstantSymbol = constantSymbol ??
                throw new ArgumentNullException(nameof(constantSymbol));
            Initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
        }
    }
}
