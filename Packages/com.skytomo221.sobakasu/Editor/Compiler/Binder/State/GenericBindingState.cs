using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class GenericBindingState
    {
        internal Dictionary<string, TypeSymbol> CurrentTypeParameters { get; set; } =
            new(StringComparer.Ordinal);
        internal Dictionary<TypeSymbol, List<GenericImplTemplate>> ImplTemplates { get; } =
            new();
        internal List<PendingGenericMethodBinding> PendingMethodBindings { get; } =
            new();
    }
}
