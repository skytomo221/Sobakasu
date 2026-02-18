using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class WhileStatementSyntax : StatementSyntax
  {
    public Token WhileKeyword { get; }
    public ExpressionSyntax Condition { get; }
    public StatementSyntax Body { get; }

    public WhileStatementSyntax(Token whileKw, ExpressionSyntax cond, StatementSyntax body, TextSpan span)
        : base(span)
    {
      WhileKeyword = whileKw;
      Condition = cond;
      Body = body;
    }
  }
}
