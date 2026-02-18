using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IrGotoIfTrue : IrStatement
  {
    public BoundExpression Condition { get; }
    public LabelSymbol Target { get; }
    public IrGotoIfTrue(BoundExpression condition, LabelSymbol target)
    {
      Condition = condition;
      Target = target;
    }
    public override string ToString() => $"ifTrue ({Condition.Type}) goto {Target};";
  }
}
