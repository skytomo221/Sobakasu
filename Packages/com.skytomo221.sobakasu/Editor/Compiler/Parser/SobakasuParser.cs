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
        case SyntaxKind.IfKeyword:
          return ParseIfExpression();

        case SyntaxKind.WhileKeyword:
          return ParseWhileExpression(null);

        case SyntaxKind.LoopKeyword:
          return ParseLoopExpression(null);

        case SyntaxKind.LabelIdentifier:
          return ParseLabeledLoopExpression();

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

    private IfExpressionSyntax ParseIfExpression()
    {
      var ifKeyword = MatchToken(SyntaxKind.IfKeyword);
      var condition = ParseExpression();
      var thenBlock = ParseRequiredControlBlock(ifKeyword);

      SyntaxToken elseKeyword = null;
      ExpressionSyntax elseExpression = null;
      if (Current.Kind == SyntaxKind.ElseKeyword)
      {
        elseKeyword = NextToken();
        if (Current.Kind == SyntaxKind.IfKeyword)
        {
          elseExpression = ParseIfExpression();
        }
        else
        {
          elseExpression = new BlockExpressionSyntax(
              ParseRequiredControlBlock(elseKeyword));
        }
      }

      return new IfExpressionSyntax(
          ifKeyword,
          condition,
          thenBlock,
          elseKeyword,
          elseExpression);
    }

    private ExpressionSyntax ParseLabeledLoopExpression()
    {
      var labelToken = MatchToken(SyntaxKind.LabelIdentifier);
      SyntaxToken colonToken;
      if (Current.Kind == SyntaxKind.Colon)
      {
        colonToken = NextToken();
      }
      else
      {
        Diagnostics.ReportMissingLoopLabelColon(labelToken.Span);
        colonToken = new SyntaxToken(
            SyntaxKind.Colon,
            new TextSpan(labelToken.Span.End, 0),
            string.Empty);
      }

      var label = new LoopLabelSyntax(labelToken, colonToken);
      if (Current.Kind == SyntaxKind.WhileKeyword)
        return ParseWhileExpression(label);

      if (Current.Kind == SyntaxKind.LoopKeyword)
        return ParseLoopExpression(label);

      Diagnostics.ReportInvalidLoopLabelTarget(Current.Span);
      var bad = NextToken();
      return new NameExpressionSyntax(bad);
    }

    private WhileExpressionSyntax ParseWhileExpression(LoopLabelSyntax label)
    {
      var whileKeyword = MatchToken(SyntaxKind.WhileKeyword);
      var condition = ParseExpression();
      var body = ParseRequiredControlBlock(whileKeyword);
      return new WhileExpressionSyntax(label, whileKeyword, condition, body);
    }

    private LoopExpressionSyntax ParseLoopExpression(LoopLabelSyntax label)
    {
      var loopKeyword = MatchToken(SyntaxKind.LoopKeyword);
      var body = ParseRequiredControlBlock(loopKeyword);
      return new LoopExpressionSyntax(label, loopKeyword, body);
    }

    private BlockStatementSyntax ParseRequiredControlBlock(SyntaxToken keyword)
    {
      if (Current.Kind == SyntaxKind.LeftBrace)
        return ParseBlockStatement(allowTrailingExpression: true);

      Diagnostics.ReportControlBodyRequiresBlock(Current.Span, keyword.Text);
      var missingOpen = new SyntaxToken(
          SyntaxKind.LeftBrace,
          new TextSpan(Current.Span.Start, 0),
          string.Empty);
      var missingClose = new SyntaxToken(
          SyntaxKind.RightBrace,
          new TextSpan(Current.Span.Start, 0),
          string.Empty);
      return new BlockStatementSyntax(
          missingOpen,
          new List<StatementSyntax>(),
          null,
          missingClose);
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

    private SynchronizationModifierSyntax ParseSynchronizationModifier()
    {
      var syncKeyword = MatchToken(SyntaxKind.SyncKeyword);
      if (Current.Kind != SyntaxKind.LeftParen)
      {
        return new SynchronizationModifierSyntax(
            syncKeyword,
            null,
            null,
            null,
            SynchronizationModeSyntaxKind.None);
      }

      var openParen = NextToken();
      SyntaxToken modeToken = null;
      var mode = SynchronizationModeSyntaxKind.Invalid;

      if (Current.Kind == SyntaxKind.Identifier)
      {
        modeToken = NextToken();
        mode = modeToken.Text switch
        {
          "none" => SynchronizationModeSyntaxKind.None,
          "linear" => SynchronizationModeSyntaxKind.Linear,
          "smooth" => SynchronizationModeSyntaxKind.Smooth,
          _ => SynchronizationModeSyntaxKind.Invalid
        };

        if (mode == SynchronizationModeSyntaxKind.Invalid)
        {
          Diagnostics.ReportUnknownSynchronizationMode(
              modeToken.Span,
              modeToken.Text ?? string.Empty);
        }
      }
      else if (Current.Kind == SyntaxKind.RightParen)
      {
        Diagnostics.ReportSynchronizationModeArgumentCount(Current.Span);
      }
      else
      {
        Diagnostics.ReportUnknownSynchronizationMode(
            Current.Span,
            Current.Text ?? string.Empty);
      }

      if (Current.Kind != SyntaxKind.RightParen &&
          Current.Kind != SyntaxKind.EndOfFile &&
          Current.Kind != SyntaxKind.LetKeyword)
      {
        Diagnostics.ReportSynchronizationModeArgumentCount(Current.Span);
        while (Current.Kind != SyntaxKind.RightParen &&
               Current.Kind != SyntaxKind.EndOfFile &&
               Current.Kind != SyntaxKind.LetKeyword)
        {
          NextToken();
        }
      }

      var closeParen = MatchToken(SyntaxKind.RightParen);
      return new SynchronizationModifierSyntax(
          syncKeyword,
          openParen,
          modeToken,
          closeParen,
          mode);
    }

    private StateDeclarationSyntax ParseStateDeclaration()
    {
      SyntaxToken pubKeyword = null;
      SynchronizationModifierSyntax synchronizationModifier = null;
      var sawSynchronizationModifier = false;

      while (Current.Kind == SyntaxKind.PubKeyword ||
             Current.Kind == SyntaxKind.SyncKeyword)
      {
        if (Current.Kind == SyntaxKind.PubKeyword)
        {
          var currentPub = NextToken();
          if (pubKeyword != null)
            Diagnostics.ReportDuplicateStateModifier(currentPub.Span, "pub");
          else
            pubKeyword = currentPub;

          if (sawSynchronizationModifier)
            Diagnostics.ReportStateModifierOrder(currentPub.Span);

          continue;
        }

        var currentSynchronizationModifier = ParseSynchronizationModifier();
        if (synchronizationModifier != null)
        {
          Diagnostics.ReportDuplicateStateModifier(
              currentSynchronizationModifier.SyncKeyword.Span,
              "sync");
        }
        else
        {
          synchronizationModifier = currentSynchronizationModifier;
        }

        sawSynchronizationModifier = true;
      }

      var letKeyword = MatchToken(SyntaxKind.LetKeyword);
      ConsumeMisplacedStateModifiers();

      SyntaxToken mutKeyword = null;
      if (Current.Kind == SyntaxKind.MutKeyword)
        mutKeyword = NextToken();

      ConsumeMisplacedStateModifiers();
      var identifier = MatchToken(SyntaxKind.Identifier);

      TypeClauseSyntax typeClause = null;
      if (Current.Kind == SyntaxKind.Colon)
        typeClause = ParseTypeClause();

      SyntaxToken equalsToken = null;
      ExpressionSyntax initializer = null;
      if (Current.Kind == SyntaxKind.EqualsToken)
      {
        equalsToken = NextToken();
        if (Current.Kind == SyntaxKind.Semicolon)
        {
          Diagnostics.ReportMissingTopLevelStateInitializer(
              Current.Span,
              identifier.Text ?? string.Empty);
        }
        else
        {
          initializer = ParseExpression();
        }
      }
      else
      {
        Diagnostics.ReportMissingTopLevelStateInitializer(
            identifier.Span,
            identifier.Text ?? string.Empty);
      }

      var semicolon = MatchToken(SyntaxKind.Semicolon);
      return new StateDeclarationSyntax(
          pubKeyword,
          synchronizationModifier,
          letKeyword,
          mutKeyword,
          identifier,
          typeClause,
          equalsToken,
          initializer,
          semicolon);
    }

    private void ConsumeMisplacedStateModifiers()
    {
      while (Current.Kind == SyntaxKind.PubKeyword ||
             Current.Kind == SyntaxKind.SyncKeyword)
      {
        Diagnostics.ReportStateModifierOrder(Current.Span);
        if (Current.Kind == SyntaxKind.SyncKeyword)
          ParseSynchronizationModifier();
        else
          NextToken();
      }
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
      SyntaxToken semicolon = null;
      if (Current.Kind == SyntaxKind.Semicolon)
      {
        semicolon = NextToken();
      }
      else if (!IsControlExpression(expression))
      {
        semicolon = MatchToken(SyntaxKind.Semicolon);
      }

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

    private BreakStatementSyntax ParseBreakStatement()
    {
      var breakKeyword = MatchToken(SyntaxKind.BreakKeyword);
      SyntaxToken label = null;
      if (Current.Kind == SyntaxKind.LabelIdentifier)
        label = NextToken();

      ExpressionSyntax expression = null;
      if (Current.Kind != SyntaxKind.Semicolon &&
          Current.Kind != SyntaxKind.RightBrace &&
          Current.Kind != SyntaxKind.EndOfFile)
      {
        expression = ParseExpression();
      }

      var semicolon = RecoverJumpTerminator("break");
      return new BreakStatementSyntax(
          breakKeyword,
          label,
          expression,
          semicolon);
    }

    private ContinueStatementSyntax ParseContinueStatement()
    {
      var continueKeyword = MatchToken(SyntaxKind.ContinueKeyword);
      SyntaxToken label = null;
      if (Current.Kind == SyntaxKind.LabelIdentifier)
        label = NextToken();

      if (Current.Kind != SyntaxKind.Semicolon &&
          Current.Kind != SyntaxKind.RightBrace &&
          Current.Kind != SyntaxKind.EndOfFile)
      {
        Diagnostics.ReportJumpDoesNotAcceptValue(
            Current.Span,
            continueKeyword.Text);
      }

      var semicolon = RecoverJumpTerminator("continue");
      return new ContinueStatementSyntax(
          continueKeyword,
          label,
          semicolon);
    }

    private RedoStatementSyntax ParseRedoStatement()
    {
      var redoKeyword = MatchToken(SyntaxKind.RedoKeyword);
      SyntaxToken label = null;
      if (Current.Kind == SyntaxKind.LabelIdentifier)
        label = NextToken();

      if (Current.Kind != SyntaxKind.Semicolon &&
          Current.Kind != SyntaxKind.RightBrace &&
          Current.Kind != SyntaxKind.EndOfFile)
      {
        Diagnostics.ReportJumpDoesNotAcceptValue(
            Current.Span,
            redoKeyword.Text);
      }

      var semicolon = RecoverJumpTerminator("redo");
      return new RedoStatementSyntax(
          redoKeyword,
          label,
          semicolon);
    }

    private SyntaxToken RecoverJumpTerminator(string statementName)
    {
      if (Current.Kind == SyntaxKind.Semicolon)
        return NextToken();

      if (Current.Kind != SyntaxKind.RightBrace &&
          Current.Kind != SyntaxKind.EndOfFile)
      {
        Diagnostics.ReportInvalidJumpSyntax(Current.Span, statementName);
        while (Current.Kind != SyntaxKind.Semicolon &&
               Current.Kind != SyntaxKind.RightBrace &&
               Current.Kind != SyntaxKind.EndOfFile)
        {
          NextToken();
        }
      }

      return MatchToken(SyntaxKind.Semicolon);
    }

    private static bool CanStartExpression(SyntaxKind kind)
    {
      switch (kind)
      {
        case SyntaxKind.LeftParen:
        case SyntaxKind.IfKeyword:
        case SyntaxKind.WhileKeyword:
        case SyntaxKind.LoopKeyword:
        case SyntaxKind.LabelIdentifier:
        case SyntaxKind.String:
        case SyntaxKind.Int8Literal:
        case SyntaxKind.UInt8Literal:
        case SyntaxKind.Int16Literal:
        case SyntaxKind.UInt16Literal:
        case SyntaxKind.Int32Literal:
        case SyntaxKind.UInt32Literal:
        case SyntaxKind.Int64Literal:
        case SyntaxKind.UInt64Literal:
        case SyntaxKind.Float32Literal:
        case SyntaxKind.Float64Literal:
        case SyntaxKind.CharacterLiteral:
        case SyntaxKind.TrueKeyword:
        case SyntaxKind.FalseKeyword:
        case SyntaxKind.NullKeyword:
        case SyntaxKind.LeftBracket:
        case SyntaxKind.Identifier:
        case SyntaxKind.PlusToken:
        case SyntaxKind.MinusToken:
        case SyntaxKind.BangToken:
        case SyntaxKind.TildeToken:
          return true;

        default:
          return false;
      }
    }

    private StatementSyntax ParseStatement()
    {
      if (Current.Kind == SyntaxKind.LeftBrace)
        return ParseBlockStatement();

      if (Current.Kind == SyntaxKind.PubKeyword ||
          Current.Kind == SyntaxKind.SyncKeyword)
      {
        return ParseInvalidLocalStateDeclaration();
      }

      if (Current.Kind == SyntaxKind.LetKeyword)
        return ParseVariableDeclarationStatement();

      if (Current.Kind == SyntaxKind.ReturnKeyword)
        return ParseReturnStatement();

      if (Current.Kind == SyntaxKind.BreakKeyword)
        return ParseBreakStatement();

      if (Current.Kind == SyntaxKind.ContinueKeyword)
        return ParseContinueStatement();

      if (Current.Kind == SyntaxKind.RedoKeyword)
        return ParseRedoStatement();

      return ParseExpressionStatement();
    }

    private StatementSyntax ParseInvalidLocalStateDeclaration()
    {
      while (Current.Kind == SyntaxKind.PubKeyword ||
             Current.Kind == SyntaxKind.SyncKeyword)
      {
        if (Current.Kind == SyntaxKind.PubKeyword)
        {
          var pubKeyword = NextToken();
          Diagnostics.ReportPublicModifierOnlyOnTopLevelState(pubKeyword.Span);
          continue;
        }

        var syncKeyword = Current;
        Diagnostics.ReportSynchronizedStateMustBeTopLevel(syncKeyword.Span);
        ParseSynchronizationModifier();
      }

      if (Current.Kind == SyntaxKind.LetKeyword)
        return ParseVariableDeclarationStatement();

      return ParseExpressionStatement();
    }

    private BlockStatementSyntax ParseBlockStatement(bool allowTrailingExpression = false)
    {
      var openBrace = MatchToken(SyntaxKind.LeftBrace);
      var statements = new List<StatementSyntax>();
      ExpressionSyntax trailingExpression = null;

      while (Current.Kind != SyntaxKind.RightBrace &&
             Current.Kind != SyntaxKind.EndOfFile)
      {
        if (allowTrailingExpression && CanStartExpression(Current.Kind))
        {
          var expression = ParseExpression();
          if (Current.Kind == SyntaxKind.RightBrace)
          {
            trailingExpression = expression;
            break;
          }

          SyntaxToken semicolon = null;
          if (Current.Kind == SyntaxKind.Semicolon)
          {
            semicolon = NextToken();
          }
          else if (!IsControlExpression(expression))
          {
            semicolon = MatchToken(SyntaxKind.Semicolon);
          }

          statements.Add(new ExpressionStatementSyntax(expression, semicolon));
          continue;
        }

        statements.Add(ParseStatement());
      }

      var closeBrace = MatchToken(SyntaxKind.RightBrace);
      return new BlockStatementSyntax(openBrace, statements, trailingExpression, closeBrace);
    }

    private static bool IsControlExpression(ExpressionSyntax expression)
    {
      return expression is IfExpressionSyntax ||
             expression is WhileExpressionSyntax ||
             expression is LoopExpressionSyntax;
    }

    private ParameterSyntax ParseParameter()
    {
      var parameterName = MatchToken(SyntaxKind.Identifier);
      var colon = MatchToken(SyntaxKind.Colon);
      var type = ParseTypeSyntax();
      return new ParameterSyntax(parameterName, colon, type);
    }

    private void ParseParameterList(
        IList<ParameterSyntax> parameters,
        IList<SyntaxToken> separators)
    {
      if (Current.Kind == SyntaxKind.RightParen ||
          Current.Kind == SyntaxKind.EndOfFile)
      {
        return;
      }

      while (true)
      {
        parameters.Add(ParseParameter());

        if (Current.Kind != SyntaxKind.Comma)
          break;

        separators.Add(NextToken());
      }
    }

    private FunctionReturnTypeSyntax ParseFunctionReturnType()
    {
      var arrowToken = MatchToken(SyntaxKind.ArrowToken);
      var type = ParseTypeSyntax();
      return new FunctionReturnTypeSyntax(arrowToken, type);
    }

    private FunctionDeclarationSyntax ParseFunctionDeclaration()
    {
      var fnKeyword = MatchToken(SyntaxKind.FnKeyword);
      var identifier = MatchToken(SyntaxKind.Identifier);
      var openParenToken = MatchToken(SyntaxKind.LeftParen);
      var parameters = new List<ParameterSyntax>();
      var separators = new List<SyntaxToken>();
      ParseParameterList(parameters, separators);

      var closeParenToken = MatchToken(SyntaxKind.RightParen);
      FunctionReturnTypeSyntax returnTypeAnnotation = null;
      if (Current.Kind == SyntaxKind.ArrowToken)
        returnTypeAnnotation = ParseFunctionReturnType();

      var body = ParseBlockStatement(allowTrailingExpression: true);

      return new FunctionDeclarationSyntax(
          fnKeyword,
          identifier,
          openParenToken,
          parameters,
          separators,
          closeParenToken,
          returnTypeAnnotation,
          body);
    }

    private EventDeclarationSyntax ParseEventDeclaration()
    {
      var onKeyword = MatchToken(SyntaxKind.On);
      var identifier = MatchToken(SyntaxKind.Identifier);
      var openParenToken = MatchToken(SyntaxKind.LeftParen);
      var parameters = new List<ParameterSyntax>();
      var separators = new List<SyntaxToken>();
      ParseParameterList(parameters, separators);

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
      if (TryFindModifiedNonStateMember(out var modifiedMemberKind))
      {
        while (Current.Kind == SyntaxKind.PubKeyword ||
               Current.Kind == SyntaxKind.SyncKeyword)
        {
          if (Current.Kind == SyntaxKind.PubKeyword)
          {
            var pubKeyword = NextToken();
            Diagnostics.ReportUnsupportedTopLevelModifier(
                pubKeyword.Span,
                "pub",
                modifiedMemberKind);
          }
          else
          {
            var syncKeyword = Current;
            ParseSynchronizationModifier();
            Diagnostics.ReportUnsupportedTopLevelModifier(
                syncKeyword.Span,
                "sync",
                modifiedMemberKind);
          }
        }

        return ParseMember();
      }

      if (Current.Kind == SyntaxKind.FnKeyword)
        return ParseFunctionDeclaration();

      if (Current.Kind == SyntaxKind.On)
        return ParseEventDeclaration();

      if (Current.Kind == SyntaxKind.UseKeyword)
        return ParseUseDirective();

      if (Current.Kind == SyntaxKind.LetKeyword ||
          Current.Kind == SyntaxKind.PubKeyword ||
          Current.Kind == SyntaxKind.SyncKeyword)
      {
        return ParseStateDeclaration();
      }

      Diagnostics.ReportUnexpectedMember(Current.Span, Current.Kind);

      var badToken = NextToken();
      return new SkippedMemberSyntax(badToken);
    }

    private bool TryFindModifiedNonStateMember(out SyntaxKind memberKind)
    {
      var offset = 0;
      var sawModifier = false;
      while (true)
      {
        if (Peek(offset).Kind == SyntaxKind.PubKeyword)
        {
          sawModifier = true;
          offset++;
          continue;
        }

        if (Peek(offset).Kind != SyntaxKind.SyncKeyword)
          break;

        sawModifier = true;
        offset++;
        if (Peek(offset).Kind != SyntaxKind.LeftParen)
          continue;

        offset++;
        while (Peek(offset).Kind != SyntaxKind.RightParen &&
               Peek(offset).Kind != SyntaxKind.EndOfFile)
        {
          offset++;
        }

        if (Peek(offset).Kind == SyntaxKind.RightParen)
          offset++;
      }

      memberKind = Peek(offset).Kind;
      return sawModifier &&
          (memberKind == SyntaxKind.FnKeyword ||
           memberKind == SyntaxKind.On ||
           memberKind == SyntaxKind.UseKeyword);
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
