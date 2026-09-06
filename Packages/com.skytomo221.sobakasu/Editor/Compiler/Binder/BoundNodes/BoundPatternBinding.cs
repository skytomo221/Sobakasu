using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundPatternBinding
    {
        public AggregateFieldSymbol Field { get; }
        public LocalVariableSymbol Variable { get; }

        public BoundPatternBinding(
            AggregateFieldSymbol field,
            LocalVariableSymbol variable)
        {
            Field = field ?? throw new ArgumentNullException(nameof(field));
            Variable = variable ?? throw new ArgumentNullException(nameof(variable));
        }
    }
}
