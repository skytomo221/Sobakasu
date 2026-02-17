using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{
  // =========================
  // Source location
  // =========================
  public readonly struct TextSpan
  {
    public readonly int Start;
    public readonly int Length;
    public readonly int Line;   // 1-based
    public readonly int Column; // 1-based

    public int End => Start + Length;

    public TextSpan(int start, int length, int line, int column)
    {
      Start = start;
      Length = length;
      Line = line;
      Column = column;
    }

    public override string ToString() => $"(Ln {Line}, Col {Column}, Start {Start}, Len {Length})";
  }

  // =========================
  // Diagnostics
  // =========================
  public enum DiagnosticSeverity
  {
    Info,
    Warning,
    Error,
  }

  public readonly struct Diagnostic
  {
    public readonly DiagnosticSeverity Severity;
    public readonly TextSpan Span;
    public readonly string Message;

    public Diagnostic(DiagnosticSeverity severity, TextSpan span, string message)
    {
      Severity = severity;
      Span = span;
      Message = message;
    }

    public override string ToString() => $"{Severity}: {Message} @ {Span}";
  }

  public sealed class DiagnosticBag
  {
    private readonly List<Diagnostic> _list = new();

    public void ReportError(TextSpan span, string message)
        => _list.Add(new Diagnostic(DiagnosticSeverity.Error, span, message));

    public void ReportWarning(TextSpan span, string message)
        => _list.Add(new Diagnostic(DiagnosticSeverity.Warning, span, message));

    public IReadOnlyList<Diagnostic> Diagnostics => _list;
    public bool HasErrors
    {
      get
      {
        foreach (var d in _list)
          if (d.Severity == DiagnosticSeverity.Error) return true;
        return false;
      }
    }
  }

  // =========================
  // Tokens
  // =========================
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

  // =========================
  // Lexer
  // =========================
  public sealed class Lexer
  {
    private readonly string _text;
    private readonly DiagnosticBag _diagnostics;

    private int _pos;          // char index
    private int _line = 1;     // 1-based
    private int _col = 1;      // 1-based

    public Lexer(string text, DiagnosticBag diagnostics)
    {
      _text = text ?? "";
      _diagnostics = diagnostics;
    }

    private char Current => _pos >= _text.Length ? '\0' : _text[_pos];
    private char Peek(int offset)
    {
      int i = _pos + offset;
      return i >= _text.Length ? '\0' : _text[i];
    }

    private void Advance()
    {
      if (Current == '\0') return;

      if (Current == '\n')
      {
        _pos++;
        _line++;
        _col = 1;
        return;
      }

      _pos++;
      _col++;
    }

    private TextSpan MakeSpan(int startPos, int startLine, int startCol, int length)
        => new TextSpan(startPos, length, startLine, startCol);

    public Token NextToken()
    {
      // Skip whitespace (including newlines)
      while (char.IsWhiteSpace(Current))
        Advance();

      int startPos = _pos;
      int startLine = _line;
      int startCol = _col;

      if (Current == '\0')
        return new Token(TokenKind.EOF, "", null, MakeSpan(startPos, startLine, startCol, 0));

      // Identifiers / keywords
      if (IsIdentStart(Current))
      {
        while (IsIdentPart(Current))
          Advance();

        var text = _text.Substring(startPos, _pos - startPos);
        var kind = LookupKeyword(text);
        return new Token(kind, text, null, MakeSpan(startPos, startLine, startCol, text.Length));
      }

      // Number (int only for now)
      if (char.IsDigit(Current))
      {
        while (char.IsDigit(Current))
          Advance();

        var text = _text.Substring(startPos, _pos - startPos);
        if (!int.TryParse(text, out var value))
        {
          _diagnostics.ReportError(MakeSpan(startPos, startLine, startCol, text.Length),
              $"数値が大きすぎます: {text}");
          return new Token(TokenKind.Number, text, 0, MakeSpan(startPos, startLine, startCol, text.Length));
        }

        return new Token(TokenKind.Number, text, value, MakeSpan(startPos, startLine, startCol, text.Length));
      }

      // String (double quotes)
      if (Current == '"')
      {
        Advance(); // consume opening quote
        while (Current != '\0' && Current != '"' && Current != '\n')
          Advance();

        if (Current != '"')
        {
          var span = MakeSpan(startPos, startLine, startCol, Math.Max(1, _pos - startPos));
          _diagnostics.ReportError(span, "文字列リテラルが閉じていません");
          // Return a BadToken so parser can continue
          return new Token(TokenKind.BadToken, _text.Substring(startPos, _pos - startPos), null, span);
        }

        Advance(); // consume closing quote

        var fullText = _text.Substring(startPos, _pos - startPos);
        var valueText = fullText.Length >= 2 ? fullText.Substring(1, fullText.Length - 2) : "";
        return new Token(TokenKind.String, fullText, valueText, MakeSpan(startPos, startLine, startCol, fullText.Length));
      }

      // Two-char operators
      switch (Current)
      {
        case '=' when Peek(1) == '=':
          Advance(); Advance();
          return new Token(TokenKind.EqualEqual, "==", null, MakeSpan(startPos, startLine, startCol, 2));
        case '!' when Peek(1) == '=':
          Advance(); Advance();
          return new Token(TokenKind.BangEqual, "!=", null, MakeSpan(startPos, startLine, startCol, 2));
        case '<' when Peek(1) == '=':
          Advance(); Advance();
          return new Token(TokenKind.LessEqual, "<=", null, MakeSpan(startPos, startLine, startCol, 2));
        case '>' when Peek(1) == '=':
          Advance(); Advance();
          return new Token(TokenKind.GreaterEqual, ">=", null, MakeSpan(startPos, startLine, startCol, 2));
        case '&' when Peek(1) == '&':
          Advance(); Advance();
          return new Token(TokenKind.AndAnd, "&&", null, MakeSpan(startPos, startLine, startCol, 2));
        case '|' when Peek(1) == '|':
          Advance(); Advance();
          return new Token(TokenKind.OrOr, "||", null, MakeSpan(startPos, startLine, startCol, 2));
      }

      // Single-char tokens
      char c = Current;
      Advance();

      return c switch
      {
        '(' => new Token(TokenKind.LParen, "(", null, MakeSpan(startPos, startLine, startCol, 1)),
        ')' => new Token(TokenKind.RParen, ")", null, MakeSpan(startPos, startLine, startCol, 1)),
        '{' => new Token(TokenKind.LBrace, "{", null, MakeSpan(startPos, startLine, startCol, 1)),
        '}' => new Token(TokenKind.RBrace, "}", null, MakeSpan(startPos, startLine, startCol, 1)),
        ',' => new Token(TokenKind.Comma, ",", null, MakeSpan(startPos, startLine, startCol, 1)),
        ':' => new Token(TokenKind.Colon, ":", null, MakeSpan(startPos, startLine, startCol, 1)),
        ';' => new Token(TokenKind.Semicolon, ";", null, MakeSpan(startPos, startLine, startCol, 1)),
        '.' => new Token(TokenKind.Dot, ".", null, MakeSpan(startPos, startLine, startCol, 1)),

        '+' => new Token(TokenKind.Plus, "+", null, MakeSpan(startPos, startLine, startCol, 1)),
        '-' => new Token(TokenKind.Minus, "-", null, MakeSpan(startPos, startLine, startCol, 1)),
        '*' => new Token(TokenKind.Star, "*", null, MakeSpan(startPos, startLine, startCol, 1)),
        '/' => new Token(TokenKind.Slash, "/", null, MakeSpan(startPos, startLine, startCol, 1)),
        '!' => new Token(TokenKind.Bang, "!", null, MakeSpan(startPos, startLine, startCol, 1)),
        '=' => new Token(TokenKind.Equal, "=", null, MakeSpan(startPos, startLine, startCol, 1)),
        '<' => new Token(TokenKind.Less, "<", null, MakeSpan(startPos, startLine, startCol, 1)),
        '>' => new Token(TokenKind.Greater, ">", null, MakeSpan(startPos, startLine, startCol, 1)),

        _ => Bad(startPos, startLine, startCol, c),
      };
    }

    private Token Bad(int startPos, int startLine, int startCol, char c)
    {
      var span = MakeSpan(startPos, startLine, startCol, 1);
      _diagnostics.ReportError(span, $"不正な文字です: '{c}'");
      return new Token(TokenKind.BadToken, c.ToString(), null, span);
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static TokenKind LookupKeyword(string text) => text switch
    {
      "let" => TokenKind.Let,
      "func" => TokenKind.Func,
      "on" => TokenKind.On,
      "if" => TokenKind.If,
      "else" => TokenKind.Else,
      "while" => TokenKind.While,
      "return" => TokenKind.Return,
      "true" => TokenKind.True,
      "false" => TokenKind.False,
      _ => TokenKind.Identifier
    };
  }
}
