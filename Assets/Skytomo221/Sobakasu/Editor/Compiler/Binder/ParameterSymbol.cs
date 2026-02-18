using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class ParameterSymbol : Symbol
  {
    public TypeSymbol Type { get; }

    public ParameterSymbol(string name, TypeSymbol type)
        : base(name)
    {
      Type = type;
    }
  }
}
