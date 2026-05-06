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

    private QualifiedNameSyntax ParseQualifiedName(out bool isMalformed)
    {
      var identifiers = new List<SyntaxToken>();
      var dotTokens = new List<SyntaxToken>();

      var firstIdentifier = MatchToken(SyntaxKind.Identifier);
      identifiers.Add(firstIdentifier);
      isMalformed = string.IsNullOrEmpty(firstIdentifier.Text);

      while (Current.Kind == SyntaxKind.Dot)
      {
        dotTokens.Add(NextToken());
        var identifier = MatchToken(SyntaxKind.Identifier);
        identifiers.Add(identifier);
        isMalformed |= string.IsNullOrEmpty(identifier.Text);
      }

      return new QualifiedNameSyntax(identifiers, dotTokens);
    }

    private UseDirectiveSyntax ParseUseDirective()
    {
      var useKeyword = MatchToken(SyntaxKind.UseKeyword);
      var path = ParseQualifiedName(out var isMalformed);

      SyntaxToken asKeyword = null;
      SyntaxToken alias = null;
      if (Current.Kind == SyntaxKind.AsKeyword)
      {
        asKeyword = NextToken();
        alias = MatchToken(SyntaxKind.Identifier);
        isMalformed |= string.IsNullOrEmpty(alias.Text);
      }

      var semicolonToken = MatchToken(SyntaxKind.Semicolon);
      isMalformed |= string.IsNullOrEmpty(semicolonToken.Text);

      if (isMalformed)
      {
        var end = semicolonToken.Span.End;
        if (end <= useKeyword.Span.Start)
        {
          end = alias?.Span.End ?? path.Identifiers[^1].Span.End;
        }

        Diagnostics.ReportInvalidUseDirective(
            TextSpan.FromBounds(useKeyword.Span.Start, end));
      }

      return new UseDirectiveSyntax(
          useKeyword,
          path,
          asKeyword,
          alias,
          semicolonToken,
          isMalformed);
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
      switch (Current.Kind)
      {
        case SyntaxKind.LeftParen:
          return ParseParenthesizedExpression();

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

    private ParenthesizedExpressionSyntax ParseParenthesizedExpression()
    {
      var openParenToken = MatchToken(SyntaxKind.LeftParen);
      var expression = ParseExpression();
      var closeParenToken = MatchToken(SyntaxKind.RightParen);
      return new ParenthesizedExpressionSyntax(
          openParenToken,
          expression,
          closeParenToken);
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
      return new ArrayLiteralExpressionSyntax(
          openBracketToken,
          elements,
          separators,
          closeBracketToken);
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

    private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
      ExpressionSyntax left;

      var unaryPrecedence = SyntaxFacts.GetUnaryOperatorPrecedence(Current.Kind);
      if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
      {
        var operatorToken = NextToken();
        var operand = ParseExpression(unaryPrecedence);
        left = new UnaryExpressionSyntax(operatorToken, operand);
      }
      else
      {
        left = ParsePostfixExpression();
      }

      while (true)
      {
        var operatorKind = Current.Kind;
        var precedence = SyntaxFacts.GetBinaryOperatorPrecedence(operatorKind);
        if (precedence == 0 || precedence < parentPrecedence)
          break;

        var operatorToken = NextToken();
        var rightPrecedence = SyntaxFacts.IsRightAssociative(operatorKind)
            ? precedence
            : precedence + 1;
        var right = ParseExpression(rightPrecedence);

        left = SyntaxFacts.IsAssignmentOperator(operatorKind)
            ? new AssignmentExpressionSyntax(left, operatorToken, right)
            : new BinaryExpressionSyntax(left, operatorToken, right);
      }

      return left;
    }

    private TypeClauseSyntax ParseTypeClause()
    {
      var colonToken = MatchToken(SyntaxKind.Colon);
      var type = ParseTypeSyntax();
      return new TypeClauseSyntax(colonToken, type);
    }

    private TypeSyntax ParseTypeSyntax()
    {
      var parts = new List<SyntaxToken>();
      var dots = new List<SyntaxToken>();

      parts.Add(ParseTypeIdentifierToken());

      while (Current.Kind == SyntaxKind.Dot)
      {
        dots.Add(NextToken());
        parts.Add(MatchToken(SyntaxKind.Identifier));
      }

      return new TypeSyntax(parts, dots);
    }

    private SyntaxToken ParseTypeIdentifierToken()
    {
      if (Current.Kind == SyntaxKind.Identifier ||
          Current.Kind == SyntaxKind.U0Keyword)
      {
        return NextToken();
      }

      return MatchToken(SyntaxKind.Identifier);
    }

    private VariableDeclarationStatementSyntax ParseVariableDeclarationStatement()
    {
      var letKeyword = MatchToken(SyntaxKind.LetKeyword);
      SyntaxToken mutKeyword = null;
      if (Current.Kind == SyntaxKind.MutKeyword)
        mutKeyword = NextToken();

      var identifier = MatchToken(SyntaxKind.Identifier);

      TypeClauseSyntax typeClause = null;
      if (Current.Kind == SyntaxKind.Colon)
        typeClause = ParseTypeClause();

      SyntaxToken equalsToken = null;
      ExpressionSyntax initializer = null;
      if (Current.Kind == SyntaxKind.EqualsToken)
      {
        equalsToken = NextToken();
        initializer = ParseExpression();
      }

      var semicolon = MatchToken(SyntaxKind.Semicolon);
      return new VariableDeclarationStatementSyntax(
          letKeyword,
          mutKeyword,
          identifier,
          typeClause,
          equalsToken,
          initializer,
          semicolon);
    }

    private ExpressionStatementSyntax ParseExpressionStatement()
    {
      var expression = ParseExpression();
      var semicolon = MatchToken(SyntaxKind.Semicolon);
      return new ExpressionStatementSyntax(expression, semicolon);
    }

    private ReturnStatementSyntax ParseReturnStatement()
    {
      var returnKeyword = MatchToken(SyntaxKind.ReturnKeyword);
      ExpressionSyntax expression = null;
      if (Current.Kind != SyntaxKind.Semicolon &&
          Current.Kind != SyntaxKind.EndOfFile)
      {
        expression = ParseExpression();
      }

      var semicolon = MatchToken(SyntaxKind.Semicolon);
      return new ReturnStatementSyntax(returnKeyword, expression, semicolon);
    }

    private StatementSyntax ParseStatement()
    {
      if (Current.Kind == SyntaxKind.LeftBrace)
        return ParseBlockStatement();

      if (Current.Kind == SyntaxKind.LetKeyword)
        return ParseVariableDeclarationStatement();

      if (Current.Kind == SyntaxKind.ReturnKeyword)
        return ParseReturnStatement();

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
      var parameters = new List<EventParameterSyntax>();
      var separators = new List<SyntaxToken>();

      if (Current.Kind != SyntaxKind.RightParen &&
          Current.Kind != SyntaxKind.EndOfFile)
      {
        while (true)
        {
          var parameterName = MatchToken(SyntaxKind.Identifier);
          var colon = MatchToken(SyntaxKind.Colon);
          var type = ParseTypeSyntax();
          parameters.Add(new EventParameterSyntax(parameterName, colon, type));

          if (Current.Kind != SyntaxKind.Comma)
            break;

          separators.Add(NextToken());
        }
      }

      var closeParenToken = MatchToken(SyntaxKind.RightParen);
      TypeClauseSyntax returnTypeAnnotation = null;
      if (Current.Kind == SyntaxKind.Colon)
        returnTypeAnnotation = ParseTypeClause();

      var body = ParseBlockStatement();

      return new EventDeclarationSyntax(
          onKeyword,
          identifier,
          openParenToken,
          parameters,
          separators,
          closeParenToken,
          returnTypeAnnotation,
          body);
    }

    private MemberSyntax ParseMember()
    {
      if (Current.Kind == SyntaxKind.On)
        return ParseEventDeclaration();

      if (Current.Kind == SyntaxKind.UseKeyword)
        return ParseUseDirective();

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
