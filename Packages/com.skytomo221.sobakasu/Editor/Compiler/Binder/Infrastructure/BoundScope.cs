using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundScope
  {
    private readonly List<LocalVariableSymbol> _locals = new();
    private readonly List<ParameterSymbol> _parameters = new();
    public BoundScope(BoundScope parent)
    {
      Parent = parent;
    }
  
    public BoundScope Parent { get; }
  
    public void Declare(LocalVariableSymbol local)
    {
      if (local == null)
        throw new ArgumentNullException(nameof(local));
      _locals.Add(local);
    }
  
    public void DeclareParameter(ParameterSymbol parameter)
    {
      if (parameter == null)
        throw new ArgumentNullException(nameof(parameter));
      _parameters.Add(parameter);
    }
  
    public bool TryLookupLocal(string name, out LocalVariableSymbol local)
    {
      for (var index = _locals.Count - 1; index >= 0; index--)
      {
        if (_locals[index].Name == name)
        {
          local = _locals[index];
          return true;
        }
      }
  
      if (Parent != null)
        return Parent.TryLookupLocal(name, out local);
      local = null;
      return false;
    }
  
    public bool TryLookupSymbol(string name, out Symbol symbol)
    {
      if (TryLookupLocal(name, out var local))
      {
        symbol = local;
        return true;
      }
  
      for (var index = _parameters.Count - 1; index >= 0; index--)
      {
        if (_parameters[index].Name == name)
        {
          symbol = _parameters[index];
          return true;
        }
      }
  
      if (Parent != null)
        return Parent.TryLookupSymbol(name, out symbol);
      symbol = null;
      return false;
    }
  }
}
