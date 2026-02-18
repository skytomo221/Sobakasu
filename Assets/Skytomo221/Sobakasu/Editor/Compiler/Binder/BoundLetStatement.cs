using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundLetStatement : BoundStatement
  {
    public VariableSymbol Variable { get; }
    public BoundExpression? Initializer { get; }

    public BoundLetStatement(VariableSymbol variable, BoundExpression? initializer)
    {
      Variable = variable;
      Initializer = initializer;
    }
  }
}
