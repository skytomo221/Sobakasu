using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IrGoto : IrStatement
  {
    public LabelSymbol Target { get; }
    public IrGoto(LabelSymbol target) => Target = target;
    public override string ToString() => $"goto {Target};";
  }
}
