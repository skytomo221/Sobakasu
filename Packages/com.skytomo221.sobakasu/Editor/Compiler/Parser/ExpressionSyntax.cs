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

  sealed class ElementAccessExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Expression { get; }
    public SyntaxToken OpenBracketToken { get; }
    public ExpressionSyntax Index { get; }
    public SyntaxToken CloseBracketToken { get; }

    public ElementAccessExpressionSyntax(
        ExpressionSyntax expression,
        SyntaxToken openBracketToken,
        ExpressionSyntax index,
        SyntaxToken closeBracketToken)
    {
      Expression = expression;
      OpenBracketToken = openBracketToken;
      Index = index;
      CloseBracketToken = closeBracketToken;
    }
  }

  sealed class TupleExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<ExpressionSyntax> Elements { get; }
    public IReadOnlyList<SyntaxToken> Separators { get; }
    public SyntaxToken CloseParenToken { get; }

    public TupleExpressionSyntax(
        SyntaxToken openParenToken,
        IReadOnlyList<ExpressionSyntax> elements,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken closeParenToken)
    {
      OpenParenToken = openParenToken;
      Elements = elements;
      Separators = separators;
      CloseParenToken = closeParenToken;
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

  abstract class PatternSyntax : SyntaxNode
  {
  }

  sealed class WildcardPatternSyntax : PatternSyntax
  {
    public SyntaxToken UnderscoreToken { get; }

    public WildcardPatternSyntax(SyntaxToken underscoreToken)
    {
      UnderscoreToken = underscoreToken;
    }
  }

  sealed class LiteralPatternSyntax : PatternSyntax
  {
    public SyntaxToken LiteralToken { get; }

    public LiteralPatternSyntax(SyntaxToken literalToken)
    {
      LiteralToken = literalToken;
    }
  }

  sealed class UnsupportedPatternSyntax : PatternSyntax
  {
    public SyntaxToken Token { get; }

    public UnsupportedPatternSyntax(SyntaxToken token)
    {
      Token = token;
    }
  }

  sealed class PatternBindingSyntax : SyntaxNode
  {
    public SyntaxToken Identifier { get; }
    public bool IsWildcard => Identifier.Text == "_";
    public bool IsSupported { get; }

    public PatternBindingSyntax(
        SyntaxToken identifier,
        bool isSupported = true)
    {
      Identifier = identifier;
      IsSupported = isSupported;
    }
  }

  abstract class EnumVariantPatternSyntax : PatternSyntax
  {
    protected EnumVariantPatternSyntax(
        TypeSyntax enumType,
        SyntaxToken dotToken,
        SyntaxToken variantIdentifier)
    {
      EnumType = enumType;
      DotToken = dotToken;
      VariantIdentifier = variantIdentifier;
    }

    public TypeSyntax EnumType { get; }
    public SyntaxToken DotToken { get; }
    public SyntaxToken VariantIdentifier { get; }
  }

  sealed class EnumUnitVariantPatternSyntax : EnumVariantPatternSyntax
  {
    public EnumUnitVariantPatternSyntax(
        TypeSyntax enumType,
        SyntaxToken dotToken,
        SyntaxToken variantIdentifier)
        : base(enumType, dotToken, variantIdentifier)
    {
    }
  }

  sealed class EnumTupleVariantPatternSyntax : EnumVariantPatternSyntax
  {
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<PatternBindingSyntax> Bindings { get; }
    public IReadOnlyList<SyntaxToken> Separators { get; }
    public SyntaxToken CloseParenToken { get; }

    public EnumTupleVariantPatternSyntax(
        TypeSyntax enumType,
        SyntaxToken dotToken,
        SyntaxToken variantIdentifier,
        SyntaxToken openParenToken,
        IReadOnlyList<PatternBindingSyntax> bindings,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken closeParenToken)
        : base(enumType, dotToken, variantIdentifier)
    {
      OpenParenToken = openParenToken;
      Bindings = bindings;
      Separators = separators;
      CloseParenToken = closeParenToken;
    }
  }

  sealed class EnumStructVariantPatternSyntax : EnumVariantPatternSyntax
  {
    public SyntaxToken OpenBraceToken { get; }
    public IReadOnlyList<PatternBindingSyntax> Fields { get; }
    public IReadOnlyList<SyntaxToken> Separators { get; }
    public SyntaxToken CloseBraceToken { get; }

    public EnumStructVariantPatternSyntax(
        TypeSyntax enumType,
        SyntaxToken dotToken,
        SyntaxToken variantIdentifier,
        SyntaxToken openBraceToken,
        IReadOnlyList<PatternBindingSyntax> fields,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken closeBraceToken)
        : base(enumType, dotToken, variantIdentifier)
    {
      OpenBraceToken = openBraceToken;
      Fields = fields;
      Separators = separators;
      CloseBraceToken = closeBraceToken;
    }
  }

  sealed class MatchArmSyntax : SyntaxNode
  {
    public PatternSyntax Pattern { get; }
    public SyntaxToken FatArrowToken { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken CommaToken { get; }

    public MatchArmSyntax(
        PatternSyntax pattern,
        SyntaxToken fatArrowToken,
        ExpressionSyntax expression,
        SyntaxToken commaToken)
    {
      Pattern = pattern;
      FatArrowToken = fatArrowToken;
      Expression = expression;
      CommaToken = commaToken;
    }
  }

  sealed class MatchExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken MatchKeyword { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken OpenBraceToken { get; }
    public IReadOnlyList<MatchArmSyntax> Arms { get; }
    public SyntaxToken CloseBraceToken { get; }

    public MatchExpressionSyntax(
        SyntaxToken matchKeyword,
        ExpressionSyntax expression,
        SyntaxToken openBraceToken,
        IReadOnlyList<MatchArmSyntax> arms,
        SyntaxToken closeBraceToken)
    {
      MatchKeyword = matchKeyword;
      Expression = expression;
      OpenBraceToken = openBraceToken;
      Arms = arms;
      CloseBraceToken = closeBraceToken;
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
