using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public abstract class BoundExpression : BoundNode
  {
    public TypeSymbol Type { get; }
    protected BoundExpression(TypeSymbol type) => Type = type;
  }
}
