namespace Skytomo221.Sobakasu.Compiler.Syntax
{
  public enum SyntaxKind
  {
    On,
    UseKeyword,
    AsKeyword,
    LetKeyword,
    MutKeyword,
    TrueKeyword,
    FalseKeyword,
    NullKeyword,
    U0Keyword,
    Identifier,
    Int8Literal,
    UInt8Literal,
    Int16Literal,
    UInt16Literal,
    Int32Literal,
    UInt32Literal,
    Int64Literal,
    UInt64Literal,
    Float32Literal,
    Float64Literal,
    CharacterLiteral,
    String,
    PlusToken,
    MinusToken,
    StarToken,
    SlashToken,
    PercentToken,
    EqualsEqualsToken,
    BangEqualsToken,
    LessToken,
    LessOrEqualsToken,
    GreaterToken,
    GreaterOrEqualsToken,
    BangToken,
    AmpersandAmpersandToken,
    PipePipeToken,
    TildeToken,
    AmpersandToken,
    PipeToken,
    CaretToken,
    LessLessToken,
    GreaterGreaterToken,
    Dot,
    Comma,
    Colon,
    EqualsToken,
    PlusEqualsToken,
    MinusEqualsToken,
    StarEqualsToken,
    SlashEqualsToken,
    PercentEqualsToken,
    AmpersandEqualsToken,
    PipeEqualsToken,
    CaretEqualsToken,
    LessLessEqualsToken,
    GreaterGreaterEqualsToken,
    LeftBrace,
    RightBrace,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    Semicolon,
    EndOfFile,
    BadToken
  }

  internal static class SyntaxFacts
  {
    public static int GetUnaryOperatorPrecedence(SyntaxKind kind)
    {
      switch (kind)
      {
        case SyntaxKind.PlusToken:
        case SyntaxKind.MinusToken:
        case SyntaxKind.BangToken:
        case SyntaxKind.TildeToken:
          return 12;

        default:
          return 0;
      }
    }

    public static int GetBinaryOperatorPrecedence(SyntaxKind kind)
    {
      switch (kind)
      {
        case SyntaxKind.StarToken:
        case SyntaxKind.SlashToken:
        case SyntaxKind.PercentToken:
          return 11;

        case SyntaxKind.PlusToken:
        case SyntaxKind.MinusToken:
          return 10;

        case SyntaxKind.LessLessToken:
        case SyntaxKind.GreaterGreaterToken:
          return 9;

        case SyntaxKind.LessToken:
        case SyntaxKind.LessOrEqualsToken:
        case SyntaxKind.GreaterToken:
        case SyntaxKind.GreaterOrEqualsToken:
          return 8;

        case SyntaxKind.EqualsEqualsToken:
        case SyntaxKind.BangEqualsToken:
          return 7;

        case SyntaxKind.AmpersandToken:
          return 6;

        case SyntaxKind.CaretToken:
          return 5;

        case SyntaxKind.PipeToken:
          return 4;

        case SyntaxKind.AmpersandAmpersandToken:
          return 3;

        case SyntaxKind.PipePipeToken:
          return 2;

        case SyntaxKind.EqualsToken:
        case SyntaxKind.PlusEqualsToken:
        case SyntaxKind.MinusEqualsToken:
        case SyntaxKind.StarEqualsToken:
        case SyntaxKind.SlashEqualsToken:
        case SyntaxKind.PercentEqualsToken:
        case SyntaxKind.AmpersandEqualsToken:
        case SyntaxKind.PipeEqualsToken:
        case SyntaxKind.CaretEqualsToken:
        case SyntaxKind.LessLessEqualsToken:
        case SyntaxKind.GreaterGreaterEqualsToken:
          return 1;

        default:
          return 0;
      }
    }

    public static bool IsAssignmentOperator(SyntaxKind kind)
    {
      switch (kind)
      {
        case SyntaxKind.EqualsToken:
        case SyntaxKind.PlusEqualsToken:
        case SyntaxKind.MinusEqualsToken:
        case SyntaxKind.StarEqualsToken:
        case SyntaxKind.SlashEqualsToken:
        case SyntaxKind.PercentEqualsToken:
        case SyntaxKind.AmpersandEqualsToken:
        case SyntaxKind.PipeEqualsToken:
        case SyntaxKind.CaretEqualsToken:
        case SyntaxKind.LessLessEqualsToken:
        case SyntaxKind.GreaterGreaterEqualsToken:
          return true;

        default:
          return false;
      }
    }

    public static bool IsRightAssociative(SyntaxKind kind)
    {
      return IsAssignmentOperator(kind);
    }
  }
}
