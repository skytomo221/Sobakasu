using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class ExpressionStatementSyntax : StatementSyntax
  {
    public ExpressionSyntax Expression { get; }
    public Token? Semicolon { get; }

    public ExpressionStatementSyntax(ExpressionSyntax expr, Token? semi, TextSpan span)
        : base(span)
    {
      Expression = expr;
      Semicolon = semi;
    }
  }
}
