using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{
  // ============================================================
  // AST
  // ============================================================

  public abstract class SyntaxNode
  {
    public TextSpan Span { get; protected set; }
    protected SyntaxNode(TextSpan span) => Span = span;
  }
}
