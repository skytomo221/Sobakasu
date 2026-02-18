using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IrLabel : IrStatement
  {
    public LabelSymbol Label { get; }
    public IrLabel(LabelSymbol label) => Label = label;
    public override string ToString() => $"{Label}:";
  }
}
