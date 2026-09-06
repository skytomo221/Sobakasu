using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ConstantBindingStateStore
    {
        internal Dictionary<StandardLibraryModule, Dictionary<string, ConstantSymbol>> ModuleConstants { get; } =
            new();
        internal Dictionary<ConstantSymbol, ConstDeclarationSyntax> SyntaxBySymbol { get; } =
            new();
        internal Dictionary<ConstantSymbol, StandardLibraryModule> ModulesBySymbol { get; } =
            new();
        internal Dictionary<ConstantSymbol, ConstantBindingState> BindingStates { get; } =
            new();
        internal Dictionary<ConstantSymbol, BoundConstantDeclaration> BoundConstants { get; } =
            new();
        internal List<ConstantSymbol> DeclarationOrder { get; } = new();
        internal List<ConstantSymbol> BindingStack { get; } = new();
    }
}
