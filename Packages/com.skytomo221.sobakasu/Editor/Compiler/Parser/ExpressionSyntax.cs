using System.Collections.Generic;
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

  sealed class ExternExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken ExternKeyword { get; }
    public ExpressionSyntax Expression { get; }

    public ExternExpressionSyntax(
        SyntaxToken externKeyword,
        ExpressionSyntax expression)
    {
      ExternKeyword = externKeyword;
      Expression = expression;
    }
  }

  sealed class NewExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken NewKeyword { get; }
    public TypeSyntax Type { get; }
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<ExpressionSyntax> Arguments { get; }
    public SyntaxToken CloseParenToken { get; }

    public NewExpressionSyntax(
        SyntaxToken newKeyword,
        TypeSyntax type,
        SyntaxToken openParenToken,
        IReadOnlyList<ExpressionSyntax> arguments,
        SyntaxToken closeParenToken)
    {
      NewKeyword = newKeyword;
      Type = type;
      OpenParenToken = openParenToken;
      Arguments = arguments;
      CloseParenToken = closeParenToken;
    }
  }

  sealed class LoopLabelSyntax : SyntaxNode
  {
    public SyntaxToken LabelToken { get; }
    public SyntaxToken ColonToken { get; }

    public LoopLabelSyntax(
        SyntaxToken labelToken,
        SyntaxToken colonToken)
    {
      LabelToken = labelToken;
      ColonToken = colonToken;
    }
  }

  sealed class IfExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken IfKeyword { get; }
    public ExpressionSyntax Condition { get; }
    public BlockStatementSyntax ThenBlock { get; }
    public SyntaxToken ElseKeyword { get; }
    public ExpressionSyntax ElseExpression { get; }

    public IfExpressionSyntax(
        SyntaxToken ifKeyword,
        ExpressionSyntax condition,
        BlockStatementSyntax thenBlock,
        SyntaxToken elseKeyword,
        ExpressionSyntax elseExpression)
    {
      IfKeyword = ifKeyword;
      Condition = condition;
      ThenBlock = thenBlock;
      ElseKeyword = elseKeyword;
      ElseExpression = elseExpression;
    }
  }

  sealed class BlockExpressionSyntax : ExpressionSyntax
  {
    public BlockStatementSyntax Block { get; }

    public BlockExpressionSyntax(BlockStatementSyntax block)
    {
      Block = block;
    }
  }

  sealed class WhileExpressionSyntax : ExpressionSyntax
  {
    public LoopLabelSyntax Label { get; }
    public SyntaxToken WhileKeyword { get; }
    public ExpressionSyntax Condition { get; }
    public BlockStatementSyntax Body { get; }

    public WhileExpressionSyntax(
        LoopLabelSyntax label,
        SyntaxToken whileKeyword,
        ExpressionSyntax condition,
        BlockStatementSyntax body)
    {
      Label = label;
      WhileKeyword = whileKeyword;
      Condition = condition;
      Body = body;
    }
  }

  sealed class LoopExpressionSyntax : ExpressionSyntax
  {
    public LoopLabelSyntax Label { get; }
    public SyntaxToken LoopKeyword { get; }
    public BlockStatementSyntax Body { get; }

    public LoopExpressionSyntax(
        LoopLabelSyntax label,
        SyntaxToken loopKeyword,
        BlockStatementSyntax body)
    {
      Label = label;
      LoopKeyword = loopKeyword;
      Body = body;
    }
  }
}
