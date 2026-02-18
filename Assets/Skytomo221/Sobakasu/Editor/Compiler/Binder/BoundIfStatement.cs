using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundIfStatement : BoundStatement
  {
    public BoundExpression Condition { get; }
    public BoundStatement Then { get; }
    public BoundStatement? Else { get; }

    public BoundIfStatement(BoundExpression condition, BoundStatement thenStmt, BoundStatement? elseStmt)
    {
      Condition = condition;
      Then = thenStmt;
      Else = elseStmt;
    }
  }
}
