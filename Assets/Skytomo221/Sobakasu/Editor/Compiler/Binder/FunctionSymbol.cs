using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class FunctionSymbol : Symbol
  {
    public IReadOnlyList<ParameterSymbol> Parameters { get; }
    public TypeSymbol ReturnType { get; }
    public bool IsEventEntry { get; }
    public string? UdonEventName { get; } // for on-decls

    public FunctionSymbol(
        string name,
        IReadOnlyList<ParameterSymbol> parameters,
        TypeSymbol returnType,
        bool isEventEntry,
        string? udonEventName)
        : base(name)
    {
      Parameters = parameters;
      ReturnType = returnType;
      IsEventEntry = isEventEntry;
      UdonEventName = udonEventName;
    }
  }
}
