using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class OnDeclSyntax : TopLevelDeclSyntax
  {
    public Token OnKeyword { get; }
    public Token EventName { get; }
    public Token ColonToken { get; }
    public BlockSyntax Body { get; }

    public OnDeclSyntax(Token onKw, Token eventName, Token colon, BlockSyntax body, TextSpan span)
        : base(span)
    {
      OnKeyword = onKw;
      EventName = eventName;
      ColonToken = colon;
      Body = body;
    }
  }
}
