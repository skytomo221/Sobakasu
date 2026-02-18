namespace Skytomo221.Sobakasu.Compiler.Lexer
{
  public enum TokenKind
  {
    // Special
    EOF,
    BadToken,

    // Trivia-less tokens
    Identifier,
    Number,
    String,

    // Punctuators
    LParen, RParen,
    LBrace, RBrace,
    Comma, Colon, Semicolon,
    Dot,

    // Operators
    Plus, Minus, Star, Slash,
    Bang,
    Equal,
    EqualEqual,
    BangEqual,
    Less, LessEqual,
    Greater, GreaterEqual,

    AndAnd,
    OrOr,

    // Keywords (sample)
    Let,
    Func,
    On,
    If,
    Else,
    While,
    Return,
    True,
    False,
  }
}
