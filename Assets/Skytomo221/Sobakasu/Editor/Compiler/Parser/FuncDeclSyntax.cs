using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class FuncDeclSyntax : TopLevelDeclSyntax
  {
    public Token FuncKeyword { get; }
    public Token Name { get; }
    public Token LParen { get; }
    public IReadOnlyList<ParamSyntax> Parameters { get; }
    public Token RParen { get; }
    public Token ColonToken { get; }
    public BlockSyntax Body { get; }

    public FuncDeclSyntax(Token funcKw, Token name, Token lParen, IReadOnlyList<ParamSyntax> parameters, Token rParen, Token colon, BlockSyntax body, TextSpan span)
        : base(span)
    {
      FuncKeyword = funcKw;
      Name = name;
      LParen = lParen;
      Parameters = parameters;
      RParen = rParen;
      ColonToken = colon;
      Body = body;
    }
  }
}
