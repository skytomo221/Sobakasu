using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{
  // ============================================================
  // IR
  // ============================================================

  public sealed class LabelSymbol
  {
    public string Name { get; }
    public LabelSymbol(string name) => Name = name;
    public override string ToString() => Name;
  }
}
