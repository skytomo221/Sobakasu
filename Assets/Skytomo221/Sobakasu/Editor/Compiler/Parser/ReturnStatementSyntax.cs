using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class ReturnStatementSyntax : StatementSyntax
  {
    public Token ReturnKeyword { get; }
    public ExpressionSyntax? Expression { get; }
    public Token? Semicolon { get; }

    public ReturnStatementSyntax(Token retKw, ExpressionSyntax? expr, Token? semi, TextSpan span)
        : base(span)
    {
      ReturnKeyword = retKw;
      Expression = expr;
      Semicolon = semi;
    }
  }
}
