using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundWhileStatement : BoundStatement
  {
    public BoundExpression Condition { get; }
    public BoundStatement Body { get; }

    public BoundWhileStatement(BoundExpression condition, BoundStatement body)
    {
      Condition = condition;
      Body = body;
    }
  }
}
