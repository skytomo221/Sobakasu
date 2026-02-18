using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class CallExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Callee { get; }
    public Token LParen { get; }
    public IReadOnlyList<ExpressionSyntax> Arguments { get; }
    public Token RParen { get; }

    public CallExpressionSyntax(ExpressionSyntax callee, Token lParen, IReadOnlyList<ExpressionSyntax> args, Token rParen, TextSpan span)
        : base(span)
    {
      Callee = callee;
      LParen = lParen;
      Arguments = args;
      RParen = rParen;
    }
  }
}
