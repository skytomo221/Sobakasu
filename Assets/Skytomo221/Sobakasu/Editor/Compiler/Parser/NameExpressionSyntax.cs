using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class NameExpressionSyntax : ExpressionSyntax
  {
    public Token Identifier { get; }
    public NameExpressionSyntax(Token id, TextSpan span) : base(span) => Identifier = id;
  }
}
