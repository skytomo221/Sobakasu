using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class DeclarationBindingState
  {
    internal Dictionary<string, StateVariableSymbol> StateSymbols { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, TypeSymbol> ExternalBindingsByRuntimeType { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> MethodGroupsByType { get; } =
        new();
    internal Dictionary<MemberSyntax, TypeSymbol> AggregateTypesBySyntax { get; } =
        new();
  }
}
