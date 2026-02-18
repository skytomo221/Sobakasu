using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public abstract class StatementSyntax : SyntaxNode
  {
    protected StatementSyntax(TextSpan span) : base(span) { }
  }
}
