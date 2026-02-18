using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  // Expressions
  public sealed class BoundErrorExpression : BoundExpression
  {
    public BoundErrorExpression() : base(TypeSymbol.Error) { }
  }
}
