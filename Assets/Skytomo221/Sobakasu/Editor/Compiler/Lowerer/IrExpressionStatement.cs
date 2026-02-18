using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IrExpressionStatement : IrStatement
  {
    public BoundExpression Expression { get; }
    public IrExpressionStatement(BoundExpression expr) => Expression = expr;
  }
}
