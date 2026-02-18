using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BlockSyntax : StatementSyntax
  {
    public Token LBrace { get; }
    public IReadOnlyList<StatementSyntax> Statements { get; }
    public Token RBrace { get; }

    public BlockSyntax(Token lBrace, IReadOnlyList<StatementSyntax> statements, Token rBrace, TextSpan span)
        : base(span)
    {
      LBrace = lBrace;
      Statements = statements;
      RBrace = rBrace;
    }
  }
}
