using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundCallExpression : BoundExpression
  {
    public FunctionSymbol Function { get; }
    public IReadOnlyList<BoundExpression> Arguments { get; }

    public BoundCallExpression(FunctionSymbol function, IReadOnlyList<BoundExpression> args)
        : base(function.ReturnType)
    {
      Function = function;
      Arguments = args;
    }
  }
}
