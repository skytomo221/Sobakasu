using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class ExpressionStatementSyntax : StatementSyntax
  {
    public ExpressionSyntax Expression { get; }
    public SyntaxToken SemicolonToken { get; }

    public ExpressionStatementSyntax(
        ExpressionSyntax expression,
        SyntaxToken semicolonToken)
    {
      Expression = expression;
      SemicolonToken = semicolonToken;
    }
  }
}
