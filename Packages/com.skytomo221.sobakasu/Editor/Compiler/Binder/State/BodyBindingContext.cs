using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BodyBindingContext
    {
        internal BoundScope Scope { get; set; }
        internal TypeSymbol CurrentType { get; set; }
        internal FunctionSymbol CurrentFunction { get; set; }
        internal TypeSymbol CurrentReturnType { get; set; } = TypeSymbol.Unit;
        internal string CurrentEventName { get; set; } = string.Empty;
        internal bool SawValueReturn { get; set; }
        internal int NextDestructuringTemporaryId { get; set; }
        internal List<LoopBindingContext> LoopContexts { get; } = new();
    }
}
