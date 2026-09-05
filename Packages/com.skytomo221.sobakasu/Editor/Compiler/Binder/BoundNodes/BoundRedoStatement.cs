using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundRedoStatement : BoundStatement
  {
    public LoopSymbol Target { get; }

    public BoundRedoStatement(LoopSymbol target)
    {
      Target = target ?? throw new ArgumentNullException(nameof(target));
    }
  }
}
