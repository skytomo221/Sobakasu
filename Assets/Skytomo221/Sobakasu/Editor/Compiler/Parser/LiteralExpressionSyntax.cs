using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class LiteralExpressionSyntax : ExpressionSyntax
  {
    public Token LiteralToken { get; }
    public object? Value => LiteralToken.Value;

    public LiteralExpressionSyntax(Token lit, TextSpan span) : base(span) => LiteralToken = lit;
  }
}
