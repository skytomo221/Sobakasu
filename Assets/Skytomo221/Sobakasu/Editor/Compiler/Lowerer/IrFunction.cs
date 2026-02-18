using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IrFunction
  {
    public FunctionSymbol Symbol { get; }
    public IrBlock Body { get; }

    public IrFunction(FunctionSymbol symbol, IrBlock body)
    {
      Symbol = symbol;
      Body = body;
    }
  }
}
