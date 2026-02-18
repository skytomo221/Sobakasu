using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
  {
    public Token LParen { get; }
    public ExpressionSyntax Expression { get; }
    public Token RParen { get; }

    public ParenthesizedExpressionSyntax(Token lParen, ExpressionSyntax expr, Token rParen, TextSpan span)
        : base(span)
    {
      LParen = lParen;
      Expression = expr;
      RParen = rParen;
    }
  }
}
