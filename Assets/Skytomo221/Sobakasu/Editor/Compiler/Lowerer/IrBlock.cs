using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IrBlock
  {
    public IReadOnlyList<IrStatement> Statements { get; }
    public IrBlock(IReadOnlyList<IrStatement> statements) => Statements = statements;
  }
}
