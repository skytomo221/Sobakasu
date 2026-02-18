using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundReturnStatement : BoundStatement
  {
    public BoundExpression? Expression { get; }
    public BoundReturnStatement(BoundExpression? expr) => Expression = expr;
  }
}
