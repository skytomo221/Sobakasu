using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class IfStatementSyntax : StatementSyntax
  {
    public Token IfKeyword { get; }
    public ExpressionSyntax Condition { get; }
    public StatementSyntax Then { get; }
    public Token? ElseKeyword { get; }
    public StatementSyntax? Else { get; }

    public IfStatementSyntax(Token ifKw, ExpressionSyntax cond, StatementSyntax thenStmt, Token? elseKw, StatementSyntax? elseStmt, TextSpan span)
        : base(span)
    {
      IfKeyword = ifKw;
      Condition = cond;
      Then = thenStmt;
      ElseKeyword = elseKw;
      Else = elseStmt;
    }
  }
}
