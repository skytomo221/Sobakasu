using System;
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
    private int _suppressAggregateInitializerDepth;
    private SyntaxToken _pendingGreaterToken;

    public DiagnosticBag Diagnostics { get; } = new();

    public SobakasuParser(SourceText text)
        : this(text, string.Empty)
    {
    }

    internal SobakasuParser(SourceText text, string sourcePath)
    {
      var lexer = new SobakasuLexer(text);
      lexer.Diagnostics.SourcePath = sourcePath ?? string.Empty;
      Diagnostics.SourcePath = sourcePath ?? string.Empty;
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

    private SyntaxToken Current => _pendingGreaterToken ?? Peek(0);

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
      if (_pendingGreaterToken != null)
      {
        _pendingGreaterToken = null;
        return current;
      }
      _position++;
      return current;
    }

    private SyntaxToken MatchTypeArgumentGreaterToken()
    {
      if (Current.Kind == SyntaxKind.GreaterToken)
        return NextToken();

      if (Current.Kind == SyntaxKind.GreaterGreaterToken)
      {
        var shift = NextToken();
        var first = new SyntaxToken(
            SyntaxKind.GreaterToken,
            new TextSpan(shift.Span.Start, 1),
            ">");
        _pendingGreaterToken = new SyntaxToken(
            SyntaxKind.GreaterToken,
            new TextSpan(shift.Span.Start + 1, 1),
            ">");
        return first;
      }

      return MatchToken(SyntaxKind.GreaterToken);
    }

    private SyntaxToken MatchToken(SyntaxKind kind)
    {
      if (Current.Kind == kind)
        return NextToken();

      Diagnostics.ReportUnexpectedToken(Current.Span, Current.Kind, kind);
      return new SyntaxToken(kind, Current.Span, string.Empty);
    }

    private SyntaxToken ParseCallableQuestionSuffix(SyntaxToken identifier)
    {
      SyntaxToken questionToken = null;
      if (Current.Kind == SyntaxKind.QuestionToken)
      {
        questionToken = NextToken();

        if (Current.Kind == SyntaxKind.QuestionToken)
        {
          var start = Current.Span.Start;
          var end = Current.Span.End;
          while (Current.Kind == SyntaxKind.QuestionToken)
          {
            end = Current.Span.End;
            NextToken();
          }

          Diagnostics.ReportMultipleCallableQuestionMarks(
              TextSpan.FromBounds(start, end));
        }

        if (Current.Kind == SyntaxKind.Identifier &&
            questionToken.Span.End == Current.Span.Start)
        {
          Diagnostics.ReportQuestionMarkMustEndCallableName(questionToken.Span);
        }
      }

      var suffixEnd = questionToken?.Span.End ?? identifier.Span.End;
      if (Current.Kind == SyntaxKind.BangToken &&
          suffixEnd == Current.Span.Start)
      {
        Diagnostics.ReportBangCallableNameSuffix(Current.Span);
        NextToken();
      }

      return questionToken;
    }

    private NameExpressionSyntax ParseNameExpression()
    {
      var identifier = Current.Kind == SyntaxKind.SelfKeyword ||
                       Current.Kind == SyntaxKind.SelfTypeKeyword
          ? NextToken()
          : MatchToken(SyntaxKind.Identifier);
      var questionToken = identifier.Kind == SyntaxKind.Identifier
          ? ParseCallableQuestionSuffix(identifier)
          : null;
      return new NameExpressionSyntax(identifier, questionToken);
    }

    private void RejectQuestionMarkInName(string nameKind)
    {
      if (Current.Kind != SyntaxKind.QuestionToken)
        return;

      var start = Current.Span.Start;
      var end = Current.Span.End;
      while (Current.Kind == SyntaxKind.QuestionToken)
      {
        end = Current.Span.End;
        NextToken();
      }

      Diagnostics.ReportQuestionMarkNotAllowedInName(
          TextSpan.FromBounds(start, end),
          nameKind);
    }

    private void ParseOptionalParameterList(
        string declarationKind,
        SyntaxKind returnTypeStart,
        bool allowExternalBinding,
        IList<ParameterSyntax> parameters,
        IList<SyntaxToken> separators,
        out SyntaxToken openParenToken,
        out SyntaxToken closeParenToken)
    {
      openParenToken = null;
      closeParenToken = null;

      if (Current.Kind == SyntaxKind.LeftParen)
      {
        openParenToken = NextToken();
        ParseParameterList(parameters, separators);
        closeParenToken = MatchToken(SyntaxKind.RightParen);
        return;
      }

      if (Current.Kind == returnTypeStart ||
          allowExternalBinding && Current.Kind == SyntaxKind.EqualsToken ||
          Current.Kind == SyntaxKind.LeftBrace)
      {
        return;
      }

      Diagnostics.ReportCallableParametersRequireParentheses(
          Current.Span,
          declarationKind);

      while (Current.Kind != returnTypeStart &&
             (!allowExternalBinding || Current.Kind != SyntaxKind.EqualsToken) &&
             Current.Kind != SyntaxKind.LeftBrace &&
             Current.Kind != SyntaxKind.EndOfFile &&
             Current.Kind != SyntaxKind.FnKeyword &&
             Current.Kind != SyntaxKind.ReceiveKeyword &&
             Current.Kind != SyntaxKind.On &&
             Current.Kind != SyntaxKind.UseKeyword)
      {
        NextToken();
      }
    }

    private QualifiedNameSyntax ParseQualifiedName(out bool isMalformed)
    {
      var identifiers = new List<SyntaxToken>();
      var dotTokens = new List<SyntaxToken>();

      var firstIdentifier = MatchToken(SyntaxKind.Identifier);
      identifiers.Add(firstIdentifier);
      isMalformed = string.IsNullOrEmpty(firstIdentifier.Text);

      while (Current.Kind == SyntaxKind.Dot ||
             (Current.Kind == SyntaxKind.Colon &&
              Peek(1).Kind == SyntaxKind.Colon))
      {
        if (Current.Kind == SyntaxKind.Colon)
        {
          var firstColon = NextToken();
          var secondColon = NextToken();
          Diagnostics.ReportDoubleColonModulePath(
              TextSpan.FromBounds(
                  firstColon.Span.Start,
                  secondColon.Span.End));
          dotTokens.Add(firstColon);
          isMalformed = true;
        }
        else
        {
          dotTokens.Add(NextToken());
        }

        var identifier = MatchToken(SyntaxKind.Identifier);
        identifiers.Add(identifier);
        isMalformed |= string.IsNullOrEmpty(identifier.Text);
      }

      return new QualifiedNameSyntax(identifiers, dotTokens);
    }

    private NewExpressionSyntax ParseNewExpression()
    {
      var newKeyword = MatchToken(SyntaxKind.NewKeyword);
      var type = ParseTypeSyntax();
      var openParen = MatchToken(SyntaxKind.LeftParen);
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

      var closeParen = MatchToken(SyntaxKind.RightParen);
      return new NewExpressionSyntax(
          newKeyword,
          type,
          openParen,
          arguments,
          closeParen);
    }

    private UseDirectiveSyntax ParseUseDirective()
    {
      SyntaxToken pubKeyword = null;
      if (Current.Kind == SyntaxKind.PubKeyword)
        pubKeyword = NextToken();

      var useKeyword = MatchToken(SyntaxKind.UseKeyword);
      var useTree = ParseUseTree(allowBareSpecial: false, out var isMalformed);

      var semicolonToken = MatchToken(SyntaxKind.Semicolon);
      isMalformed |= string.IsNullOrEmpty(semicolonToken.Text);

      if (isMalformed)
      {
        var end = semicolonToken.Span.End;
        if (end <= useKeyword.Span.Start)
          end = useTree.GetSpan().End;

        Diagnostics.ReportInvalidUseDirective(
            TextSpan.FromBounds(useKeyword.Span.Start, end));
      }

      return new UseDirectiveSyntax(
          pubKeyword,
          useKeyword,
          useTree,
          semicolonToken,
          isMalformed);
    }

    private UseTreeSyntax ParseUseTree(bool allowBareSpecial, out bool isMalformed)
    {
      isMalformed = false;
      if (allowBareSpecial && Current.Kind == SyntaxKind.SelfKeyword)
      {
        var selfKeyword = NextToken();
        ParseUseTreeAlias(
            out var selfAsKeyword,
            out var selfAlias,
            ref isMalformed);
        return new UseTreeSyntax(
            null,
            selfKeyword,
            null,
            null,
            null,
            selfAsKeyword,
            selfAlias);
      }

      if (allowBareSpecial && Current.Kind == SyntaxKind.StarToken)
      {
        return new UseTreeSyntax(
            null,
            null,
            null,
            null,
            NextToken(),
            null,
            null);
      }

      var identifiers = new List<SyntaxToken>();
      var dotTokens = new List<SyntaxToken>();
      var firstIdentifier = MatchToken(SyntaxKind.Identifier);
      identifiers.Add(firstIdentifier);
      isMalformed = string.IsNullOrEmpty(firstIdentifier.Text);

      SyntaxToken suffixDot = null;
      UseTreeGroupSyntax group = null;
      SyntaxToken starToken = null;
      while (Current.Kind == SyntaxKind.Dot ||
             Current.Kind == SyntaxKind.Colon && Peek(1).Kind == SyntaxKind.Colon)
      {
        SyntaxToken separator;
        var isDoubleColon = Current.Kind == SyntaxKind.Colon;
        if (isDoubleColon)
        {
          var firstColon = NextToken();
          var secondColon = NextToken();
          Diagnostics.ReportDoubleColonModulePath(
              TextSpan.FromBounds(firstColon.Span.Start, secondColon.Span.End));
          separator = firstColon;
          isMalformed = true;
        }
        else
        {
          separator = NextToken();
        }

        if (!isDoubleColon && Current.Kind == SyntaxKind.LeftBrace)
        {
          suffixDot = separator;
          group = ParseUseTreeGroup(out var groupMalformed);
          isMalformed |= groupMalformed;
          break;
        }

        if (!isDoubleColon && Current.Kind == SyntaxKind.StarToken)
        {
          suffixDot = separator;
          starToken = NextToken();
          break;
        }

        dotTokens.Add(separator);
        var identifier = MatchToken(SyntaxKind.Identifier);
        identifiers.Add(identifier);
        isMalformed |= string.IsNullOrEmpty(identifier.Text);
        if (string.IsNullOrEmpty(identifier.Text))
          break;
      }

      var path = new QualifiedNameSyntax(identifiers, dotTokens);
      SyntaxToken asKeyword = null;
      SyntaxToken alias = null;
      if (group == null && starToken == null)
        ParseUseTreeAlias(out asKeyword, out alias, ref isMalformed);

      return new UseTreeSyntax(
          path,
          null,
          suffixDot,
          group,
          starToken,
          asKeyword,
          alias);
    }

    private UseTreeGroupSyntax ParseUseTreeGroup(out bool isMalformed)
    {
      var openBrace = MatchToken(SyntaxKind.LeftBrace);
      var items = new List<UseTreeSyntax>();
      var commas = new List<SyntaxToken>();
      isMalformed = string.IsNullOrEmpty(openBrace.Text);

      while (Current.Kind != SyntaxKind.RightBrace &&
             Current.Kind != SyntaxKind.Semicolon &&
             Current.Kind != SyntaxKind.EndOfFile)
      {
        if (Current.Kind == SyntaxKind.Comma)
        {
          Diagnostics.ReportUnexpectedToken(
              Current.Span,
              Current.Kind,
              SyntaxKind.Identifier);
          commas.Add(NextToken());
          isMalformed = true;
          continue;
        }

        var start = _position;
        items.Add(ParseUseTree(allowBareSpecial: true, out var itemMalformed));
        isMalformed |= itemMalformed;
        if (Current.Kind == SyntaxKind.Comma)
        {
          commas.Add(NextToken());
          continue;
        }

        if (Current.Kind != SyntaxKind.RightBrace)
        {
          Diagnostics.ReportUnexpectedToken(
              Current.Span,
              Current.Kind,
              SyntaxKind.Comma);
          isMalformed = true;
          if (_position == start)
            NextToken();
        }
      }

      if (items.Count == 0)
      {
        Diagnostics.ReportUnexpectedToken(
            Current.Span,
            Current.Kind,
            SyntaxKind.Identifier);
        isMalformed = true;
      }

      var closeBrace = MatchToken(SyntaxKind.RightBrace);
      isMalformed |= string.IsNullOrEmpty(closeBrace.Text);
      return new UseTreeGroupSyntax(openBrace, items, commas, closeBrace);
    }

    private void ParseUseTreeAlias(
        out SyntaxToken asKeyword,
        out SyntaxToken alias,
        ref bool isMalformed)
    {
      asKeyword = null;
      alias = null;
      if (Current.Kind != SyntaxKind.AsKeyword)
        return;

      asKeyword = NextToken();
      alias = MatchToken(SyntaxKind.Identifier);
      isMalformed |= string.IsNullOrEmpty(alias.Text);
    }

    private ModDeclarationSyntax ParseModDeclaration()
    {
      SyntaxToken pubKeyword = null;
      if (Current.Kind == SyntaxKind.PubKeyword)
        pubKeyword = NextToken();

      var modKeyword = MatchToken(SyntaxKind.ModKeyword);
      var identifier = MatchToken(SyntaxKind.Identifier);
      var semicolonToken = MatchToken(SyntaxKind.Semicolon);
      var isMalformed = string.IsNullOrEmpty(identifier.Text) ||
          string.IsNullOrEmpty(semicolonToken.Text);
      if (isMalformed)
      {
        var end = semicolonToken.Span.End;
        if (end <= modKeyword.Span.Start)
          end = identifier.Span.End;
        Diagnostics.ReportInvalidModDeclaration(
            TextSpan.FromBounds(modKeyword.Span.Start, end));
      }

      return new ModDeclarationSyntax(
          pubKeyword,
          modKeyword,
          identifier,
          semicolonToken,
          isMalformed);
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
      switch (Current.Kind)
      {
        case SyntaxKind.ExternKeyword:
        {
          var externKeyword = NextToken();
          return new ExternExpressionSyntax(externKeyword, ParseExpression());
        }

        case SyntaxKind.NewKeyword:
          return ParseNewExpression();

        case SyntaxKind.IfKeyword:
          return ParseIfExpression();

        case SyntaxKind.MatchKeyword:
          return ParseMatchExpression();

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

        case SyntaxKind.LeftBracket:
          return ParseArrayLiteralExpression();

        case SyntaxKind.Identifier:
        case SyntaxKind.SelfKeyword:
        case SyntaxKind.SelfTypeKeyword:
          return ParseNameExpression();

        default:
          Diagnostics.ReportUnexpectedExpression(Current.Span, Current.Kind);
          var bad = NextToken();
          return new NameExpressionSyntax(bad);
      }
    }

    private IfExpressionSyntax ParseIfExpression()
    {
      var ifKeyword = MatchToken(SyntaxKind.IfKeyword);
      var condition = ParseControlCondition();
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

    private MatchExpressionSyntax ParseMatchExpression()
    {
      var matchKeyword = MatchToken(SyntaxKind.MatchKeyword);
      var expression = ParseControlCondition();
      var openBrace = MatchToken(SyntaxKind.LeftBrace);
      var arms = new List<MatchArmSyntax>();

      while (Current.Kind != SyntaxKind.RightBrace &&
             Current.Kind != SyntaxKind.EndOfFile &&
             !IsMemberStart(Current.Kind))
      {
        var start = _position;
        var pattern = ParsePattern();
        if (Current.Kind != SyntaxKind.FatArrowToken &&
            Current.Kind != SyntaxKind.Comma &&
            Current.Kind != SyntaxKind.RightBrace &&
            Current.Kind != SyntaxKind.EndOfFile)
        {
          Diagnostics.ReportUnsupportedPatternForm(Current.Span, Current.Text);
          SkipUnsupportedPatternTail();
        }
        var fatArrow = MatchToken(SyntaxKind.FatArrowToken);
        ExpressionSyntax armExpression;
        if (Current.Kind == SyntaxKind.LeftBrace)
        {
          armExpression = new BlockExpressionSyntax(
              ParseBlockStatement(allowTrailingExpression: true));
        }
        else
        {
          armExpression = ParseExpression();
        }

        SyntaxToken comma = null;
        if (Current.Kind == SyntaxKind.Comma)
          comma = NextToken();
        else if (Current.Kind != SyntaxKind.RightBrace)
          comma = MatchToken(SyntaxKind.Comma);

        arms.Add(new MatchArmSyntax(
            pattern,
            fatArrow,
            armExpression,
            comma));

        if (_position == start)
          NextToken();
      }

      var closeBrace = MatchToken(SyntaxKind.RightBrace);
      return new MatchExpressionSyntax(
          matchKeyword,
          expression,
          openBrace,
          arms,
          closeBrace);
    }

    private PatternSyntax ParsePattern()
    {
      switch (Current.Kind)
      {
        case SyntaxKind.String:
        case SyntaxKind.Int8Literal:
        case SyntaxKind.UInt8Literal:
        case SyntaxKind.Int16Literal:
        case SyntaxKind.UInt16Literal:
        case SyntaxKind.Int32Literal:
        case SyntaxKind.UInt32Literal:
        case SyntaxKind.Int64Literal:
        case SyntaxKind.UInt64Literal:
        case SyntaxKind.CharacterLiteral:
        case SyntaxKind.TrueKeyword:
        case SyntaxKind.FalseKeyword:
          return new LiteralPatternSyntax(NextToken());

        case SyntaxKind.Identifier:
          if (Current.Text == "_")
            return new WildcardPatternSyntax(NextToken());
          return ParseEnumVariantPattern();

        case SyntaxKind.Float32Literal:
        case SyntaxKind.Float64Literal:
        {
          var unsupported = NextToken();
          Diagnostics.ReportUnsupportedPatternForm(
              unsupported.Span,
              unsupported.Text);
          return new UnsupportedPatternSyntax(unsupported);
        }

        default:
        {
          var unsupported = NextToken();
          Diagnostics.ReportUnsupportedPatternForm(
              unsupported.Span,
              unsupported.Text);
          return new UnsupportedPatternSyntax(unsupported);
        }
      }
    }

    private void SkipUnsupportedPatternTail()
    {
      var parenDepth = 0;
      var braceDepth = 0;
      while (Current.Kind != SyntaxKind.EndOfFile)
      {
        if (parenDepth == 0 && braceDepth == 0 &&
            (Current.Kind == SyntaxKind.FatArrowToken ||
             Current.Kind == SyntaxKind.Comma ||
             Current.Kind == SyntaxKind.RightBrace))
        {
          return;
        }

        if (Current.Kind == SyntaxKind.LeftParen)
          parenDepth++;
        else if (Current.Kind == SyntaxKind.RightParen)
        {
          if (parenDepth == 0)
            return;
          parenDepth--;
        }
        else if (Current.Kind == SyntaxKind.LeftBrace)
          braceDepth++;
        else if (Current.Kind == SyntaxKind.RightBrace)
        {
          if (braceDepth == 0)
            return;
          braceDepth--;
        }

        NextToken();
      }
    }

    private PatternSyntax ParseEnumVariantPattern()
    {
      var identifiers = new List<SyntaxToken> { MatchToken(SyntaxKind.Identifier) };
      var dots = new List<SyntaxToken>();
      while (Current.Kind == SyntaxKind.Dot)
      {
        dots.Add(NextToken());
        identifiers.Add(MatchToken(SyntaxKind.Identifier));
      }

      if (identifiers.Count < 2)
      {
        Diagnostics.ReportUnsupportedPatternForm(
            identifiers[0].Span,
            identifiers[0].Text);
        return new UnsupportedPatternSyntax(identifiers[0]);
      }

      var typeParts = identifiers.GetRange(0, identifiers.Count - 1);
      var typeDots = dots.Count <= 1
          ? new List<SyntaxToken>()
          : dots.GetRange(0, dots.Count - 1);
      var enumType = new TypeSyntax(typeParts, typeDots);
      var finalDot = dots[^1];
      var variant = identifiers[^1];

      if (Current.Kind == SyntaxKind.LeftParen)
      {
        var openParen = NextToken();
        var bindings = new List<PatternBindingSyntax>();
        var separators = new List<SyntaxToken>();
        while (Current.Kind != SyntaxKind.RightParen &&
               Current.Kind != SyntaxKind.EndOfFile &&
               Current.Kind != SyntaxKind.FatArrowToken)
        {
          bindings.Add(ParsePatternBinding(SyntaxKind.RightParen));
          if (Current.Kind != SyntaxKind.Comma)
            break;
          separators.Add(NextToken());
          if (Current.Kind == SyntaxKind.RightParen)
            break;
        }
        var closeParen = MatchToken(SyntaxKind.RightParen);
        return new EnumTupleVariantPatternSyntax(
            enumType,
            finalDot,
            variant,
            openParen,
            bindings,
            separators,
            closeParen);
      }

      if (Current.Kind == SyntaxKind.LeftBrace)
      {
        var openBrace = NextToken();
        var fields = new List<PatternBindingSyntax>();
        var separators = new List<SyntaxToken>();
        while (Current.Kind != SyntaxKind.RightBrace &&
               Current.Kind != SyntaxKind.EndOfFile &&
               Current.Kind != SyntaxKind.FatArrowToken)
        {
          fields.Add(ParsePatternBinding(SyntaxKind.RightBrace));
          if (Current.Kind != SyntaxKind.Comma)
            break;
          separators.Add(NextToken());
          if (Current.Kind == SyntaxKind.RightBrace)
            break;
        }
        var closeBrace = MatchToken(SyntaxKind.RightBrace);
        return new EnumStructVariantPatternSyntax(
            enumType,
            finalDot,
            variant,
            openBrace,
            fields,
            separators,
            closeBrace);
      }

      return new EnumUnitVariantPatternSyntax(enumType, finalDot, variant);
    }

    private PatternBindingSyntax ParsePatternBinding(SyntaxKind terminator)
    {
      if (Current.Kind == SyntaxKind.Identifier)
      {
        var identifier = NextToken();
        if (Current.Kind != SyntaxKind.Colon &&
            Current.Kind != SyntaxKind.Dot &&
            Current.Kind != SyntaxKind.LeftParen &&
            Current.Kind != SyntaxKind.LeftBrace)
        {
          return new PatternBindingSyntax(identifier);
        }

        Diagnostics.ReportUnsupportedPatternForm(
            identifier.Span,
            identifier.Text);
        SkipPatternPayloadItem(terminator);
        return new PatternBindingSyntax(identifier, isSupported: false);
      }

      var unsupported = NextToken();
      Diagnostics.ReportUnsupportedPatternForm(
          unsupported.Span,
          unsupported.Text);
      SkipPatternPayloadItem(terminator);
      return new PatternBindingSyntax(unsupported, isSupported: false);
    }

    private void SkipPatternPayloadItem(SyntaxKind terminator)
    {
      var parenDepth = 0;
      var braceDepth = 0;
      while (Current.Kind != SyntaxKind.EndOfFile)
      {
        if (parenDepth == 0 && braceDepth == 0 &&
            (Current.Kind == SyntaxKind.Comma || Current.Kind == terminator))
        {
          return;
        }

        if (Current.Kind == SyntaxKind.LeftParen)
          parenDepth++;
        else if (Current.Kind == SyntaxKind.RightParen)
        {
          if (parenDepth == 0)
            return;
          parenDepth--;
        }
        else if (Current.Kind == SyntaxKind.LeftBrace)
          braceDepth++;
        else if (Current.Kind == SyntaxKind.RightBrace)
        {
          if (braceDepth == 0)
            return;
          braceDepth--;
        }

        NextToken();
      }
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
      var condition = ParseControlCondition();
      var body = ParseRequiredControlBlock(whileKeyword);
      return new WhileExpressionSyntax(label, whileKeyword, condition, body);
    }

    private ExpressionSyntax ParseControlCondition()
    {
      _suppressAggregateInitializerDepth++;
      try
      {
        return ParseExpression();
      }
      finally
      {
        _suppressAggregateInitializerDepth--;
      }
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

    private ExpressionSyntax ParseParenthesizedExpression()
    {
      var openParenToken = MatchToken(SyntaxKind.LeftParen);
      var elements = new List<ExpressionSyntax>();
      var separators = new List<SyntaxToken>();

      if (Current.Kind == SyntaxKind.RightParen)
      {
        return new TupleExpressionSyntax(
            openParenToken,
            elements,
            separators,
            NextToken());
      }

      var expression = ParseExpression();
      if (Current.Kind != SyntaxKind.Comma)
      {
        var parenthesizedClose = MatchToken(SyntaxKind.RightParen);
        return new ParenthesizedExpressionSyntax(
            openParenToken,
            expression,
            parenthesizedClose);
      }

      elements.Add(expression);
      while (Current.Kind == SyntaxKind.Comma)
      {
        separators.Add(NextToken());
        if (Current.Kind == SyntaxKind.RightParen)
          break;
        elements.Add(ParseExpression());
      }

      var closeParenToken = MatchToken(SyntaxKind.RightParen);
      return new TupleExpressionSyntax(
          openParenToken,
          elements,
          separators,
          closeParenToken);
    }

    private ArrayLiteralExpressionSyntax ParseArrayLiteralExpression()
    {
      var openBracketToken = MatchToken(SyntaxKind.LeftBracket);
      var elements = new List<ExpressionSyntax>();
      var separators = new List<SyntaxToken>();

      if (Current.Kind == SyntaxKind.RightBracket)
      {
        return new ArrayLiteralExpressionSyntax(
            openBracketToken,
            elements,
            separators,
            NextToken());
      }

      elements.Add(ParseExpression());
      if (Current.Kind == SyntaxKind.Semicolon)
      {
        var repeatSeparator = NextToken();
        var repeatLength = ParseExpression();
        var repeatCloseBracket = MatchToken(SyntaxKind.RightBracket);
        return new ArrayLiteralExpressionSyntax(
            openBracketToken,
            elements,
            separators,
            repeatCloseBracket,
            repeatSeparator,
            repeatLength);
      }

      while (Current.Kind != SyntaxKind.RightBracket &&
             Current.Kind != SyntaxKind.EndOfFile)
      {
        if (Current.Kind != SyntaxKind.Comma)
          break;

        separators.Add(NextToken());
        if (Current.Kind == SyntaxKind.RightBracket)
          break;

        elements.Add(ParseExpression());
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

    private AggregateInitializerExpressionSyntax ParseAggregateInitializerExpression(
        ExpressionSyntax target)
    {
      var openBrace = MatchToken(SyntaxKind.LeftBrace);
      var fields = new List<AggregateInitializerFieldSyntax>();
      while (Current.Kind != SyntaxKind.RightBrace &&
             Current.Kind != SyntaxKind.EndOfFile &&
             !IsMemberStart(Current.Kind))
      {
        var start = _position;
        var identifier = MatchToken(SyntaxKind.Identifier);
        var colon = MatchToken(SyntaxKind.Colon);
        var expression = ParseExpression();
        SyntaxToken comma = null;
        if (Current.Kind == SyntaxKind.Comma)
          comma = NextToken();
        else if (Current.Kind != SyntaxKind.RightBrace)
          comma = MatchToken(SyntaxKind.Comma);

        fields.Add(new AggregateInitializerFieldSyntax(
            identifier,
            colon,
            expression,
            comma));
        if (_position == start)
          NextToken();
      }

      var closeBrace = MatchToken(SyntaxKind.RightBrace);
      return new AggregateInitializerExpressionSyntax(
          target,
          openBrace,
          fields,
          closeBrace);
    }

    private ExpressionSyntax ParsePostfixExpression()
    {
      ExpressionSyntax expression = ParsePrimaryExpression();

      while (true)
      {
        if (Current.Kind == SyntaxKind.LessToken &&
            CanParseExpressionTypeArgumentList())
        {
          expression = new GenericTypeExpressionSyntax(
              expression,
              ParseTypeArgumentList());
          continue;
        }

        if (Current.Kind == SyntaxKind.Dot)
        {
          var dot = NextToken();
          var name = ParseMemberNameToken();
          var questionToken = ParseCallableQuestionSuffix(name);
          expression = new MemberAccessExpressionSyntax(
              expression,
              dot,
              name,
              questionToken);
          continue;
        }

        if (Current.Kind == SyntaxKind.LeftParen)
        {
          expression = ParseCallExpression(expression);
          continue;
        }

        if (Current.Kind == SyntaxKind.LeftBracket)
        {
          var openBracket = NextToken();
          var index = ParseExpression();
          var closeBracket = MatchToken(SyntaxKind.RightBracket);
          expression = new ElementAccessExpressionSyntax(
              expression,
              openBracket,
              index,
              closeBracket);
          continue;
        }

        if (Current.Kind == SyntaxKind.LeftBrace &&
            _suppressAggregateInitializerDepth == 0 &&
            (expression is NameExpressionSyntax ||
             expression is MemberAccessExpressionSyntax ||
             expression is GenericTypeExpressionSyntax))
        {
          expression = ParseAggregateInitializerExpression(expression);
          continue;
        }

        break;
      }

      return expression;
    }

    private bool CanParseExpressionTypeArgumentList()
    {
      var depth = 0;
      for (var offset = 0; ; offset++)
      {
        var token = Peek(offset);
        if (token.Kind == SyntaxKind.EndOfFile)
          return false;

        if (token.Kind == SyntaxKind.LessToken)
        {
          depth++;
          continue;
        }

        if (token.Kind == SyntaxKind.GreaterToken)
          depth--;
        else if (token.Kind == SyntaxKind.GreaterGreaterToken)
          depth -= 2;
        else
          continue;

        if (depth != 0)
          continue;

        var following = Peek(offset + 1).Kind;
        return following == SyntaxKind.Dot ||
            following == SyntaxKind.LeftBrace;
      }
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

    private AggregateFieldDeclarationSyntax ParseAggregateFieldDeclaration()
    {
      var identifier = MatchToken(SyntaxKind.Identifier);
      var colon = MatchToken(SyntaxKind.Colon);
      var type = ParseTypeSyntax();
      SyntaxToken comma = null;
      if (Current.Kind == SyntaxKind.Comma)
        comma = NextToken();
      else if (Current.Kind != SyntaxKind.RightBrace)
        comma = MatchToken(SyntaxKind.Comma);

      return new AggregateFieldDeclarationSyntax(identifier, colon, type, comma);
    }

    private StructDeclarationSyntax ParseStructDeclaration()
    {
      var pubKeyword = Current.Kind == SyntaxKind.PubKeyword ? NextToken() : null;
      var structKeyword = MatchToken(SyntaxKind.StructKeyword);
      var identifier = MatchToken(SyntaxKind.Identifier);
      var genericParameters = Current.Kind == SyntaxKind.LessToken
          ? ParseGenericParameterList()
          : null;
      var openBrace = MatchToken(SyntaxKind.LeftBrace);
      var fields = new List<AggregateFieldDeclarationSyntax>();
      while (Current.Kind != SyntaxKind.RightBrace &&
             Current.Kind != SyntaxKind.EndOfFile &&
             !IsMemberStart(Current.Kind))
      {
        var start = _position;
        fields.Add(ParseAggregateFieldDeclaration());
        if (_position == start)
          NextToken();
      }

      var closeBrace = MatchToken(SyntaxKind.RightBrace);
      return new StructDeclarationSyntax(
          pubKeyword,
          structKeyword,
          identifier,
          genericParameters,
          openBrace,
          fields,
          closeBrace);
    }

    private EnumVariantDeclarationSyntax ParseEnumVariantDeclaration()
    {
      var identifier = MatchToken(SyntaxKind.Identifier);
      var kind = EnumVariantSyntaxKind.Unit;
      SyntaxToken openParen = null;
      SyntaxToken closeParen = null;
      SyntaxToken openBrace = null;
      SyntaxToken closeBrace = null;
      var tupleTypes = new List<TypeSyntax>();
      var tupleSeparators = new List<SyntaxToken>();
      var namedFields = new List<AggregateFieldDeclarationSyntax>();

      if (Current.Kind == SyntaxKind.LeftParen)
      {
        kind = EnumVariantSyntaxKind.Tuple;
        openParen = NextToken();
        while (Current.Kind != SyntaxKind.RightParen &&
               Current.Kind != SyntaxKind.EndOfFile &&
               Current.Kind != SyntaxKind.RightBrace &&
               !IsMemberStart(Current.Kind))
        {
          tupleTypes.Add(ParseTypeSyntax());
          if (Current.Kind != SyntaxKind.Comma)
            break;
          tupleSeparators.Add(NextToken());
        }
        closeParen = MatchToken(SyntaxKind.RightParen);
      }
      else if (Current.Kind == SyntaxKind.LeftBrace)
      {
        kind = EnumVariantSyntaxKind.Struct;
        openBrace = NextToken();
        while (Current.Kind != SyntaxKind.RightBrace &&
               Current.Kind != SyntaxKind.EndOfFile &&
               !IsMemberStart(Current.Kind))
        {
          var start = _position;
          namedFields.Add(ParseAggregateFieldDeclaration());
          if (_position == start)
            NextToken();
        }
        closeBrace = MatchToken(SyntaxKind.RightBrace);
      }

      SyntaxToken comma = null;
      if (Current.Kind == SyntaxKind.Comma)
        comma = NextToken();
      else if (Current.Kind != SyntaxKind.RightBrace)
        comma = MatchToken(SyntaxKind.Comma);

      return new EnumVariantDeclarationSyntax(
          identifier,
          kind,
          openParen,
          tupleTypes,
          tupleSeparators,
          closeParen,
          openBrace,
          namedFields,
          closeBrace,
          comma);
    }

    private EnumDeclarationSyntax ParseEnumDeclaration()
    {
      var pubKeyword = Current.Kind == SyntaxKind.PubKeyword ? NextToken() : null;
      var enumKeyword = MatchToken(SyntaxKind.EnumKeyword);
      var identifier = MatchToken(SyntaxKind.Identifier);
      var genericParameters = Current.Kind == SyntaxKind.LessToken
          ? ParseGenericParameterList()
          : null;
      var openBrace = MatchToken(SyntaxKind.LeftBrace);
      var variants = new List<EnumVariantDeclarationSyntax>();
      while (Current.Kind != SyntaxKind.RightBrace &&
             Current.Kind != SyntaxKind.EndOfFile &&
             !IsMemberStart(Current.Kind))
      {
        var start = _position;
        variants.Add(ParseEnumVariantDeclaration());
        if (_position == start)
          NextToken();
      }

      var closeBrace = MatchToken(SyntaxKind.RightBrace);
      return new EnumDeclarationSyntax(
          pubKeyword,
          enumKeyword,
          identifier,
          genericParameters,
          openBrace,
          variants,
          closeBrace);
    }

    private static bool IsMemberStart(SyntaxKind kind)
    {
      return kind == SyntaxKind.FnKeyword ||
          kind == SyntaxKind.ReceiveKeyword ||
          kind == SyntaxKind.On ||
          kind == SyntaxKind.UseKeyword ||
          kind == SyntaxKind.ModKeyword ||
          kind == SyntaxKind.ImplKeyword ||
          kind == SyntaxKind.StructKeyword ||
          kind == SyntaxKind.EnumKeyword ||
          kind == SyntaxKind.ConstKeyword ||
          kind == SyntaxKind.StateKeyword ||
          kind == SyntaxKind.LetKeyword ||
          kind == SyntaxKind.SyncKeyword ||
          kind == SyntaxKind.PubKeyword;
    }

    private TypeClauseSyntax ParseTypeClause()
    {
      var colonToken = MatchToken(SyntaxKind.Colon);
      var type = ParseTypeSyntax();
      return new TypeClauseSyntax(colonToken, type);
    }

    private TypeSyntax ParseTypeSyntax()
    {
      if (Current.Kind == SyntaxKind.LeftParen)
      {
        var openParen = NextToken();
        var elements = new List<TypeSyntax>();
        var separators = new List<SyntaxToken>();
        if (Current.Kind == SyntaxKind.RightParen)
        {
          return new TypeSyntax(
              openParen,
              elements,
              separators,
              NextToken());
        }

        var first = ParseTypeSyntax();
        if (Current.Kind != SyntaxKind.Comma)
        {
          MatchToken(SyntaxKind.RightParen);
          return first;
        }

        elements.Add(first);
        while (Current.Kind == SyntaxKind.Comma)
        {
          separators.Add(NextToken());
          if (Current.Kind == SyntaxKind.RightParen)
            break;
          elements.Add(ParseTypeSyntax());
        }

        return new TypeSyntax(
            openParen,
            elements,
            separators,
            MatchToken(SyntaxKind.RightParen));
      }

      if (Current.Kind == SyntaxKind.LeftBracket)
      {
        var openBracket = NextToken();
        var elementType = ParseTypeSyntax();
        var closeBracket = MatchToken(SyntaxKind.RightBracket);
        return new TypeSyntax(openBracket, elementType, closeBracket);
      }

      var parts = new List<SyntaxToken>();
      var dots = new List<SyntaxToken>();

      parts.Add(ParseTypeIdentifierToken());
      RejectQuestionMarkInName("type");

      while (Current.Kind == SyntaxKind.Dot)
      {
        dots.Add(NextToken());
        parts.Add(MatchToken(SyntaxKind.Identifier));
        RejectQuestionMarkInName("type");
      }

      var typeArguments = Current.Kind == SyntaxKind.LessToken
          ? ParseTypeArgumentList()
          : null;
      return new TypeSyntax(parts, dots, typeArguments);
    }

    private GenericParameterListSyntax ParseGenericParameterList()
    {
      var lessToken = MatchToken(SyntaxKind.LessToken);
      var parameters = new List<SyntaxToken>();
      var separators = new List<SyntaxToken>();
      while (Current.Kind != SyntaxKind.GreaterToken &&
             Current.Kind != SyntaxKind.GreaterGreaterToken &&
             Current.Kind != SyntaxKind.EndOfFile)
      {
        parameters.Add(MatchToken(SyntaxKind.Identifier));
        if (Current.Kind != SyntaxKind.Comma)
          break;
        separators.Add(NextToken());
      }

      return new GenericParameterListSyntax(
          lessToken,
          parameters,
          separators,
          MatchTypeArgumentGreaterToken());
    }

    private TypeArgumentListSyntax ParseTypeArgumentList()
    {
      var lessToken = MatchToken(SyntaxKind.LessToken);
      var arguments = new List<TypeSyntax>();
      var separators = new List<SyntaxToken>();
      while (Current.Kind != SyntaxKind.GreaterToken &&
             Current.Kind != SyntaxKind.GreaterGreaterToken &&
             Current.Kind != SyntaxKind.EndOfFile)
      {
        arguments.Add(ParseTypeSyntax());
        if (Current.Kind != SyntaxKind.Comma)
          break;
        separators.Add(NextToken());
      }

      return new TypeArgumentListSyntax(
          lessToken,
          arguments,
          separators,
          MatchTypeArgumentGreaterToken());
    }

    private SyntaxToken ParseTypeIdentifierToken()
    {
      if (Current.Kind == SyntaxKind.Identifier ||
          Current.Kind == SyntaxKind.SelfTypeKeyword)
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
          Current.Kind != SyntaxKind.ConstKeyword &&
          Current.Kind != SyntaxKind.StateKeyword &&
          Current.Kind != SyntaxKind.LetKeyword)
      {
        Diagnostics.ReportSynchronizationModeArgumentCount(Current.Span);
        while (Current.Kind != SyntaxKind.RightParen &&
               Current.Kind != SyntaxKind.EndOfFile &&
               Current.Kind != SyntaxKind.ConstKeyword &&
               Current.Kind != SyntaxKind.StateKeyword &&
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

      var stateKeyword = MatchToken(SyntaxKind.StateKeyword);
      ConsumeMisplacedStateModifiers();

      SyntaxToken mutKeyword = null;
      if (Current.Kind == SyntaxKind.MutKeyword)
      {
        mutKeyword = NextToken();
        Diagnostics.ReportStateCannotUseMut(mutKeyword.Span);
      }

      ConsumeMisplacedStateModifiers();
      var identifier = MatchToken(SyntaxKind.Identifier);
      RejectQuestionMarkInName("top-level state");

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
          stateKeyword,
          mutKeyword,
          identifier,
          typeClause,
          equalsToken,
          initializer,
          semicolon);
    }

    private ConstDeclarationSyntax ParseConstDeclaration()
    {
      SyntaxToken pubKeyword = null;
      SynchronizationModifierSyntax synchronizationModifier = null;

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
          continue;
        }

        var currentSynchronizationModifier = ParseSynchronizationModifier();
        Diagnostics.ReportSynchronizationOnlyOnState(
            currentSynchronizationModifier.SyncKeyword.Span);
        synchronizationModifier ??= currentSynchronizationModifier;
      }

      var constKeyword = MatchToken(SyntaxKind.ConstKeyword);
      var identifier = MatchToken(SyntaxKind.Identifier);
      RejectQuestionMarkInName("constant");

      TypeClauseSyntax typeClause = null;
      if (Current.Kind == SyntaxKind.Colon)
        typeClause = ParseTypeClause();

      SyntaxToken equalsToken = null;
      ExpressionSyntax initializer = null;
      if (Current.Kind == SyntaxKind.EqualsToken)
      {
        equalsToken = NextToken();
        if (Current.Kind != SyntaxKind.Semicolon)
          initializer = ParseExpression();
      }

      if (initializer == null)
      {
        Diagnostics.ReportMissingConstantInitializer(
            identifier.Span,
            identifier.Text ?? string.Empty);
      }

      var semicolon = MatchToken(SyntaxKind.Semicolon);
      return new ConstDeclarationSyntax(
          pubKeyword,
          synchronizationModifier,
          constKeyword,
          identifier,
          typeClause,
          equalsToken,
          initializer,
          semicolon);
    }

    private LegacyTopLevelLetDeclarationSyntax ParseLegacyTopLevelLetDeclaration()
    {
      var firstToken = Current;
      while (Current.Kind == SyntaxKind.PubKeyword ||
             Current.Kind == SyntaxKind.SyncKeyword)
      {
        if (Current.Kind == SyntaxKind.SyncKeyword)
          ParseSynchronizationModifier();
        else
          NextToken();
      }

      var letKeyword = MatchToken(SyntaxKind.LetKeyword);
      Diagnostics.ReportTopLevelLetNoLongerSupported(letKeyword.Span);
      if (Current.Kind == SyntaxKind.MutKeyword)
        NextToken();
      MatchToken(SyntaxKind.Identifier);
      if (Current.Kind == SyntaxKind.Colon)
        ParseTypeClause();
      if (Current.Kind == SyntaxKind.EqualsToken)
      {
        NextToken();
        if (Current.Kind != SyntaxKind.Semicolon)
          ParseExpression();
      }
      var semicolon = MatchToken(SyntaxKind.Semicolon);
      return new LegacyTopLevelLetDeclarationSyntax(
          firstToken,
          letKeyword,
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

      var pattern = ParseBindingPattern();
      if (pattern is NameBindingPatternSyntax)
        RejectQuestionMarkInName("local variable");

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
          pattern,
          typeClause,
          equalsToken,
          initializer,
          semicolon);
    }

    private BindingPatternSyntax ParseBindingPattern()
    {
      if (Current.Kind != SyntaxKind.LeftParen)
        return new NameBindingPatternSyntax(MatchToken(SyntaxKind.Identifier));

      var openParen = NextToken();
      var elements = new List<BindingPatternSyntax>();
      var separators = new List<SyntaxToken>();
      if (Current.Kind == SyntaxKind.RightParen)
      {
        return new TupleBindingPatternSyntax(
            openParen,
            elements,
            separators,
            NextToken());
      }

      var first = ParseBindingPattern();
      if (Current.Kind != SyntaxKind.Comma)
      {
        MatchToken(SyntaxKind.RightParen);
        return first;
      }

      elements.Add(first);
      while (Current.Kind == SyntaxKind.Comma)
      {
        separators.Add(NextToken());
        if (Current.Kind == SyntaxKind.RightParen)
          break;
        elements.Add(ParseBindingPattern());
      }

      return new TupleBindingPatternSyntax(
          openParen,
          elements,
          separators,
          MatchToken(SyntaxKind.RightParen));
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

    private SendStatementSyntax ParseSendStatement()
    {
      var sendKeyword = MatchToken(SyntaxKind.SendKeyword);
      var receiverName = MatchToken(SyntaxKind.Identifier);
      RejectQuestionMarkInName("network receiver");
      SyntaxToken openParen = null;
      SyntaxToken closeParen = null;
      var arguments = new List<ExpressionSyntax>();
      var separators = new List<SyntaxToken>();

      if (Current.Kind == SyntaxKind.LeftParen)
      {
        openParen = NextToken();
        if (Current.Kind != SyntaxKind.RightParen &&
            Current.Kind != SyntaxKind.EndOfFile)
        {
          while (true)
          {
            arguments.Add(ParseExpression());
            if (Current.Kind != SyntaxKind.Comma)
              break;
            separators.Add(NextToken());
          }
        }

        closeParen = MatchToken(SyntaxKind.RightParen);
      }

      var toKeyword = MatchToken(SyntaxKind.ToKeyword);
      var target = ParseExpression();
      var semicolon = MatchToken(SyntaxKind.Semicolon);
      return new SendStatementSyntax(
          sendKeyword,
          receiverName,
          openParen,
          arguments,
          separators,
          closeParen,
          toKeyword,
          target,
          semicolon);
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
        case SyntaxKind.MatchKeyword:
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
        case SyntaxKind.ExternKeyword:
        case SyntaxKind.NewKeyword:
        case SyntaxKind.LeftBracket:
        case SyntaxKind.Identifier:
        case SyntaxKind.SelfKeyword:
        case SyntaxKind.SelfTypeKeyword:
        case SyntaxKind.PlusToken:
        case SyntaxKind.MinusToken:
        case SyntaxKind.BangToken:
        case SyntaxKind.TildeToken:
          return true;

        default:
          return false;
      }
    }

    private SyntaxToken ParseMemberNameToken()
    {
      return Current.Kind == SyntaxKind.NewKeyword ||
             Current.Kind == SyntaxKind.SelfTypeKeyword ||
             Current.Kind == SyntaxKind.Int32Literal
          ? NextToken()
          : MatchToken(SyntaxKind.Identifier);
    }

    private StatementSyntax ParseStatement()
    {
      if (Current.Kind == SyntaxKind.LeftBrace)
        return ParseBlockStatement();

      if (Current.Kind == SyntaxKind.ModKeyword ||
          Current.Kind == SyntaxKind.PubKeyword &&
          Peek(1).Kind == SyntaxKind.ModKeyword)
      {
        var declarationStart = Current;
        Diagnostics.ReportModMustBeTopLevel(declarationStart.Span);
        var declaration = ParseModDeclaration();
        return new ExpressionStatementSyntax(
            new NameExpressionSyntax(declaration.ModKeyword),
            declaration.SemicolonToken);
      }

      if (Current.Kind == SyntaxKind.PubKeyword ||
          Current.Kind == SyntaxKind.SyncKeyword)
      {
        return ParseInvalidLocalStateDeclaration();
      }

      if (Current.Kind == SyntaxKind.LetKeyword)
        return ParseVariableDeclarationStatement();

      if (Current.Kind == SyntaxKind.ConstKeyword ||
          Current.Kind == SyntaxKind.StateKeyword)
      {
        return ParseInvalidLocalDeclaration();
      }

      if (Current.Kind == SyntaxKind.ReturnKeyword)
        return ParseReturnStatement();

      if (Current.Kind == SyntaxKind.SendKeyword)
        return ParseSendStatement();

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

      if (Current.Kind == SyntaxKind.ConstKeyword ||
          Current.Kind == SyntaxKind.StateKeyword)
      {
        return ParseInvalidLocalDeclaration();
      }

      return ParseExpressionStatement();
    }

    private InvalidLocalDeclarationStatementSyntax ParseInvalidLocalDeclaration()
    {
      var keyword = NextToken();
      Diagnostics.ReportDeclarationMustBeTopLevel(
          keyword.Span,
          keyword.Text ?? string.Empty);
      if (Current.Kind == SyntaxKind.MutKeyword)
        NextToken();
      MatchToken(SyntaxKind.Identifier);
      if (Current.Kind == SyntaxKind.Colon)
        ParseTypeClause();
      if (Current.Kind == SyntaxKind.EqualsToken)
      {
        NextToken();
        if (Current.Kind != SyntaxKind.Semicolon)
          ParseExpression();
      }
      var semicolon = MatchToken(SyntaxKind.Semicolon);
      return new InvalidLocalDeclarationStatementSyntax(keyword, semicolon);
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
             expression is MatchExpressionSyntax ||
             expression is WhileExpressionSyntax ||
             expression is LoopExpressionSyntax;
    }

    private ParameterSyntax ParseParameter()
    {
      var parameterName = Current.Kind == SyntaxKind.SelfKeyword
          ? NextToken()
          : MatchToken(SyntaxKind.Identifier);
      RejectQuestionMarkInName("parameter");
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
        if (Current.Kind == SyntaxKind.RightParen)
          break;
      }
    }

    private FunctionReturnTypeSyntax ParseFunctionReturnType()
    {
      var arrowToken = MatchToken(SyntaxKind.ArrowToken);
      var type = ParseTypeSyntax();
      return new FunctionReturnTypeSyntax(arrowToken, type);
    }

    private static bool IsOperatorFunctionName(SyntaxKind kind)
    {
      switch (kind)
      {
        case SyntaxKind.PlusToken:
        case SyntaxKind.MinusToken:
        case SyntaxKind.StarToken:
        case SyntaxKind.SlashToken:
        case SyntaxKind.PercentToken:
        case SyntaxKind.EqualsEqualsToken:
        case SyntaxKind.BangEqualsToken:
        case SyntaxKind.LessToken:
        case SyntaxKind.LessOrEqualsToken:
        case SyntaxKind.GreaterToken:
        case SyntaxKind.GreaterOrEqualsToken:
        case SyntaxKind.BangToken:
        case SyntaxKind.TildeToken:
        case SyntaxKind.AmpersandToken:
        case SyntaxKind.PipeToken:
        case SyntaxKind.CaretToken:
        case SyntaxKind.LessLessToken:
        case SyntaxKind.GreaterGreaterToken:
        case SyntaxKind.AmpersandAmpersandToken:
        case SyntaxKind.PipePipeToken:
        case SyntaxKind.EqualsToken:
        case SyntaxKind.PlusEqualsToken:
        case SyntaxKind.MinusEqualsToken:
        case SyntaxKind.StarEqualsToken:
        case SyntaxKind.SlashEqualsToken:
        case SyntaxKind.PercentEqualsToken:
        case SyntaxKind.AmpersandEqualsToken:
        case SyntaxKind.PipeEqualsToken:
        case SyntaxKind.CaretEqualsToken:
        case SyntaxKind.LessLessEqualsToken:
        case SyntaxKind.GreaterGreaterEqualsToken:
          return true;

        default:
          return false;
      }
    }

    private FunctionDeclarationSyntax ParseFunctionDeclaration()
    {
      SyntaxToken pubKeyword = null;
      if (Current.Kind == SyntaxKind.PubKeyword)
        pubKeyword = NextToken();

      SyntaxToken staticKeyword = null;
      if (Current.Kind == SyntaxKind.StaticKeyword)
        staticKeyword = NextToken();

      var fnKeyword = MatchToken(SyntaxKind.FnKeyword);
      SyntaxToken atToken = null;
      SyntaxToken operatorToken = null;
      SyntaxToken identifier = null;
      SyntaxToken questionToken = null;

      if (Current.Kind == SyntaxKind.AtToken)
      {
        atToken = NextToken();
        if (IsOperatorFunctionName(Current.Kind))
          operatorToken = NextToken();
        else
          operatorToken = MatchToken(SyntaxKind.PlusToken);
      }
      else if (IsOperatorFunctionName(Current.Kind))
      {
        operatorToken = NextToken();
      }
      else if (Current.Kind == SyntaxKind.NewKeyword)
      {
        identifier = NextToken();
      }
      else if (Current.Kind == SyntaxKind.SelfTypeKeyword)
      {
        identifier = NextToken();
      }
      else
      {
        identifier = MatchToken(SyntaxKind.Identifier);
        questionToken = ParseCallableQuestionSuffix(identifier);
      }

      var parameters = new List<ParameterSyntax>();
      var separators = new List<SyntaxToken>();
      ParseOptionalParameterList(
          "function",
          SyntaxKind.ArrowToken,
          true,
          parameters,
          separators,
          out var openParenToken,
          out var closeParenToken);
      FunctionReturnTypeSyntax returnTypeAnnotation = null;
      if (Current.Kind == SyntaxKind.ArrowToken)
        returnTypeAnnotation = ParseFunctionReturnType();

      BlockStatementSyntax body = null;
      ExternalFunctionBindingSyntax externalBinding = null;
      if (Current.Kind == SyntaxKind.EqualsToken)
        externalBinding = ParseExternalFunctionBinding();
      else
        body = ParseBlockStatement(allowTrailingExpression: true);

      return new FunctionDeclarationSyntax(
          pubKeyword,
          staticKeyword,
          fnKeyword,
          identifier,
          questionToken,
          atToken,
          operatorToken,
          openParenToken,
          parameters,
          separators,
          closeParenToken,
          returnTypeAnnotation,
          body,
          externalBinding);
    }

    private ExternalFunctionBindingSyntax ParseExternalFunctionBinding()
    {
      var equalsToken = MatchToken(SyntaxKind.EqualsToken);
      SyntaxToken maybeKeyword = null;
      if (Current.Kind == SyntaxKind.Identifier &&
          string.Equals(Current.Text, "maybe", StringComparison.Ordinal))
        maybeKeyword = NextToken();

      if (Current.Kind != SyntaxKind.ExternKeyword)
      {
        var start = maybeKeyword?.Span.Start ?? Current.Span.Start;
        _ = ParseExpression();
        Diagnostics.ReportInvalidExternalFunctionBinding(
            TextSpan.FromBounds(start, Current.Span.Start));
        return new ExternalFunctionBindingSyntax(
            equalsToken,
            maybeKeyword,
            null,
            isMalformed: true);
      }

      var externKeyword = NextToken();
      if (LooksLikeExternalAbiSignature())
      {
        var abiSignature = ParseExternalAbiSignature();
        SyntaxToken abiSemicolon = null;
        if (Current.Kind == SyntaxKind.Semicolon)
          abiSemicolon = NextToken();
        return new ExternalFunctionBindingSyntax(
            equalsToken,
            maybeKeyword,
            null,
            isMalformed: false,
            abiSignature: abiSignature,
            semicolonToken: abiSemicolon);
      }

      var target = ParseExpression();
      SyntaxToken semicolon = null;
      if (Current.Kind == SyntaxKind.Semicolon)
        semicolon = NextToken();
      return new ExternalFunctionBindingSyntax(
          equalsToken,
          maybeKeyword,
          new ExternExpressionSyntax(externKeyword, target),
          isMalformed: false,
          semicolonToken: semicolon);
    }

    private bool LooksLikeExternalAbiSignature()
    {
      var openParenOffset = 0;
      var firstTargetKind = Peek(openParenOffset).Kind;
      if (firstTargetKind != SyntaxKind.Identifier &&
          firstTargetKind != SyntaxKind.SelfKeyword &&
          firstTargetKind != SyntaxKind.SelfTypeKeyword)
      {
        return false;
      }

      openParenOffset++;
      while (Peek(openParenOffset).Kind == SyntaxKind.Dot)
      {
        if (Peek(openParenOffset + 1).Kind != SyntaxKind.Identifier)
          return false;
        openParenOffset += 2;
      }

      if (Peek(openParenOffset).Kind != SyntaxKind.LeftParen)
        return false;

      var first = Peek(openParenOffset + 1);
      if (first.Kind == SyntaxKind.RightParen)
        return false;
      if (first.Kind == SyntaxKind.RefKeyword || first.Kind == SyntaxKind.OutKeyword)
        return true;

      var lastKind = SyntaxKind.BadToken;
      var previousKind = SyntaxKind.BadToken;
      var tokenCount = 0;
      for (var offset = openParenOffset + 1; ; offset++)
      {
        var token = Peek(offset);
        if (token.Kind == SyntaxKind.Comma ||
            token.Kind == SyntaxKind.RightParen ||
            token.Kind == SyntaxKind.EndOfFile)
        {
          break;
        }
        if (token.Kind == SyntaxKind.RefKeyword || token.Kind == SyntaxKind.OutKeyword)
          return true;
        previousKind = lastKind;
        lastKind = token.Kind;
        tokenCount++;
      }

      return tokenCount >= 2 &&
          lastKind == SyntaxKind.Identifier &&
          previousKind != SyntaxKind.Dot;
    }

    private ExternalAbiSignatureSyntax ParseExternalAbiSignature()
    {
      ExpressionSyntax target = new NameExpressionSyntax(
          Current.Kind == SyntaxKind.SelfKeyword ||
          Current.Kind == SyntaxKind.SelfTypeKeyword
              ? NextToken()
              : MatchToken(SyntaxKind.Identifier));
      while (Current.Kind == SyntaxKind.Dot)
      {
        var dot = NextToken();
        target = new MemberAccessExpressionSyntax(
            target,
            dot,
            ParseMemberNameToken(),
            null);
      }

      var openParen = MatchToken(SyntaxKind.LeftParen);
      var parameters = new List<ExternalAbiParameterSyntax>();
      var separators = new List<SyntaxToken>();
      while (Current.Kind != SyntaxKind.RightParen &&
             Current.Kind != SyntaxKind.EndOfFile)
      {
        SyntaxToken modifier = null;
        if (Current.Kind == SyntaxKind.RefKeyword ||
            Current.Kind == SyntaxKind.OutKeyword)
        {
          modifier = NextToken();
        }
        var type = ParseTypeSyntax();
        var identifier = MatchToken(SyntaxKind.Identifier);
        parameters.Add(new ExternalAbiParameterSyntax(modifier, type, identifier));
        if (Current.Kind != SyntaxKind.Comma)
          break;
        separators.Add(NextToken());
        if (Current.Kind == SyntaxKind.RightParen)
          break;
      }

      return new ExternalAbiSignatureSyntax(
          target,
          openParen,
          parameters,
          separators,
          MatchToken(SyntaxKind.RightParen));
    }

    private ImplDeclarationSyntax ParseImplDeclaration()
    {
      SyntaxToken pubKeyword = null;
      if (Current.Kind == SyntaxKind.PubKeyword)
        pubKeyword = NextToken();

      var implKeyword = MatchToken(SyntaxKind.ImplKeyword);
      var genericParameters = Current.Kind == SyntaxKind.LessToken
          ? ParseGenericParameterList()
          : null;
      var targetType = ParseTypeSyntax();
      SyntaxToken equalsToken = null;
      SyntaxToken externKeyword = null;
      QualifiedNameSyntax externalTypeName = null;

      if (Current.Kind == SyntaxKind.EqualsToken)
      {
        equalsToken = NextToken();
        externKeyword = MatchToken(SyntaxKind.ExternKeyword);
        externalTypeName = ParseQualifiedName(out _);
      }

      var openBrace = MatchToken(SyntaxKind.LeftBrace);
      var methods = new List<FunctionDeclarationSyntax>();

      while (Current.Kind != SyntaxKind.RightBrace &&
             Current.Kind != SyntaxKind.EndOfFile)
      {
        var start = _position;
        if (Current.Kind == SyntaxKind.PubKeyword ||
            Current.Kind == SyntaxKind.StaticKeyword ||
            Current.Kind == SyntaxKind.FnKeyword)
        {
          methods.Add(ParseFunctionDeclaration());
        }
        else
        {
          Diagnostics.ReportUnexpectedImplMember(Current.Span, Current.Kind);
          NextToken();
        }

        if (_position == start)
          NextToken();
      }

      var closeBrace = MatchToken(SyntaxKind.RightBrace);
      return new ImplDeclarationSyntax(
          pubKeyword,
          implKeyword,
          genericParameters,
          targetType,
          equalsToken,
          externKeyword,
          externalTypeName,
          openBrace,
          methods,
          closeBrace);
    }

    private EventDeclarationSyntax ParseEventDeclaration()
    {
      var onKeyword = MatchToken(SyntaxKind.On);
      var identifier = MatchToken(SyntaxKind.Identifier);
      RejectQuestionMarkInName("event");
      var parameters = new List<ParameterSyntax>();
      var separators = new List<SyntaxToken>();
      ParseOptionalParameterList(
          "event",
          SyntaxKind.Colon,
          false,
          parameters,
          separators,
          out var openParenToken,
          out var closeParenToken);
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

    private ReceiveDeclarationSyntax ParseReceiveDeclaration()
    {
      var receiveKeyword = MatchToken(SyntaxKind.ReceiveKeyword);
      var identifier = MatchToken(SyntaxKind.Identifier);
      RejectQuestionMarkInName("network receiver");
      var parameters = new List<ParameterSyntax>();
      var separators = new List<SyntaxToken>();
      ParseOptionalParameterList(
          "network receiver",
          SyntaxKind.ArrowToken,
          false,
          parameters,
          separators,
          out var openParenToken,
          out var closeParenToken);

      FunctionReturnTypeSyntax rejectedReturnType = null;
      if (Current.Kind == SyntaxKind.ArrowToken)
      {
        rejectedReturnType = ParseFunctionReturnType();
        Diagnostics.ReportReceiveReturnTypeNotAllowed(
            rejectedReturnType.ArrowToken.Span);
      }

      var body = ParseBlockStatement();
      return new ReceiveDeclarationSyntax(
          receiveKeyword,
          identifier,
          openParenToken,
          parameters,
          separators,
          closeParenToken,
          rejectedReturnType,
          body);
    }

    private MemberSyntax ParseMember()
    {
      if (Current.Kind == SyntaxKind.StructKeyword ||
          Current.Kind == SyntaxKind.PubKeyword &&
          Peek(1).Kind == SyntaxKind.StructKeyword)
      {
        return ParseStructDeclaration();
      }

      if (Current.Kind == SyntaxKind.EnumKeyword ||
          Current.Kind == SyntaxKind.PubKeyword &&
          Peek(1).Kind == SyntaxKind.EnumKeyword)
      {
        return ParseEnumDeclaration();
      }

      if (Current.Kind == SyntaxKind.ModKeyword ||
          Current.Kind == SyntaxKind.PubKeyword &&
          Peek(1).Kind == SyntaxKind.ModKeyword)
      {
        return ParseModDeclaration();
      }

      if (Current.Kind == SyntaxKind.UseKeyword ||
          Current.Kind == SyntaxKind.PubKeyword &&
          Peek(1).Kind == SyntaxKind.UseKeyword)
      {
        return ParseUseDirective();
      }

      var declarationKind = PeekModifiedDeclarationKind();
      if (declarationKind == SyntaxKind.ConstKeyword)
        return ParseConstDeclaration();
      if (declarationKind == SyntaxKind.StateKeyword)
        return ParseStateDeclaration();
      if (declarationKind == SyntaxKind.LetKeyword)
        return ParseLegacyTopLevelLetDeclaration();

      if (TryFindModifiedNonStateMember(out var modifiedMemberKind))
      {
        while (Current.Kind == SyntaxKind.PubKeyword ||
               Current.Kind == SyntaxKind.SyncKeyword)
        {
          if (Current.Kind == SyntaxKind.PubKeyword)
          {
            var pubKeyword = NextToken();
            if (modifiedMemberKind != SyntaxKind.FnKeyword)
            {
              Diagnostics.ReportUnsupportedTopLevelModifier(
                  pubKeyword.Span,
                  "pub",
                  modifiedMemberKind);
            }
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

      if (Current.Kind == SyntaxKind.ImplKeyword ||
          Current.Kind == SyntaxKind.PubKeyword &&
          Peek(1).Kind == SyntaxKind.ImplKeyword)
      {
        return ParseImplDeclaration();
      }

      if (Current.Kind == SyntaxKind.FnKeyword ||
          Current.Kind == SyntaxKind.PubKeyword &&
          Peek(1).Kind == SyntaxKind.FnKeyword)
        return ParseFunctionDeclaration();

      if (Current.Kind == SyntaxKind.On)
        return ParseEventDeclaration();

      if (Current.Kind == SyntaxKind.ReceiveKeyword)
        return ParseReceiveDeclaration();

      Diagnostics.ReportUnexpectedMember(Current.Span, Current.Kind);

      var badToken = NextToken();
      return new SkippedMemberSyntax(badToken);
    }

    private SyntaxKind PeekModifiedDeclarationKind()
    {
      var offset = 0;
      while (true)
      {
        if (Peek(offset).Kind == SyntaxKind.PubKeyword)
        {
          offset++;
          continue;
        }

        if (Peek(offset).Kind != SyntaxKind.SyncKeyword)
          return Peek(offset).Kind;

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
    }

    private bool TryFindModifiedNonStateMember(out SyntaxKind memberKind)
    {
      var offset = 0;
      var sawModifier = false;
      var sawSynchronizationModifier = false;
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
        sawSynchronizationModifier = true;
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
          (memberKind == SyntaxKind.On ||
           memberKind == SyntaxKind.ReceiveKeyword ||
           memberKind == SyntaxKind.UseKeyword ||
           memberKind == SyntaxKind.FnKeyword &&
           sawSynchronizationModifier);
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
