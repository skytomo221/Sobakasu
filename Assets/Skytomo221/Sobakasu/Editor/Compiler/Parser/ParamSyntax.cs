using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class ParamSyntax : SyntaxNode
  {
    public Token Name { get; }
    public Token ColonToken { get; }
    public Token TypeName { get; }

    public ParamSyntax(Token name, Token colon, Token typeName, TextSpan span)
        : base(span)
    {
      Name = name;
      ColonToken = colon;
      TypeName = typeName;
    }
  }
}
