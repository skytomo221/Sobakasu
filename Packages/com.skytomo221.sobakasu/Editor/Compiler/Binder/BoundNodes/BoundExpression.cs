using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal abstract class BoundExpression : BoundNode
  {
    public abstract TypeSymbol Type { get; }
  }
}
