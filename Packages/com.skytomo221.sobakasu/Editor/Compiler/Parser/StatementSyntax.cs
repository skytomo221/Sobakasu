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
}
