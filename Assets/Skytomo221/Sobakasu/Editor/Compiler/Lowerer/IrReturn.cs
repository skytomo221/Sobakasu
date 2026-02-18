using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IrReturn : IrStatement
  {
    public BoundExpression? Expression { get; }
    public IrReturn(BoundExpression? expr) => Expression = expr;
  }
}
