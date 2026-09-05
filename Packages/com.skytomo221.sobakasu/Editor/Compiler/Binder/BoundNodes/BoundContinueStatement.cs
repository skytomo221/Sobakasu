using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundContinueStatement : BoundStatement
  {
    public LoopSymbol Target { get; }

    public BoundContinueStatement(LoopSymbol target)
    {
      Target = target ?? throw new ArgumentNullException(nameof(target));
    }
  }
}
