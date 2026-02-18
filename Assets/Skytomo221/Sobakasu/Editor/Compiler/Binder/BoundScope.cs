using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  // ============================================================
  // Scopes
  // ============================================================

  internal sealed class BoundScope
  {
    private readonly Dictionary<string, VariableSymbol> _variables = new(StringComparer.Ordinal);
    public BoundScope? Parent { get; }

    public BoundScope(BoundScope? parent) => Parent = parent;

    public bool TryDeclare(VariableSymbol variable)
    {
      if (_variables.ContainsKey(variable.Name))
        return false;
      _variables.Add(variable.Name, variable);
      return true;
    }

    public VariableSymbol? TryLookup(string name)
    {
      for (var scope = this; scope != null; scope = scope.Parent)
      {
        if (scope._variables.TryGetValue(name, out var v))
          return v;
      }
      return null;
    }
  }
}
