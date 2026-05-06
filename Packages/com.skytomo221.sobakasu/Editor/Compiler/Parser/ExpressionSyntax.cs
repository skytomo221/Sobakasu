using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  abstract class ExpressionSyntax : SyntaxNode
  {
  }

  sealed class UnaryExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken OperatorToken { get; }
    public ExpressionSyntax Operand { get; }

    public UnaryExpressionSyntax(
        SyntaxToken operatorToken,
        ExpressionSyntax operand)
    {
      OperatorToken = operatorToken;
      Operand = operand;
    }
  }

  sealed class BinaryExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Left { get; }
    public SyntaxToken OperatorToken { get; }
    public ExpressionSyntax Right { get; }

    public BinaryExpressionSyntax(
        ExpressionSyntax left,
        SyntaxToken operatorToken,
        ExpressionSyntax right)
    {
      Left = left;
      OperatorToken = operatorToken;
      Right = right;
    }
  }

  sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken OpenParenToken { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken CloseParenToken { get; }

    public ParenthesizedExpressionSyntax(
        SyntaxToken openParenToken,
        ExpressionSyntax expression,
        SyntaxToken closeParenToken)
    {
      OpenParenToken = openParenToken;
      Expression = expression;
      CloseParenToken = closeParenToken;
    }
  }

  sealed class AssignmentExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Target { get; }
    public SyntaxToken OperatorToken { get; }
    public ExpressionSyntax Expression { get; }

    public AssignmentExpressionSyntax(
        ExpressionSyntax target,
        SyntaxToken operatorToken,
        ExpressionSyntax expression)
    {
      Target = target;
      OperatorToken = operatorToken;
      Expression = expression;
    }
  }
}
