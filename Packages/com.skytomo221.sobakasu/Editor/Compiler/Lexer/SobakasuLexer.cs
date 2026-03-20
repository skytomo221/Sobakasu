using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using System;

namespace Skytomo221.Sobakasu.Compiler.Lexer
{
  public class SobakasuLexer
  {
    public SourceText Text { get; }

    public int Position { get; protected set; }

    public char Current => Peek(0);

    public char Lookahead => Peek(1);

    public DiagnosticBag Diagnostics { get; }

    public SobakasuLexer(SourceText text)
    {
      Text = text ?? throw new ArgumentNullException(nameof(text));
      Diagnostics = new DiagnosticBag();
      Position = 0;
    }

    /// <summary>
    /// Returns next token (skips whitespace).
    /// </summary>
    public SyntaxToken Lex()
    {
      if (char.IsWhiteSpace(Current))
        return ReadWhitespace();

      if (Current == '\0')
        return new SyntaxToken(SyntaxKind.EndOfFile, new TextSpan(Position, 0), "");

      if (IsIdentifierStart(Current))
        return ReadIdentifierOrKeyword();

      // if (char.IsDigit(Current))
      //   return ReadNumber();

      if (Current == '"')
        return ProtectedReadString();

      return ReadOperator();
    }

    protected void Next()
    {
      if (Position < Text.Length)
        Position++;
    }

    protected char Peek(int offset)
    {
      int index = Position + offset;
      if (index < 0 || index >= Text.Length)
        return '\0';
      return Text[index];
    }

    protected SyntaxToken ReadIdentifierOrKeyword()
    {
      int start = Position;

      Next();
      while (IsIdentifierPart(Current))
        Next();

      int length = Position - start;
      string text = Slice(start, length);

      if (text == "on")
        return new SyntaxToken(SyntaxKind.On, new TextSpan(start, length), text);

      return new SyntaxToken(SyntaxKind.Identifier, new TextSpan(start, length), text);
    }

  /*
    protected SyntaxToken ReadNumber()
    {
      int start = Position;

      while (char.IsDigit(Current))
        Next();

      int length = Position - start;
      string text = Slice(start, length);

      // SyntaxKind に Number が無いので Identifier として返す（Value を持てるなら int を入れるのが便利）
      if (int.TryParse(text, out int value))
        return MakeToken(SyntaxKind.Identifier, start, length, text, value);

      // 解析不能でもとりあえず Identifier として返す
      return MakeToken(SyntaxKind.Identifier, start, length, text);
    }
  */

    protected SyntaxToken ProtectedReadString()
    {
      int start = Position;
      Next(); // consume opening "

      int contentStart = Position;

      while (true)
      {
        char c = Current;
        if (c == '\0' || c == '\n' || c == '\r')
        {
          Diagnostics.ReportUnterminatedString(new TextSpan(start, Position - start));
          break;
        }

        if (c == '"')
          break;

        // very simple escape handling: consume '\' + next char
        if (c == '\\')
        {
          Next();
          if (Current != '\0')
            Next();
          continue;
        }

        Next();
      }

      int contentEnd = Position;

      if (Current == '"')
        Next(); // consume closing "

      int length = Position - start;
      string rawText = Slice(start, length);
      string value = Slice(contentStart, Math.Max(0, contentEnd - contentStart));

      return new SyntaxToken(SyntaxKind.String, new TextSpan(start, length), rawText, value);
    }

    protected SyntaxToken ReadWhitespace()
    {
      // consume all whitespace then return next token
      while (char.IsWhiteSpace(Current))
        Next();

      return Lex();
    }

    protected SyntaxToken ReadOperator()
    {
      int start = Position;

      switch (Current)
      {
        case '.':
          Next();
          return new SyntaxToken(SyntaxKind.Dot, new TextSpan(start, 1), ".");

        case '{':
          Next();
          return new SyntaxToken(SyntaxKind.LeftBrace, new TextSpan(start, 1), "{");

        case '}':
          Next();
          return new SyntaxToken(SyntaxKind.RightBrace, new TextSpan(start, 1), "}");

        case '(':
          Next();
          return new SyntaxToken(SyntaxKind.LeftParen, new TextSpan(start, 1), "(");

        case ')':
          Next();
          return new SyntaxToken(SyntaxKind.RightParen, new TextSpan(start, 1), ")");

        case ';':
          Next();
          return new SyntaxToken(SyntaxKind.Semicolon, new TextSpan(start, 1), ";");

        default:
          if (Current == '\0')
            return new SyntaxToken(SyntaxKind.EndOfFile, new TextSpan(start, 0), "");

          string bad = Current.ToString();
          Next();

          Diagnostics.ReportBadCharacter(new TextSpan(start, 1), bad[0]);
          return new SyntaxToken(SyntaxKind.BadToken, new TextSpan(start, 1), bad);
      }
    }

    protected bool IsIdentifierStart(char c)
    {
      return c == '_' || char.IsLetter(c);
    }

    protected bool IsIdentifierPart(char c)
    {
      return c == '_' || char.IsLetterOrDigit(c);
    }

    // -----------------------
    // helpers
    // -----------------------

    private string Slice(int start, int length)
    {
      // SourceText の想定: Text プロパティ(string) を持つ
      // もし違うなら、ここだけ差し替えればOK
      return Text.Text.Substring(start, length);
    }
  }
}
