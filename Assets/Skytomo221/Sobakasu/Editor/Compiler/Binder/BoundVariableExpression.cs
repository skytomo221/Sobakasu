using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundVariableExpression : BoundExpression
  {
    public VariableSymbol Variable { get; }
    public BoundVariableExpression(VariableSymbol variable) : base(variable.Type) => Variable = variable;
  }
}
