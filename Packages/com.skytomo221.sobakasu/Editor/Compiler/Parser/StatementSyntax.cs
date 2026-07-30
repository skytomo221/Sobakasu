using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  abstract class StatementSyntax : SyntaxNode
  {
  }

  sealed class TypeClauseSyntax : SyntaxNode
  {
    public SyntaxToken ColonToken { get; }
    public TypeSyntax Type { get; }

    public TypeClauseSyntax(
        SyntaxToken colonToken,
        TypeSyntax type)
    {
      ColonToken = colonToken;
      Type = type;
    }
  }

  sealed class VariableDeclarationStatementSyntax : StatementSyntax
  {
    public SyntaxToken LetKeyword { get; }
    public SyntaxToken MutKeyword { get; }
    public SyntaxToken Identifier { get; }
    public TypeClauseSyntax TypeClause { get; }
    public SyntaxToken EqualsToken { get; }
    public ExpressionSyntax Initializer { get; }
    public SyntaxToken SemicolonToken { get; }

    public VariableDeclarationStatementSyntax(
        SyntaxToken letKeyword,
        SyntaxToken mutKeyword,
        SyntaxToken identifier,
        TypeClauseSyntax typeClause,
        SyntaxToken equalsToken,
        ExpressionSyntax initializer,
        SyntaxToken semicolonToken)
    {
      LetKeyword = letKeyword;
      MutKeyword = mutKeyword;
      Identifier = identifier;
      TypeClause = typeClause;
      EqualsToken = equalsToken;
      Initializer = initializer;
      SemicolonToken = semicolonToken;
    }
  }

  sealed class ReturnStatementSyntax : StatementSyntax
  {
    public SyntaxToken ReturnKeyword { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken SemicolonToken { get; }

    public ReturnStatementSyntax(
        SyntaxToken returnKeyword,
        ExpressionSyntax expression,
        SyntaxToken semicolonToken)
    {
      ReturnKeyword = returnKeyword;
      Expression = expression;
      SemicolonToken = semicolonToken;
    }
  }

  sealed class BreakStatementSyntax : StatementSyntax
  {
    public SyntaxToken BreakKeyword { get; }
    public SyntaxToken Label { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken SemicolonToken { get; }

    public BreakStatementSyntax(
        SyntaxToken breakKeyword,
        SyntaxToken label,
        ExpressionSyntax expression,
        SyntaxToken semicolonToken)
    {
      BreakKeyword = breakKeyword;
      Label = label;
      Expression = expression;
      SemicolonToken = semicolonToken;
    }
  }

  sealed class ContinueStatementSyntax : StatementSyntax
  {
    public SyntaxToken ContinueKeyword { get; }
    public SyntaxToken Label { get; }
    public SyntaxToken SemicolonToken { get; }

    public ContinueStatementSyntax(
        SyntaxToken continueKeyword,
        SyntaxToken label,
        SyntaxToken semicolonToken)
    {
      ContinueKeyword = continueKeyword;
      Label = label;
      SemicolonToken = semicolonToken;
    }
  }

  sealed class RedoStatementSyntax : StatementSyntax
  {
    public SyntaxToken RedoKeyword { get; }
    public SyntaxToken Label { get; }
    public SyntaxToken SemicolonToken { get; }

    public RedoStatementSyntax(
        SyntaxToken redoKeyword,
        SyntaxToken label,
        SyntaxToken semicolonToken)
    {
      RedoKeyword = redoKeyword;
      Label = label;
      SemicolonToken = semicolonToken;
    }
  }
}
