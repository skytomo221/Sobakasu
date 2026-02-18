using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IrGotoIfFalse : IrStatement
  {
    public BoundExpression Condition { get; }
    public LabelSymbol Target { get; }
    public IrGotoIfFalse(BoundExpression condition, LabelSymbol target)
    {
      Condition = condition;
      Target = target;
    }
    public override string ToString() => $"ifFalse ({Condition.Type}) goto {Target};";
  }
}
