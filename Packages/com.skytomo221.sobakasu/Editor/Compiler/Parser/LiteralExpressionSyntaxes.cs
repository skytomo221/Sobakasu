using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class IntegerLiteralExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken LiteralToken { get; }

    public IntegerLiteralExpressionSyntax(SyntaxToken literalToken)
    {
      LiteralToken = literalToken;
    }
  }

  sealed class FloatLiteralExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken LiteralToken { get; }

    public FloatLiteralExpressionSyntax(SyntaxToken literalToken)
    {
      LiteralToken = literalToken;
    }
  }

  sealed class CharacterLiteralExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken LiteralToken { get; }

    public CharacterLiteralExpressionSyntax(SyntaxToken literalToken)
    {
      LiteralToken = literalToken;
    }
  }

  sealed class BooleanLiteralExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken LiteralToken { get; }

    public BooleanLiteralExpressionSyntax(SyntaxToken literalToken)
    {
      LiteralToken = literalToken;
    }
  }

  sealed class NullLiteralExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken NullToken { get; }

    public NullLiteralExpressionSyntax(SyntaxToken nullToken)
    {
      NullToken = nullToken;
    }
  }
}
