using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundBlockStatement : BoundStatement
  {
    public IReadOnlyList<BoundStatement> Statements { get; }
    public BoundBlockStatement(IReadOnlyList<BoundStatement> statements) => Statements = statements;
  }
}
