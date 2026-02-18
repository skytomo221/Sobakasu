using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundProgram : BoundNode
  {
    public IReadOnlyList<FunctionSymbol> Functions { get; }
    public IReadOnlyDictionary<FunctionSymbol, BoundBlockStatement> Bodies { get; }

    public BoundProgram(IReadOnlyList<FunctionSymbol> functions,
                        IReadOnlyDictionary<FunctionSymbol, BoundBlockStatement> bodies)
    {
      Functions = functions;
      Bodies = bodies;
    }
  }
}
