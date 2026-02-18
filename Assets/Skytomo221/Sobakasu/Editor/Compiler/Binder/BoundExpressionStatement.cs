using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundExpressionStatement : BoundStatement
  {
    public BoundExpression Expression { get; }
    public BoundExpressionStatement(BoundExpression expression) => Expression = expression;
  }
}
