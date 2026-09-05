using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class FunctionGroupSymbol : Symbol
  {
    private readonly List<FunctionSymbol> _functions = new();

    public override SymbolKind Kind => SymbolKind.FunctionGroup;
    public IReadOnlyList<FunctionSymbol> Functions => _functions;

    public FunctionGroupSymbol(string name)
        : base(name)
    {
    }

    public bool AddFunction(FunctionSymbol function)
    {
      if (function == null)
        throw new ArgumentNullException(nameof(function));

      foreach (var existing in _functions)
      {
        if (ReferenceEquals(existing, function))
          return false;
      }

      _functions.Add(function);
      return true;
    }

    public void AddFunctions(IEnumerable<FunctionSymbol> functions)
    {
      if (functions == null)
        return;

      foreach (var function in functions)
        AddFunction(function);
    }

    public bool TryMerge(FunctionGroupSymbol other)
    {
      if (other == null)
        return true;

      foreach (var candidate in other.Functions)
      {
        foreach (var existing in _functions)
        {
          if (ReferenceEquals(existing, candidate))
            continue;
          if (HaveSameParameterTypes(existing.Parameters, candidate.Parameters))
            return false;
        }
      }

      AddFunctions(other.Functions);
      return true;
    }

    private static bool HaveSameParameterTypes(
        IReadOnlyList<ParameterSymbol> left,
        IReadOnlyList<ParameterSymbol> right)
    {
      if (left.Count != right.Count)
        return false;

      for (var index = 0; index < left.Count; index++)
      {
        if (left[index].Type != right[index].Type)
          return false;
      }

      return true;
    }

    public FunctionGroupSymbol Clone()
    {
      var clone = new FunctionGroupSymbol(Name);
      clone.AddFunctions(_functions);
      return clone;
    }
  }
}
