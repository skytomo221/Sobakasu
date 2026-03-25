using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Globalization;
using System.Text;

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

    public SyntaxToken Lex()
    {
      if (char.IsWhiteSpace(Current))
        return ReadWhitespace();

      if (Current == '\0')
        return new SyntaxToken(SyntaxKind.EndOfFile, new TextSpan(Position, 0), "");

      if (IsIdentifierStart(Current))
        return ReadIdentifierOrKeyword();

      if (char.IsDigit(Current))
        return ReadNumber();

      if (Current == '"')
        return ReadString();

      return ReadOperator();
    }

    protected void Next()
    {
      if (Position < Text.Length)
        Position++;
    }

    protected char Peek(int offset)
    {
      var index = Position + offset;
      if (index < 0 || index >= Text.Length)
        return '\0';

      return Text[index];
    }

    protected SyntaxToken ReadIdentifierOrKeyword()
    {
      var start = Position;

      Next();
      while (IsIdentifierPart(Current))
        Next();

      var length = Position - start;
      var text = Slice(start, length);

      return text switch
      {
        "on" => new SyntaxToken(SyntaxKind.On, new TextSpan(start, length), text),
        "true" => new SyntaxToken(SyntaxKind.TrueKeyword, new TextSpan(start, length), text, true),
        "false" => new SyntaxToken(SyntaxKind.FalseKeyword, new TextSpan(start, length), text, false),
        "null" => new SyntaxToken(SyntaxKind.NullKeyword, new TextSpan(start, length), text),
        "u0" => new SyntaxToken(SyntaxKind.U0Keyword, new TextSpan(start, length), text),
        _ => new SyntaxToken(SyntaxKind.Identifier, new TextSpan(start, length), text)
      };
    }

    protected SyntaxToken ReadNumber()
    {
      if (Current == '0' && (Lookahead == 'b' || Lookahead == 'B' ||
                             Lookahead == 'o' || Lookahead == 'O' ||
                             Lookahead == 'x' || Lookahead == 'X'))
      {
        return ReadRadixIntegerLiteral();
      }

      return ReadDecimalOrFloatLiteral();
    }

    protected SyntaxToken ReadString()
    {
      var start = Position;
      var value = new StringBuilder();

      Next();

      while (true)
      {
        var current = Current;
        if (current == '\0' || current == '\n' || current == '\r')
        {
          Diagnostics.ReportUnterminatedString(new TextSpan(start, Position - start));
          break;
        }

        if (current == '"')
          break;

        if (current == '\\')
        {
          var escapeStart = Position;
          Next();

          switch (Current)
          {
            case '"':
              value.Append('"');
              Next();
              break;

            case '\\':
              value.Append('\\');
              Next();
              break;

            case 'n':
              value.Append('\n');
              Next();
              break;

            case 'r':
              value.Append('\r');
              Next();
              break;

            case 't':
              value.Append('\t');
              Next();
              break;

            case '\0':
            case '\n':
            case '\r':
              Diagnostics.ReportUnterminatedString(new TextSpan(start, Position - start));
              break;

            default:
                var escapeLength = Current == '\0' ? 1 : 2;
                Diagnostics.ReportInvalidEscapeSequence(
                    new TextSpan(escapeStart, escapeLength),
                    Slice(escapeStart, Math.Min(escapeLength, Text.Length - escapeStart)));

                if (Current != '\0')
                {
                  value.Append(Current);
                  Next();
                }
                break;
          }

          if (Current == '\0' || Current == '\n' || Current == '\r')
            break;

          continue;
        }

        value.Append(current);
        Next();
      }

      if (Current == '"')
        Next();

      var length = Position - start;
      var rawText = Slice(start, length);
      return new SyntaxToken(SyntaxKind.String, new TextSpan(start, length), rawText, value.ToString());
    }

    protected SyntaxToken ReadWhitespace()
    {
      while (char.IsWhiteSpace(Current))
        Next();

      return Lex();
    }

    protected SyntaxToken ReadOperator()
    {
      var start = Position;

      switch (Current)
      {
        case '.':
          Next();
          return new SyntaxToken(SyntaxKind.Dot, new TextSpan(start, 1), ".");

        case ',':
          Next();
          return new SyntaxToken(SyntaxKind.Comma, new TextSpan(start, 1), ",");

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

        case '[':
          Next();
          return new SyntaxToken(SyntaxKind.LeftBracket, new TextSpan(start, 1), "[");

        case ']':
          Next();
          return new SyntaxToken(SyntaxKind.RightBracket, new TextSpan(start, 1), "]");

        case ';':
          Next();
          return new SyntaxToken(SyntaxKind.Semicolon, new TextSpan(start, 1), ";");

        default:
          if (Current == '\0')
            return new SyntaxToken(SyntaxKind.EndOfFile, new TextSpan(start, 0), "");

          var bad = Current.ToString();
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

    private SyntaxToken ReadRadixIntegerLiteral()
    {
      var start = Position;
      var numberBase = Current switch
      {
        '0' when Lookahead == 'b' || Lookahead == 'B' => 2,
        '0' when Lookahead == 'o' || Lookahead == 'O' => 8,
        _ => 16
      };

      Next();
      Next();

      var validDigits = ReadDigitSequence(IsDigitForBase(numberBase), out var digits);
      var suffix = ReadNumberSuffix();
      var text = Slice(start, Position - start);
      var kind = suffix switch
      {
        NumberSuffix.UInt32 => SyntaxKind.UInt32Literal,
        NumberSuffix.Float32 => SyntaxKind.Float32Literal,
        _ => SyntaxKind.Int32Literal
      };

      if (!validDigits || suffix == NumberSuffix.Float32)
      {
        Diagnostics.ReportInvalidNumericLiteral(new TextSpan(start, Position - start), text);
        return new SyntaxToken(kind, new TextSpan(start, Position - start), text);
      }

      if (!TryParseUnsignedInteger(digits, numberBase, out var unsignedValue))
      {
        Diagnostics.ReportInvalidNumericLiteral(new TextSpan(start, Position - start), text);
        return new SyntaxToken(kind, new TextSpan(start, Position - start), text);
      }

      if (suffix == NumberSuffix.UInt32)
      {
        if (unsignedValue > uint.MaxValue)
        {
          Diagnostics.ReportInvalidNumericLiteral(new TextSpan(start, Position - start), text);
          return new SyntaxToken(kind, new TextSpan(start, Position - start), text);
        }

        return new SyntaxToken(kind, new TextSpan(start, Position - start), text, (uint)unsignedValue);
      }

      if (unsignedValue > int.MaxValue)
      {
        Diagnostics.ReportInvalidNumericLiteral(new TextSpan(start, Position - start), text);
        return new SyntaxToken(kind, new TextSpan(start, Position - start), text);
      }

      return new SyntaxToken(kind, new TextSpan(start, Position - start), text, (int)unsignedValue);
    }

    private SyntaxToken ReadDecimalOrFloatLiteral()
    {
      var start = Position;

      var integerSeparatorsValid = ReadDigitSequence(char.IsDigit, out var integerDigits);
      var hasFraction = false;
      var fractionSeparatorsValid = true;
      var fractionDigits = string.Empty;

      if (Current == '.' && char.IsDigit(Lookahead))
      {
        hasFraction = true;
        Next();
        fractionSeparatorsValid = ReadDigitSequence(char.IsDigit, out fractionDigits);
      }

      var hasExponent = false;
      var exponentSeparatorsValid = true;
      var exponentSign = '\0';
      var exponentDigits = string.Empty;

      if ((Current == 'e' || Current == 'E') && IsExponentStart())
      {
        hasExponent = true;
        Next();

        if (Current == '+' || Current == '-')
        {
          exponentSign = Current;
          Next();
        }

        exponentSeparatorsValid = ReadDigitSequence(char.IsDigit, out exponentDigits);
      }

      var suffix = ReadNumberSuffix();
      var text = Slice(start, Position - start);
      var isFloat = hasFraction || hasExponent || suffix == NumberSuffix.Float32;
      var kind = isFloat
          ? SyntaxKind.Float32Literal
          : suffix == NumberSuffix.UInt32
              ? SyntaxKind.UInt32Literal
              : SyntaxKind.Int32Literal;

      var separatorsValid = integerSeparatorsValid && fractionSeparatorsValid && exponentSeparatorsValid;

      if (isFloat)
      {
        if (suffix == NumberSuffix.Int32 || suffix == NumberSuffix.UInt32 || !separatorsValid)
        {
          Diagnostics.ReportInvalidNumericLiteral(new TextSpan(start, Position - start), text);
          return new SyntaxToken(kind, new TextSpan(start, Position - start), text);
        }

        var normalized = new StringBuilder(integerDigits);
        if (hasFraction)
        {
          normalized.Append('.');
          normalized.Append(fractionDigits);
        }

        if (hasExponent)
        {
          normalized.Append('e');
          if (exponentSign != '\0')
            normalized.Append(exponentSign);

          normalized.Append(exponentDigits);
        }

        if (!float.TryParse(
                normalized.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var floatValue) ||
            float.IsInfinity(floatValue) ||
            float.IsNaN(floatValue))
        {
          Diagnostics.ReportInvalidNumericLiteral(new TextSpan(start, Position - start), text);
          return new SyntaxToken(kind, new TextSpan(start, Position - start), text);
        }

        return new SyntaxToken(kind, new TextSpan(start, Position - start), text, floatValue);
      }

      if (!separatorsValid || !TryParseUnsignedInteger(integerDigits, 10, out var integerValue))
      {
        Diagnostics.ReportInvalidNumericLiteral(new TextSpan(start, Position - start), text);
        return new SyntaxToken(kind, new TextSpan(start, Position - start), text);
      }

      if (suffix == NumberSuffix.UInt32)
      {
        if (integerValue > uint.MaxValue)
        {
          Diagnostics.ReportInvalidNumericLiteral(new TextSpan(start, Position - start), text);
          return new SyntaxToken(kind, new TextSpan(start, Position - start), text);
        }

        return new SyntaxToken(kind, new TextSpan(start, Position - start), text, (uint)integerValue);
      }

      if (integerValue > int.MaxValue)
      {
        Diagnostics.ReportInvalidNumericLiteral(new TextSpan(start, Position - start), text);
        return new SyntaxToken(kind, new TextSpan(start, Position - start), text);
      }

      return new SyntaxToken(kind, new TextSpan(start, Position - start), text, (int)integerValue);
    }

    private bool ReadDigitSequence(Func<char, bool> isDigit, out string digits)
    {
      var builder = new StringBuilder();
      var valid = true;
      var sawDigit = false;

      while (isDigit(Current) || Current == '_')
      {
        if (Current == '_')
        {
          if (!sawDigit || !isDigit(Lookahead))
            valid = false;

          Next();
          continue;
        }

        sawDigit = true;
        builder.Append(Current);
        Next();
      }

      digits = builder.ToString();
      return valid && sawDigit;
    }

    private NumberSuffix ReadNumberSuffix()
    {
      if (MatchesSuffix("i32"))
      {
        Position += 3;
        return NumberSuffix.Int32;
      }

      if (MatchesSuffix("u32"))
      {
        Position += 3;
        return NumberSuffix.UInt32;
      }

      if (MatchesSuffix("f32"))
      {
        Position += 3;
        return NumberSuffix.Float32;
      }

      return NumberSuffix.None;
    }

    private bool MatchesSuffix(string suffix)
    {
      for (var index = 0; index < suffix.Length; index++)
      {
        if (Peek(index) != suffix[index])
          return false;
      }

      return !IsIdentifierPart(Peek(suffix.Length));
    }

    private bool IsExponentStart()
    {
      var index = 1;
      if (Peek(index) == '+' || Peek(index) == '-')
        index++;

      return char.IsDigit(Peek(index));
    }

    private static Func<char, bool> IsDigitForBase(int numberBase)
    {
      return c => numberBase switch
      {
        2 => c == '0' || c == '1',
        8 => c >= '0' && c <= '7',
        16 => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'),
        _ => char.IsDigit(c)
      };
    }

    private static bool TryParseUnsignedInteger(string digits, int numberBase, out ulong value)
    {
      value = 0;
      if (string.IsNullOrEmpty(digits))
        return false;

      foreach (var digitChar in digits)
      {
        var digit = GetDigitValue(digitChar);
        if (digit < 0 || digit >= numberBase)
          return false;

        var baseValue = (ulong)numberBase;
        var digitValue = (ulong)digit;
        if (value > (ulong.MaxValue - digitValue) / baseValue)
          return false;

        value = (value * baseValue) + digitValue;
      }

      return true;
    }

    private static int GetDigitValue(char c)
    {
      if (c >= '0' && c <= '9')
        return c - '0';

      if (c >= 'a' && c <= 'f')
        return 10 + (c - 'a');

      if (c >= 'A' && c <= 'F')
        return 10 + (c - 'A');

      return -1;
    }

    private string Slice(int start, int length)
    {
      return Text.Text.Substring(start, length);
    }

    private enum NumberSuffix
    {
      None,
      Int32,
      UInt32,
      Float32
    }
  }
}
