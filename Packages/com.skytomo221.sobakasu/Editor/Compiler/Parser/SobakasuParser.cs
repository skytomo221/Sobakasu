using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  public class SobakasuParser
  {
    private readonly SyntaxToken[] _tokens;
    private int _position;

    public DiagnosticBag Diagnostics { get; } = new();

    public SobakasuParser(SourceText text)
    {
      var lexer = new SobakasuLexer(text);
      var tokens = new List<SyntaxToken>();

      SyntaxToken token;
      do
      {
        token = lexer.Lex();

        if (token.Kind != SyntaxKind.BadToken)
          tokens.Add(token);
      }
      while (token.Kind != SyntaxKind.EndOfFile);

      _tokens = tokens.ToArray();
      Diagnostics.AddRange(lexer.Diagnostics);
    }

    private SyntaxToken Current => Peek(0);

    private SyntaxToken Peek(int offset)
    {
      var index = _position + offset;
      if (index >= _tokens.Length)
        return _tokens[^1];

      return _tokens[index];
    }

    private SyntaxToken NextToken()
    {
      var current = Current;
      _position++;
      return current;
    }

    private SyntaxToken MatchToken(SyntaxKind kind)
    {
      if (Current.Kind == kind)
        return NextToken();

      Diagnostics.ReportUnexpectedToken(Current.Span, Current.Kind, kind);
      return new SyntaxToken(kind, Current.Span, string.Empty);
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
      switch (Current.Kind)
      {
        case SyntaxKind.String:
          return new StringLiteralExpressionSyntax(NextToken());

        case SyntaxKind.Int8Literal:
        case SyntaxKind.UInt8Literal:
        case SyntaxKind.Int16Literal:
        case SyntaxKind.UInt16Literal:
        case SyntaxKind.Int32Literal:
        case SyntaxKind.UInt32Literal:
        case SyntaxKind.Int64Literal:
        case SyntaxKind.UInt64Literal:
          return new IntegerLiteralExpressionSyntax(NextToken());

        case SyntaxKind.Float32Literal:
        case SyntaxKind.Float64Literal:
          return new FloatLiteralExpressionSyntax(NextToken());

        case SyntaxKind.CharacterLiteral:
          return new CharacterLiteralExpressionSyntax(NextToken());

        case SyntaxKind.TrueKeyword:
        case SyntaxKind.FalseKeyword:
          return new BooleanLiteralExpressionSyntax(NextToken());

        case SyntaxKind.NullKeyword:
          return new NullLiteralExpressionSyntax(NextToken());

        case SyntaxKind.LeftBracket:
          return ParseArrayLiteralExpression();

        case SyntaxKind.Identifier:
          return new NameExpressionSyntax(NextToken());

        default:
          Diagnostics.ReportUnexpectedExpression(Current.Span, Current.Kind);
          var bad = NextToken();
          return new NameExpressionSyntax(bad);
      }
    }

    private ArrayLiteralExpressionSyntax ParseArrayLiteralExpression()
    {
      var openBracketToken = MatchToken(SyntaxKind.LeftBracket);
      var elements = new List<ExpressionSyntax>();
      var separators = new List<SyntaxToken>();

      while (Current.Kind != SyntaxKind.RightBracket &&
             Current.Kind != SyntaxKind.EndOfFile)
      {
        elements.Add(ParseExpression());

        if (Current.Kind != SyntaxKind.Comma)
          break;

        separators.Add(NextToken());
      }

      var closeBracketToken = MatchToken(SyntaxKind.RightBracket);
      return new ArrayLiteralExpressionSyntax(openBracketToken, elements, separators, closeBracketToken);
    }

    private CallExpressionSyntax ParseCallExpression(ExpressionSyntax target)
    {
      var leftParen = MatchToken(SyntaxKind.LeftParen);
      var arguments = new List<ExpressionSyntax>();

      if (Current.Kind != SyntaxKind.RightParen &&
          Current.Kind != SyntaxKind.EndOfFile)
      {
        while (true)
        {
          arguments.Add(ParseExpression());

          if (Current.Kind != SyntaxKind.Comma)
            break;

          NextToken();
        }
      }

      var rightParen = MatchToken(SyntaxKind.RightParen);
      return new CallExpressionSyntax(target, leftParen, arguments, rightParen);
    }

    private ExpressionSyntax ParsePostfixExpression()
    {
      ExpressionSyntax expression = ParsePrimaryExpression();

      while (true)
      {
        if (Current.Kind == SyntaxKind.Dot)
        {
          var dot = NextToken();
          var name = MatchToken(SyntaxKind.Identifier);
          expression = new MemberAccessExpressionSyntax(expression, dot, name);
          continue;
        }

        if (Current.Kind == SyntaxKind.LeftParen)
        {
          expression = ParseCallExpression(expression);
          continue;
        }

        break;
      }

      return expression;
    }

    private ExpressionSyntax ParseExpression()
    {
      return ParsePostfixExpression();
    }

    private ExpressionStatementSyntax ParseExpressionStatement()
    {
      var expression = ParseExpression();
      var semicolon = MatchToken(SyntaxKind.Semicolon);
      return new ExpressionStatementSyntax(expression, semicolon);
    }

    private StatementSyntax ParseStatement()
    {
      if (Current.Kind == SyntaxKind.LeftBrace)
        return ParseBlockStatement();

      return ParseExpressionStatement();
    }

    private BlockStatementSyntax ParseBlockStatement()
    {
      var openBrace = MatchToken(SyntaxKind.LeftBrace);
      var statements = new List<StatementSyntax>();

      while (Current.Kind != SyntaxKind.RightBrace &&
             Current.Kind != SyntaxKind.EndOfFile)
      {
        statements.Add(ParseStatement());
      }

      var closeBrace = MatchToken(SyntaxKind.RightBrace);
      return new BlockStatementSyntax(openBrace, statements, closeBrace);
    }

    private EventDeclarationSyntax ParseEventDeclaration()
    {
      var onKeyword = MatchToken(SyntaxKind.On);
      var identifier = MatchToken(SyntaxKind.Identifier);
      var openParenToken = MatchToken(SyntaxKind.LeftParen);
      var closeParenToken = MatchToken(SyntaxKind.RightParen);
      var body = ParseBlockStatement();

      return new EventDeclarationSyntax(
          onKeyword,
          identifier,
          openParenToken,
          closeParenToken,
          body);
    }

    private MemberSyntax ParseMember()
    {
      if (Current.Kind == SyntaxKind.On)
        return ParseEventDeclaration();

      Diagnostics.ReportUnexpectedMember(Current.Span, Current.Kind);

      var badToken = NextToken();
      return new SkippedMemberSyntax(badToken);
    }

    public CompilationUnitSyntax ParseCompilationUnit()
    {
      var members = new List<MemberSyntax>();

      while (Current.Kind != SyntaxKind.EndOfFile)
      {
        var start = _position;
        var member = ParseMember();
        members.Add(member);

        if (_position == start)
          NextToken();
      }

      var eof = MatchToken(SyntaxKind.EndOfFile);
      return new CompilationUnitSyntax(members, eof);
    }
  }
}
