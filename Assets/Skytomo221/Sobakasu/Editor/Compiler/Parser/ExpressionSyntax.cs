using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public abstract class ExpressionSyntax : SyntaxNode
  {
    protected ExpressionSyntax(TextSpan span) : base(span) { }
  }
}
