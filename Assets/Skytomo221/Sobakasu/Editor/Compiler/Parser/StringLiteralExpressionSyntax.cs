using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class StringLiteralExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken StringToken { get; }

    public StringLiteralExpressionSyntax(SyntaxToken stringToken)
    {
      StringToken = stringToken;
    }
  }
}
