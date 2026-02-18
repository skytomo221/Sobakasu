using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public abstract class Symbol
  {
    public string Name { get; }
    protected Symbol(string name) => Name = name;
    public override string ToString() => Name;
  }
}
