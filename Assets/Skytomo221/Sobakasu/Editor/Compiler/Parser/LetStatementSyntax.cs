using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class LetStatementSyntax : StatementSyntax
  {
    public Token LetKeyword { get; }
    public Token Name { get; }
    public Token? ColonToken { get; }
    public Token? TypeName { get; }
    public Token? EqualToken { get; }
    public ExpressionSyntax? Initializer { get; }
    public Token? Semicolon { get; }

    public LetStatementSyntax(Token letKw, Token name, Token? colon, Token? typeName, Token? equal, ExpressionSyntax? init, Token? semi, TextSpan span)
        : base(span)
    {
      LetKeyword = letKw;
      Name = name;
      ColonToken = colon;
      TypeName = typeName;
      EqualToken = equal;
      Initializer = init;
      Semicolon = semi;
    }
  }
}
