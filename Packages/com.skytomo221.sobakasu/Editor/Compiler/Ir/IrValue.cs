using System;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler.Ir
{
    internal abstract class IrValue
    {
        protected IrValue(TypeSymbol type) { Type = type ?? throw new ArgumentNullException(nameof(type)); }
        public TypeSymbol Type { get; }
    }
}
