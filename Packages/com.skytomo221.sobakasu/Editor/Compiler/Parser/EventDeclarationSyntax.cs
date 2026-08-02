using Skytomo221.Sobakasu.Compiler.Syntax;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  class ParameterSyntax : SyntaxNode
  {
    public SyntaxToken Identifier { get; }
    public SyntaxToken ColonToken { get; }
    public TypeSyntax Type { get; }

    public ParameterSyntax(
        SyntaxToken identifier,
        SyntaxToken colonToken,
        TypeSyntax type)
    {
      Identifier = identifier;
      ColonToken = colonToken;
      Type = type;
    }
  }

  sealed class EventParameterSyntax : ParameterSyntax
  {
    public EventParameterSyntax(
        SyntaxToken identifier,
        SyntaxToken colonToken,
        TypeSyntax type)
        : base(identifier, colonToken, type)
    {
    }
  }

  sealed class EventDeclarationSyntax : MemberSyntax
  {
    public SyntaxToken OnKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<ParameterSyntax> Parameters { get; }
    public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }
    public SyntaxToken CloseParenToken { get; }
    public TypeClauseSyntax ReturnTypeAnnotation { get; }
    public BlockStatementSyntax Body { get; }

    public EventDeclarationSyntax(
        SyntaxToken onKeyword,
        SyntaxToken identifier,
        SyntaxToken openParenToken,
        IReadOnlyList<ParameterSyntax> parameters,
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

  sealed class FunctionReturnTypeSyntax : SyntaxNode
  {
    public SyntaxToken ArrowToken { get; }
    public TypeSyntax Type { get; }

    public FunctionReturnTypeSyntax(
        SyntaxToken arrowToken,
        TypeSyntax type)
    {
      ArrowToken = arrowToken;
      Type = type;
    }
  }

  sealed class FunctionDeclarationSyntax : MemberSyntax
  {
    public SyntaxToken FnKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken QuestionToken { get; }
    public string Name =>
        (Identifier.Text ?? string.Empty) +
        (QuestionToken == null ? string.Empty : "?");
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<ParameterSyntax> Parameters { get; }
    public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }
    public SyntaxToken CloseParenToken { get; }
    public FunctionReturnTypeSyntax ReturnTypeAnnotation { get; }
    public BlockStatementSyntax Body { get; }

    public FunctionDeclarationSyntax(
        SyntaxToken fnKeyword,
        SyntaxToken identifier,
        SyntaxToken questionToken,
        SyntaxToken openParenToken,
        IReadOnlyList<ParameterSyntax> parameters,
        IReadOnlyList<SyntaxToken> parameterSeparators,
        SyntaxToken closeParenToken,
        FunctionReturnTypeSyntax returnTypeAnnotation,
        BlockStatementSyntax body)
    {
      FnKeyword = fnKeyword;
      Identifier = identifier;
      QuestionToken = questionToken;
      OpenParenToken = openParenToken;
      Parameters = parameters;
      ParameterSeparators = parameterSeparators;
      CloseParenToken = closeParenToken;
      ReturnTypeAnnotation = returnTypeAnnotation;
      Body = body;
    }
  }
}
