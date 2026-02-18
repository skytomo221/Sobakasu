using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class VariableSymbol : Symbol
  {
    public TypeSymbol Type { get; }
    public bool IsReadOnly { get; }

    public VariableSymbol(string name, TypeSymbol type, bool isReadOnly)
        : base(name)
    {
      Type = type;
      IsReadOnly = isReadOnly;
    }
  }
}
