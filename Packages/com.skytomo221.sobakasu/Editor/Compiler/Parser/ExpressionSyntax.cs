using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  abstract class ExpressionSyntax : SyntaxNode
  {
  }

  sealed class AssignmentExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken IdentifierToken { get; }
    public SyntaxToken EqualsToken { get; }
    public ExpressionSyntax Expression { get; }

    public AssignmentExpressionSyntax(
        SyntaxToken identifierToken,
        SyntaxToken equalsToken,
        ExpressionSyntax expression)
    {
      IdentifierToken = identifierToken;
      EqualsToken = equalsToken;
      Expression = expression;
    }
  }
}
