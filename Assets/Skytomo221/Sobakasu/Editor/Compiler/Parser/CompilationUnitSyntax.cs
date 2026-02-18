using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class CompilationUnitSyntax : SyntaxNode
  {
    public IReadOnlyList<TopLevelDeclSyntax> Declarations { get; }
    public Token EndOfFileToken { get; }

    public CompilationUnitSyntax(IReadOnlyList<TopLevelDeclSyntax> decls, Token eof, TextSpan span)
        : base(span)
    {
      Declarations = decls;
      EndOfFileToken = eof;
    }
  }
}
