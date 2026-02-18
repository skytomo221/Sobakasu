using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundAssignmentExpression : BoundExpression
  {
    public VariableSymbol Variable { get; }
    public BoundExpression Expression { get; }

    public BoundAssignmentExpression(VariableSymbol variable, BoundExpression expr)
        : base(variable.Type)
    {
      Variable = variable;
      Expression = expr;
    }
  }
}
