using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Syntax;

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
        {
          tokens.Add(token);
        }
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

      return new SyntaxToken(
          kind,
          Current.Span,
          text: null,
          value: null);
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
      if (Current.Kind == SyntaxKind.String)
        return new StringLiteralExpressionSyntax(NextToken());

      if (Current.Kind == SyntaxKind.Identifier)
        return new NameExpressionSyntax(NextToken());

      Diagnostics.ReportUnexpectedExpression(Current.Span, Current.Kind);

      var bad = NextToken();
      return new NameExpressionSyntax(bad);
    }

    // TODO: EventDeclarationSyntax などと別に対応するかもしれません
    // イベントハンドラについては on Event {} のように () を伴わない呼び出しにするかもしれません
    private CallExpressionSyntax ParseCallExpression(ExpressionSyntax target)
    {
      var leftParen = MatchToken(SyntaxKind.LeftParen);

      var arguments = new List<ExpressionSyntax>();
      // var first = true;

      // while (Current.Kind != SyntaxKind.RightParen &&
      //        Current.Kind != SyntaxKind.EndOfFile)
      // {
      //   if (!first)
      //     MatchToken(SyntaxKind.Comma);

      //   arguments.Add(ParseExpression());
      //   first = false;

      //   if (Current.Kind != SyntaxKind.Comma)
      //     break;
      // }

      // 一旦、引数は1つまでにする
      if (Current.Kind != SyntaxKind.RightParen &&
      Current.Kind != SyntaxKind.EndOfFile)
      {
        arguments.Add(ParseExpression());
      }

      var rightParen = MatchToken(SyntaxKind.RightParen);

      return new CallExpressionSyntax(target, leftParen, arguments, rightParen);
    }

    private ExpressionSyntax ParsePostfixExpression()
    {
      ExpressionSyntax expr = ParsePrimaryExpression();

      while (true)
      {
        if (Current.Kind == SyntaxKind.Dot)
        {
          var dot = NextToken();
          var name = MatchToken(SyntaxKind.Identifier);
          expr = new MemberAccessExpressionSyntax(expr, dot, name);
          continue;
        }

        if (Current.Kind == SyntaxKind.LeftParen)
        {
          expr = ParseCallExpression(expr);
          continue;
        }

        break;
      }

      return expr;
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
        var statement = ParseStatement();
        statements.Add(statement);
      }

      var closeBrace = MatchToken(SyntaxKind.RightBrace);

      return new BlockStatementSyntax(
          openBrace,
          statements,
          closeBrace);
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
