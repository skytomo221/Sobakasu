namespace Skytomo221.Sobakasu.Compiler.Lexer
{
  public sealed class Token
  {
    public TokenKind Kind { get; }
    public string Text { get; }
    public object? Value { get; }
    public TextSpan Span { get; }

    public Token(TokenKind kind, string text, object? value, TextSpan span)
    {
      Kind = kind;
      Text = text;
      Value = value;
      Span = span;
    }

    public override string ToString() => $"{Kind} '{Text}'";
  }
}
