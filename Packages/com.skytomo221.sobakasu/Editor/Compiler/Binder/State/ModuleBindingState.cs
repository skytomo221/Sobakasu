using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Modules;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ModuleBindingState
    {
        internal StandardLibraryModule CurrentModule { get; set; }
        internal Dictionary<string, FunctionGroupSymbol> VisibleFunctions { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, TypeSymbol> VisibleTypes { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, ConstantSymbol> VisibleConstants { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<StandardLibraryModule, Dictionary<string, FunctionGroupSymbol>> Functions { get; } =
            new();
        internal Dictionary<StandardLibraryModule, Dictionary<string, TypeSymbol>> Types { get; } =
            new();
        internal Dictionary<StandardLibraryModule, Dictionary<string, Symbol>> Imports { get; } =
            new();
        internal Dictionary<StandardLibraryModule, Dictionary<string, Symbol>> Aliases { get; } =
            new();
        internal Dictionary<StandardLibraryModule, HashSet<string>> GlobImportNames { get; } =
            new();
        internal Dictionary<StandardLibraryModule, Dictionary<string, Symbol>> PreludeImports { get; } =
            new();
        internal Dictionary<StandardLibraryModule, ModuleSymbol> Symbols { get; } =
            new();
    }
}
