using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class ErrorExpressionSyntax : ExpressionSyntax
  {
    public Token BadToken { get; }
    public ErrorExpressionSyntax(Token bad, TextSpan span) : base(span) => BadToken = bad;
  }
}
