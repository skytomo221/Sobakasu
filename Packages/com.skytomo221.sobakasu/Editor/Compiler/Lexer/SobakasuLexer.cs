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

      if (Current == '\'')
        return ReadCharacterLiteral();

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
        "let" => new SyntaxToken(SyntaxKind.LetKeyword, new TextSpan(start, length), text),
        "mut" => new SyntaxToken(SyntaxKind.MutKeyword, new TextSpan(start, length), text),
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
              var escapeLength = Math.Min(2, Text.Length - escapeStart);
              Diagnostics.ReportInvalidEscapeSequence(
                  new TextSpan(escapeStart, escapeLength),
                  Slice(escapeStart, escapeLength));

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

      return CreateToken(SyntaxKind.String, start, value.ToString());
    }

    protected SyntaxToken ReadCharacterLiteral()
    {
      var start = Position;

      Next();

      if (Current == '\0' || Current == '\n' || Current == '\r')
      {
        Diagnostics.ReportUnterminatedCharacterLiteral(new TextSpan(start, Position - start));
        return CreateToken(SyntaxKind.CharacterLiteral, start);
      }

      if (Current == '\'')
      {
        Next();
        var emptyLiteralText = Slice(start, Position - start);
        Diagnostics.ReportMalformedCharacterLiteral(
            new TextSpan(start, Position - start),
            emptyLiteralText);
        return new SyntaxToken(
            SyntaxKind.CharacterLiteral,
            new TextSpan(start, Position - start),
            emptyLiteralText);
      }

      char value;
      if (Current == '\\')
      {
        var escapeStart = Position;
        Next();

        var escapeStatus = TryReadCharacterEscape(escapeStart, out value);
        if (escapeStatus == CharacterEscapeStatus.Unterminated)
        {
          Diagnostics.ReportUnterminatedCharacterLiteral(
              new TextSpan(start, Position - start));
          return CreateToken(SyntaxKind.CharacterLiteral, start);
        }

        if (escapeStatus == CharacterEscapeStatus.InvalidEscape)
        {
          RecoverCharacterLiteral();
          return CreateToken(SyntaxKind.CharacterLiteral, start);
        }

        if (escapeStatus == CharacterEscapeStatus.Malformed)
        {
          RecoverCharacterLiteral();
          var malformedText = Slice(start, Position - start);
          Diagnostics.ReportMalformedCharacterLiteral(
              new TextSpan(start, Position - start),
              malformedText);
          return new SyntaxToken(
              SyntaxKind.CharacterLiteral,
              new TextSpan(start, Position - start),
              malformedText);
        }
      }
      else
      {
        value = Current;
        Next();
      }

      if (Current == '\'')
      {
        Next();
        return CreateToken(SyntaxKind.CharacterLiteral, start, value);
      }

      if (Current == '\0' || Current == '\n' || Current == '\r')
      {
        Diagnostics.ReportUnterminatedCharacterLiteral(new TextSpan(start, Position - start));
        return CreateToken(SyntaxKind.CharacterLiteral, start);
      }

      if (!HasCharacterLiteralTerminatorAhead())
      {
        RecoverCharacterLiteral();
        Diagnostics.ReportUnterminatedCharacterLiteral(new TextSpan(start, Position - start));
        return CreateToken(SyntaxKind.CharacterLiteral, start);
      }

      RecoverCharacterLiteral();
      var malformedLiteralText = Slice(start, Position - start);
      Diagnostics.ReportMalformedCharacterLiteral(
          new TextSpan(start, Position - start),
          malformedLiteralText);
      return new SyntaxToken(
          SyntaxKind.CharacterLiteral,
          new TextSpan(start, Position - start),
          malformedLiteralText);
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

        case ':':
          Next();
          return new SyntaxToken(SyntaxKind.Colon, new TextSpan(start, 1), ":");

        case '=':
          Next();
          return new SyntaxToken(SyntaxKind.Equals, new TextSpan(start, 1), "=");

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
      if (numberBase == 16 &&
          validDigits &&
          TrySplitTrailingHexFloatSuffix(digits, out var trimmedDigits, out var inferredSuffix))
      {
        digits = trimmedDigits;
        validDigits = digits.Length > 0;
        Position -= GetSuffixLength(inferredSuffix);
      }

      var suffix = ReadNumberSuffix();

      if (!validDigits || IsFloatSuffix(suffix))
      {
        Diagnostics.ReportInvalidNumericLiteral(
            new TextSpan(start, Position - start),
            Slice(start, Position - start));
        return CreateToken(GetLiteralKindForSuffix(suffix, false), start);
      }

      if (!TryParseUnsignedInteger(digits, numberBase, out var unsignedValue) ||
          !TryConvertIntegerLiteralValue(unsignedValue, suffix, out var kind, out var value))
      {
        Diagnostics.ReportInvalidNumericLiteral(
            new TextSpan(start, Position - start),
            Slice(start, Position - start));
        return CreateToken(GetLiteralKindForSuffix(suffix, false), start);
      }

      return CreateToken(kind, start, value);
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
      var separatorsValid =
          integerSeparatorsValid &&
          fractionSeparatorsValid &&
          exponentSeparatorsValid;
      var isFloat = hasFraction || hasExponent || IsFloatSuffix(suffix);

      if (isFloat)
      {
        if (IsIntegerSuffix(suffix) || !separatorsValid)
        {
          Diagnostics.ReportInvalidNumericLiteral(
              new TextSpan(start, Position - start),
              Slice(start, Position - start));
          return CreateToken(GetLiteralKindForSuffix(suffix, true), start);
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

        if (!TryConvertFloatLiteralValue(normalized.ToString(), suffix, out var kind, out var value))
        {
          Diagnostics.ReportInvalidNumericLiteral(
              new TextSpan(start, Position - start),
              Slice(start, Position - start));
          return CreateToken(GetLiteralKindForSuffix(suffix, true), start);
        }

        return CreateToken(kind, start, value);
      }

      if (!separatorsValid ||
          !TryParseUnsignedInteger(integerDigits, 10, out var integerValue) ||
          !TryConvertIntegerLiteralValue(integerValue, suffix, out var integerKind, out var integerLiteralValue))
      {
        Diagnostics.ReportInvalidNumericLiteral(
            new TextSpan(start, Position - start),
            Slice(start, Position - start));
        return CreateToken(GetLiteralKindForSuffix(suffix, false), start);
      }

      return CreateToken(integerKind, start, integerLiteralValue);
    }

    private CharacterEscapeStatus TryReadCharacterEscape(int escapeStart, out char value)
    {
      value = '\0';

      switch (Current)
      {
        case '\'':
          value = '\'';
          Next();
          return CharacterEscapeStatus.Success;

        case '"':
          value = '"';
          Next();
          return CharacterEscapeStatus.Success;

        case '\\':
          value = '\\';
          Next();
          return CharacterEscapeStatus.Success;

        case '0':
          value = '\0';
          Next();
          return CharacterEscapeStatus.Success;

        case 'a':
          value = '\a';
          Next();
          return CharacterEscapeStatus.Success;

        case 'b':
          value = '\b';
          Next();
          return CharacterEscapeStatus.Success;

        case 'f':
          value = '\f';
          Next();
          return CharacterEscapeStatus.Success;

        case 'n':
          value = '\n';
          Next();
          return CharacterEscapeStatus.Success;

        case 'r':
          value = '\r';
          Next();
          return CharacterEscapeStatus.Success;

        case 't':
          value = '\t';
          Next();
          return CharacterEscapeStatus.Success;

        case 'v':
          value = '\v';
          Next();
          return CharacterEscapeStatus.Success;

        case 'u':
          return TryReadUnicodeEscape(out value);

        case 'x':
          return TryReadHexEscape(out value);

        case '\0':
        case '\n':
        case '\r':
          return CharacterEscapeStatus.Unterminated;

        default:
          var escapeLength = Math.Min(2, Text.Length - escapeStart);
          Diagnostics.ReportInvalidEscapeSequence(
              new TextSpan(escapeStart, escapeLength),
              Slice(escapeStart, escapeLength));

          if (Current != '\0')
            Next();

          return CharacterEscapeStatus.InvalidEscape;
      }
    }

    private CharacterEscapeStatus TryReadUnicodeEscape(out char value)
    {
      value = '\0';

      Next();

      var codeUnit = 0;
      for (var index = 0; index < 4; index++)
      {
        if (!IsHexDigit(Current))
          return CharacterEscapeStatus.Malformed;

        codeUnit = (codeUnit * 16) + GetDigitValue(Current);
        Next();
      }

      value = (char)codeUnit;
      return CharacterEscapeStatus.Success;
    }

    private CharacterEscapeStatus TryReadHexEscape(out char value)
    {
      value = '\0';

      Next();
      if (!IsHexDigit(Current))
        return CharacterEscapeStatus.Malformed;

      var codeUnit = 0;
      var digitCount = 0;
      while (digitCount < 4 && IsHexDigit(Current))
      {
        codeUnit = (codeUnit * 16) + GetDigitValue(Current);
        digitCount++;
        Next();
      }

      value = (char)codeUnit;
      return CharacterEscapeStatus.Success;
    }

    private void RecoverCharacterLiteral()
    {
      while (Current != '\0' &&
             Current != '\n' &&
             Current != '\r' &&
             Current != '\'')
      {
        Next();
      }

      if (Current == '\'')
        Next();
    }

    private bool HasCharacterLiteralTerminatorAhead()
    {
      var offset = 0;
      while (true)
      {
        var next = Peek(offset);
        if (next == '\'')
          return true;

        if (next == '\0' || next == '\n' || next == '\r')
          return false;

        offset++;
      }
    }

    private bool TrySplitTrailingHexFloatSuffix(
        string digits,
        out string trimmedDigits,
        out NumberSuffix suffix)
    {
      trimmedDigits = digits;
      suffix = NumberSuffix.None;

      if (digits.Length <= 3)
        return false;

      if (digits.EndsWith("f32", StringComparison.Ordinal))
      {
        trimmedDigits = digits.Substring(0, digits.Length - 3);
        suffix = NumberSuffix.Float32;
      }
      else if (digits.EndsWith("f64", StringComparison.Ordinal))
      {
        trimmedDigits = digits.Substring(0, digits.Length - 3);
        suffix = NumberSuffix.Float64;
      }
      else
      {
        return false;
      }

      return trimmedDigits.Length > 0;
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
      if (MatchesSuffix("i8"))
      {
        Position += 2;
        return NumberSuffix.Int8;
      }

      if (MatchesSuffix("u8"))
      {
        Position += 2;
        return NumberSuffix.UInt8;
      }

      if (MatchesSuffix("i16"))
      {
        Position += 3;
        return NumberSuffix.Int16;
      }

      if (MatchesSuffix("u16"))
      {
        Position += 3;
        return NumberSuffix.UInt16;
      }

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

      if (MatchesSuffix("i64"))
      {
        Position += 3;
        return NumberSuffix.Int64;
      }

      if (MatchesSuffix("u64"))
      {
        Position += 3;
        return NumberSuffix.UInt64;
      }

      if (MatchesSuffix("f32"))
      {
        Position += 3;
        return NumberSuffix.Float32;
      }

      if (MatchesSuffix("f64"))
      {
        Position += 3;
        return NumberSuffix.Float64;
      }

      return NumberSuffix.None;
    }

    private int GetSuffixLength(NumberSuffix suffix)
    {
      return suffix switch
      {
        NumberSuffix.Int8 => 2,
        NumberSuffix.UInt8 => 2,
        NumberSuffix.Int16 => 3,
        NumberSuffix.UInt16 => 3,
        NumberSuffix.Int32 => 3,
        NumberSuffix.UInt32 => 3,
        NumberSuffix.Int64 => 3,
        NumberSuffix.UInt64 => 3,
        NumberSuffix.Float32 => 3,
        NumberSuffix.Float64 => 3,
        _ => 0
      };
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

    private static bool TryConvertIntegerLiteralValue(
        ulong unsignedValue,
        NumberSuffix suffix,
        out SyntaxKind kind,
        out object value)
    {
      value = null;
      var normalizedSuffix = suffix == NumberSuffix.None
          ? NumberSuffix.Int32
          : suffix;

      kind = GetLiteralKindForIntegerSuffix(normalizedSuffix);

      switch (normalizedSuffix)
      {
        case NumberSuffix.Int8:
          if (unsignedValue <= (ulong)sbyte.MaxValue)
          {
            value = (sbyte)unsignedValue;
            return true;
          }

          return false;

        case NumberSuffix.UInt8:
          if (unsignedValue <= byte.MaxValue)
          {
            value = (byte)unsignedValue;
            return true;
          }

          return false;

        case NumberSuffix.Int16:
          if (unsignedValue <= (ulong)short.MaxValue)
          {
            value = (short)unsignedValue;
            return true;
          }

          return false;

        case NumberSuffix.UInt16:
          if (unsignedValue <= ushort.MaxValue)
          {
            value = (ushort)unsignedValue;
            return true;
          }

          return false;

        case NumberSuffix.Int32:
          if (unsignedValue <= int.MaxValue)
          {
            value = (int)unsignedValue;
            return true;
          }

          return false;

        case NumberSuffix.UInt32:
          if (unsignedValue <= uint.MaxValue)
          {
            value = (uint)unsignedValue;
            return true;
          }

          return false;

        case NumberSuffix.Int64:
          if (unsignedValue <= long.MaxValue)
          {
            value = (long)unsignedValue;
            return true;
          }

          return false;

        case NumberSuffix.UInt64:
          value = unsignedValue;
          return true;

        default:
          return false;
      }
    }

    private static bool TryConvertFloatLiteralValue(
        string normalizedText,
        NumberSuffix suffix,
        out SyntaxKind kind,
        out object value)
    {
      value = null;
      var normalizedSuffix = suffix == NumberSuffix.None
          ? NumberSuffix.Float32
          : suffix;
      kind = GetLiteralKindForFloatSuffix(normalizedSuffix);

      switch (normalizedSuffix)
      {
        case NumberSuffix.Float32:
          if (float.TryParse(
                  normalizedText,
                  NumberStyles.Float,
                  CultureInfo.InvariantCulture,
                  out var floatValue) &&
              !float.IsInfinity(floatValue) &&
              !float.IsNaN(floatValue))
          {
            value = floatValue;
            return true;
          }

          return false;

        case NumberSuffix.Float64:
          if (double.TryParse(
                  normalizedText,
                  NumberStyles.Float,
                  CultureInfo.InvariantCulture,
                  out var doubleValue) &&
              !double.IsInfinity(doubleValue) &&
              !double.IsNaN(doubleValue))
          {
            value = doubleValue;
            return true;
          }

          return false;

        default:
          return false;
      }
    }

    private static bool IsIntegerSuffix(NumberSuffix suffix)
    {
      return suffix is NumberSuffix.Int8 or
          NumberSuffix.UInt8 or
          NumberSuffix.Int16 or
          NumberSuffix.UInt16 or
          NumberSuffix.Int32 or
          NumberSuffix.UInt32 or
          NumberSuffix.Int64 or
          NumberSuffix.UInt64;
    }

    private static bool IsFloatSuffix(NumberSuffix suffix)
    {
      return suffix is NumberSuffix.Float32 or NumberSuffix.Float64;
    }

    private static SyntaxKind GetLiteralKindForSuffix(NumberSuffix suffix, bool isFloatContext)
    {
      if (isFloatContext || IsFloatSuffix(suffix))
        return GetLiteralKindForFloatSuffix(suffix == NumberSuffix.None ? NumberSuffix.Float32 : suffix);

      return GetLiteralKindForIntegerSuffix(suffix == NumberSuffix.None ? NumberSuffix.Int32 : suffix);
    }

    private static SyntaxKind GetLiteralKindForIntegerSuffix(NumberSuffix suffix)
    {
      return suffix switch
      {
        NumberSuffix.Int8 => SyntaxKind.Int8Literal,
        NumberSuffix.UInt8 => SyntaxKind.UInt8Literal,
        NumberSuffix.Int16 => SyntaxKind.Int16Literal,
        NumberSuffix.UInt16 => SyntaxKind.UInt16Literal,
        NumberSuffix.Int32 => SyntaxKind.Int32Literal,
        NumberSuffix.UInt32 => SyntaxKind.UInt32Literal,
        NumberSuffix.Int64 => SyntaxKind.Int64Literal,
        NumberSuffix.UInt64 => SyntaxKind.UInt64Literal,
        _ => SyntaxKind.Int32Literal
      };
    }

    private static SyntaxKind GetLiteralKindForFloatSuffix(NumberSuffix suffix)
    {
      return suffix == NumberSuffix.Float64
          ? SyntaxKind.Float64Literal
          : SyntaxKind.Float32Literal;
    }

    private static Func<char, bool> IsDigitForBase(int numberBase)
    {
      return c => numberBase switch
      {
        2 => c == '0' || c == '1',
        8 => c >= '0' && c <= '7',
        16 => IsHexDigit(c),
        _ => char.IsDigit(c)
      };
    }

    private static bool IsHexDigit(char c)
    {
      return GetDigitValue(c) is >= 0 and < 16;
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

    private SyntaxToken CreateToken(SyntaxKind kind, int start, object value = null)
    {
      var length = Position - start;
      var text = Slice(start, length);
      return new SyntaxToken(kind, new TextSpan(start, length), text, value);
    }

    private string Slice(int start, int length)
    {
      return Text.Text.Substring(start, length);
    }

    private enum NumberSuffix
    {
      None,
      Int8,
      UInt8,
      Int16,
      UInt16,
      Int32,
      UInt32,
      Int64,
      UInt64,
      Float32,
      Float64
    }

    private enum CharacterEscapeStatus
    {
      Success,
      InvalidEscape,
      Unterminated,
      Malformed
    }
  }
}
