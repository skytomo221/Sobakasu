using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class CallableBindingState
  {
    internal Dictionary<FunctionDeclarationSyntax, FunctionSymbol> FunctionSymbolsBySyntax { get; } =
        new();
    internal Dictionary<string, NetworkReceiveSymbol> NetworkReceiveSymbols { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<ReceiveDeclarationSyntax, NetworkReceiveSymbol> NetworkReceiveSymbolsBySyntax { get; } =
        new();
    internal HashSet<string> NetworkEntrypointNames { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<FunctionDeclarationSyntax, FunctionSymbol> MethodSymbolsBySyntax { get; } =
        new();
    internal Dictionary<FunctionSymbol, BoundExpression> ExternalBindingExpressions { get; } =
        new();
    internal Dictionary<FunctionDeclarationSyntax, StandardLibraryModule> FunctionModulesBySyntax { get; } =
        new();
    internal Dictionary<FunctionSymbol, StandardLibraryModule> ModulesByFunctionSymbol { get; } =
        new();
  }
}
