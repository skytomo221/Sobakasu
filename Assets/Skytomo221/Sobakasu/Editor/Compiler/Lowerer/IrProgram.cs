using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IrProgram
  {
    public IReadOnlyList<IrFunction> Functions { get; }
    public IrProgram(IReadOnlyList<IrFunction> functions) => Functions = functions;
  }
}
