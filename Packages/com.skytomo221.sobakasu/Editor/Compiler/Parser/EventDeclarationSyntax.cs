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

  sealed class ReceiveDeclarationSyntax : MemberSyntax
  {
    public SyntaxToken ReceiveKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<ParameterSyntax> Parameters { get; }
    public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }
    public SyntaxToken CloseParenToken { get; }
    public FunctionReturnTypeSyntax RejectedReturnTypeAnnotation { get; }
    public BlockStatementSyntax Body { get; }

    public ReceiveDeclarationSyntax(
        SyntaxToken receiveKeyword,
        SyntaxToken identifier,
        SyntaxToken openParenToken,
        IReadOnlyList<ParameterSyntax> parameters,
        IReadOnlyList<SyntaxToken> parameterSeparators,
        SyntaxToken closeParenToken,
        FunctionReturnTypeSyntax rejectedReturnTypeAnnotation,
        BlockStatementSyntax body)
    {
      ReceiveKeyword = receiveKeyword;
      Identifier = identifier;
      OpenParenToken = openParenToken;
      Parameters = parameters;
      ParameterSeparators = parameterSeparators;
      CloseParenToken = closeParenToken;
      RejectedReturnTypeAnnotation = rejectedReturnTypeAnnotation;
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

  sealed class ExternalFunctionBindingSyntax : SyntaxNode
  {
    public SyntaxToken EqualsToken { get; }
    public SyntaxToken MaybeKeyword { get; }
    public ExternExpressionSyntax ExternExpression { get; }
    public bool IsMalformed { get; }
    public bool IsMaybe => MaybeKeyword != null;

    public ExternalFunctionBindingSyntax(
        SyntaxToken equalsToken,
        SyntaxToken maybeKeyword,
        ExternExpressionSyntax externExpression,
        bool isMalformed)
    {
      EqualsToken = equalsToken;
      MaybeKeyword = maybeKeyword;
      ExternExpression = externExpression;
      IsMalformed = isMalformed;
    }
  }

  sealed class FunctionDeclarationSyntax : MemberSyntax
  {
    public SyntaxToken PubKeyword { get; }
    public SyntaxToken StaticKeyword { get; }
    public SyntaxToken FnKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken QuestionToken { get; }
    public SyntaxToken AtToken { get; }
    public SyntaxToken OperatorToken { get; }
    public string Name =>
        OperatorToken != null
            ? (AtToken == null ? string.Empty : "@") + (OperatorToken.Text ?? string.Empty)
            : (Identifier?.Text ?? string.Empty) +
              (QuestionToken == null ? string.Empty : "?");
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<ParameterSyntax> Parameters { get; }
    public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }
    public SyntaxToken CloseParenToken { get; }
    public FunctionReturnTypeSyntax ReturnTypeAnnotation { get; }
    public BlockStatementSyntax Body { get; }
    public ExternalFunctionBindingSyntax ExternalBinding { get; }
    public bool IsExternalBinding => ExternalBinding != null;

    public FunctionDeclarationSyntax(
        SyntaxToken pubKeyword,
        SyntaxToken staticKeyword,
        SyntaxToken fnKeyword,
        SyntaxToken identifier,
        SyntaxToken questionToken,
        SyntaxToken atToken,
        SyntaxToken operatorToken,
        SyntaxToken openParenToken,
        IReadOnlyList<ParameterSyntax> parameters,
        IReadOnlyList<SyntaxToken> parameterSeparators,
        SyntaxToken closeParenToken,
        FunctionReturnTypeSyntax returnTypeAnnotation,
        BlockStatementSyntax body,
        ExternalFunctionBindingSyntax externalBinding = null)
    {
      PubKeyword = pubKeyword;
      StaticKeyword = staticKeyword;
      FnKeyword = fnKeyword;
      Identifier = identifier;
      QuestionToken = questionToken;
      AtToken = atToken;
      OperatorToken = operatorToken;
      OpenParenToken = openParenToken;
      Parameters = parameters;
      ParameterSeparators = parameterSeparators;
      CloseParenToken = closeParenToken;
      ReturnTypeAnnotation = returnTypeAnnotation;
      Body = body;
      ExternalBinding = externalBinding;
    }
  }
}
