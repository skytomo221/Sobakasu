using Skytomo221.Sobakasu.Compiler.Syntax;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class EventParameterSyntax : SyntaxNode
  {
    public SyntaxToken Identifier { get; }
    public SyntaxToken ColonToken { get; }
    public TypeSyntax Type { get; }

    public EventParameterSyntax(
        SyntaxToken identifier,
        SyntaxToken colonToken,
        TypeSyntax type)
    {
      Identifier = identifier;
      ColonToken = colonToken;
      Type = type;
    }
  }

  sealed class EventDeclarationSyntax : MemberSyntax
  {
    public SyntaxToken OnKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<EventParameterSyntax> Parameters { get; }
    public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }
    public SyntaxToken CloseParenToken { get; }
    public TypeClauseSyntax ReturnTypeAnnotation { get; }
    public BlockStatementSyntax Body { get; }

    public EventDeclarationSyntax(
        SyntaxToken onKeyword,
        SyntaxToken identifier,
        SyntaxToken openParenToken,
        IReadOnlyList<EventParameterSyntax> parameters,
        IReadOnlyList<SyntaxToken> parameterSeparators,
        SyntaxToken closeParenToken,
        TypeClauseSyntax returnTypeAnnotation,
        BlockStatementSyntax body)
    {
      OnKeyword = onKeyword;
      Identifier = identifier;
      OpenParenToken = openParenToken;
      Parameters = parameters;
      ParameterSeparators = parameterSeparators;
      CloseParenToken = closeParenToken;
      ReturnTypeAnnotation = returnTypeAnnotation;
      Body = body;
    }
  }
}
