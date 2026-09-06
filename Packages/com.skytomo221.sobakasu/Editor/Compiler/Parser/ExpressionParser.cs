using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class ExpressionParser : ParserComponent
    {
        internal ExpressionParser(ParserState state) : base(state) { }

        internal NewExpressionSyntax ParseNewExpression()
        {
            var newKeyword = MatchToken(SyntaxKind.NewKeyword);
            var type = State.TypeParser.ParseTypeSyntax();
            var openParen = MatchToken(SyntaxKind.LeftParen);
            var arguments = new List<ExpressionSyntax>();

            if (Current.Kind != SyntaxKind.RightParen &&
                Current.Kind != SyntaxKind.EndOfFile)
            {
                while (true)
                {
                    arguments.Add(State.ExpressionParser.ParseExpression());
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

        internal ExpressionSyntax ParsePrimaryExpression()
        {
            switch (Current.Kind)
            {
                case SyntaxKind.ExternKeyword:
                    {
                        var externKeyword = NextToken();
                        return new ExternExpressionSyntax(externKeyword, State.ExpressionParser.ParseExpression());
                    }

                case SyntaxKind.NewKeyword:
                    return State.ExpressionParser.ParseNewExpression();

                case SyntaxKind.IfKeyword:
                    return State.ExpressionParser.ParseIfExpression();

                case SyntaxKind.MatchKeyword:
                    return State.ExpressionParser.ParseMatchExpression();

                case SyntaxKind.WhileKeyword:
                    return State.ExpressionParser.ParseWhileExpression(null);

                case SyntaxKind.LoopKeyword:
                    return State.ExpressionParser.ParseLoopExpression(null);

                case SyntaxKind.LabelIdentifier:
                    return State.ExpressionParser.ParseLabeledLoopExpression();

                case SyntaxKind.LeftParen:
                    return State.ExpressionParser.ParseParenthesizedExpression();

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
                    return State.ExpressionParser.ParseArrayLiteralExpression();

                case SyntaxKind.Identifier:
                case SyntaxKind.SelfKeyword:
                case SyntaxKind.SelfTypeKeyword:
                    return State.ParserUtilities.ParseNameExpression();

                default:
                    Diagnostics.ReportUnexpectedExpression(Current.Span, Current.Kind);
                    var bad = NextToken();
                    return new NameExpressionSyntax(bad);
            }
        }

        internal IfExpressionSyntax ParseIfExpression()
        {
            var ifKeyword = MatchToken(SyntaxKind.IfKeyword);
            var condition = State.ExpressionParser.ParseControlCondition();
            var thenBlock = State.ExpressionParser.ParseRequiredControlBlock(ifKeyword);

            SyntaxToken elseKeyword = null;
            ExpressionSyntax elseExpression = null;
            if (Current.Kind == SyntaxKind.ElseKeyword)
            {
                elseKeyword = NextToken();
                if (Current.Kind == SyntaxKind.IfKeyword)
                {
                    elseExpression = State.ExpressionParser.ParseIfExpression();
                }
                else
                {
                    elseExpression = new BlockExpressionSyntax(
                        State.ExpressionParser.ParseRequiredControlBlock(elseKeyword));
                }
            }

            return new IfExpressionSyntax(
                ifKeyword,
                condition,
                thenBlock,
                elseKeyword,
                elseExpression);
        }

        internal MatchExpressionSyntax ParseMatchExpression()
        {
            var matchKeyword = MatchToken(SyntaxKind.MatchKeyword);
            var expression = State.ExpressionParser.ParseControlCondition();
            var openBrace = MatchToken(SyntaxKind.LeftBrace);
            var arms = new List<MatchArmSyntax>();

            while (Current.Kind != SyntaxKind.RightBrace &&
                   Current.Kind != SyntaxKind.EndOfFile &&
                   !State.DeclarationParser.IsMemberStart(Current.Kind))
            {
                var start = Position;
                var pattern = State.PatternParser.ParsePattern();
                if (Current.Kind != SyntaxKind.FatArrowToken &&
                    Current.Kind != SyntaxKind.Comma &&
                    Current.Kind != SyntaxKind.RightBrace &&
                    Current.Kind != SyntaxKind.EndOfFile)
                {
                    Diagnostics.ReportUnsupportedPatternForm(Current.Span, Current.Text);
                    State.PatternParser.SkipUnsupportedPatternTail();
                }
                var fatArrow = MatchToken(SyntaxKind.FatArrowToken);
                ExpressionSyntax armExpression;
                if (Current.Kind == SyntaxKind.LeftBrace)
                {
                    armExpression = new BlockExpressionSyntax(
                        State.StatementParser.ParseBlockStatement(allowTrailingExpression: true));
                }
                else
                {
                    armExpression = State.ExpressionParser.ParseExpression();
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

                if (Position == start)
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

        internal ExpressionSyntax ParseLabeledLoopExpression()
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
                return State.ExpressionParser.ParseWhileExpression(label);

            if (Current.Kind == SyntaxKind.LoopKeyword)
                return State.ExpressionParser.ParseLoopExpression(label);

            Diagnostics.ReportInvalidLoopLabelTarget(Current.Span);
            var bad = NextToken();
            return new NameExpressionSyntax(bad);
        }

        internal WhileExpressionSyntax ParseWhileExpression(LoopLabelSyntax label)
        {
            var whileKeyword = MatchToken(SyntaxKind.WhileKeyword);
            var condition = State.ExpressionParser.ParseControlCondition();
            var body = State.ExpressionParser.ParseRequiredControlBlock(whileKeyword);
            return new WhileExpressionSyntax(label, whileKeyword, condition, body);
        }

        internal ExpressionSyntax ParseControlCondition()
        {
            SuppressAggregateInitializerDepth++;
            try
            {
                return State.ExpressionParser.ParseExpression();
            }
            finally
            {
                SuppressAggregateInitializerDepth--;
            }
        }

        internal LoopExpressionSyntax ParseLoopExpression(LoopLabelSyntax label)
        {
            var loopKeyword = MatchToken(SyntaxKind.LoopKeyword);
            var body = State.ExpressionParser.ParseRequiredControlBlock(loopKeyword);
            return new LoopExpressionSyntax(label, loopKeyword, body);
        }

        internal BlockStatementSyntax ParseRequiredControlBlock(SyntaxToken keyword)
        {
            if (Current.Kind == SyntaxKind.LeftBrace)
                return State.StatementParser.ParseBlockStatement(allowTrailingExpression: true);

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

        internal ExpressionSyntax ParseParenthesizedExpression()
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

            var expression = State.ExpressionParser.ParseExpression();
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
                elements.Add(State.ExpressionParser.ParseExpression());
            }

            var closeParenToken = MatchToken(SyntaxKind.RightParen);
            return new TupleExpressionSyntax(
                openParenToken,
                elements,
                separators,
                closeParenToken);
        }

        internal ArrayLiteralExpressionSyntax ParseArrayLiteralExpression()
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

            elements.Add(State.ExpressionParser.ParseExpression());
            if (Current.Kind == SyntaxKind.Semicolon)
            {
                var repeatSeparator = NextToken();
                var repeatLength = State.ExpressionParser.ParseExpression();
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

                elements.Add(State.ExpressionParser.ParseExpression());
            }

            var closeBracketToken = MatchToken(SyntaxKind.RightBracket);
            return new ArrayLiteralExpressionSyntax(
                openBracketToken,
                elements,
                separators,
                closeBracketToken);
        }

        internal CallExpressionSyntax ParseCallExpression(ExpressionSyntax target)
        {
            var leftParen = MatchToken(SyntaxKind.LeftParen);
            var arguments = new List<ExpressionSyntax>();

            if (Current.Kind != SyntaxKind.RightParen &&
                Current.Kind != SyntaxKind.EndOfFile)
            {
                while (true)
                {
                    arguments.Add(State.ExpressionParser.ParseExpression());

                    if (Current.Kind != SyntaxKind.Comma)
                        break;

                    NextToken();
                }
            }

            var rightParen = MatchToken(SyntaxKind.RightParen);
            return new CallExpressionSyntax(target, leftParen, arguments, rightParen);
        }

        internal AggregateInitializerExpressionSyntax ParseAggregateInitializerExpression(
        ExpressionSyntax target)
        {
            var openBrace = MatchToken(SyntaxKind.LeftBrace);
            var fields = new List<AggregateInitializerFieldSyntax>();
            while (Current.Kind != SyntaxKind.RightBrace &&
                   Current.Kind != SyntaxKind.EndOfFile &&
                   !State.DeclarationParser.IsMemberStart(Current.Kind))
            {
                var start = Position;
                var identifier = MatchToken(SyntaxKind.Identifier);
                var colon = MatchToken(SyntaxKind.Colon);
                var expression = State.ExpressionParser.ParseExpression();
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
                if (Position == start)
                    NextToken();
            }

            var closeBrace = MatchToken(SyntaxKind.RightBrace);
            return new AggregateInitializerExpressionSyntax(
                target,
                openBrace,
                fields,
                closeBrace);
        }

        internal ExpressionSyntax ParsePostfixExpression()
        {
            ExpressionSyntax expression = State.ExpressionParser.ParsePrimaryExpression();

            while (true)
            {
                if (Current.Kind == SyntaxKind.LessToken &&
                    State.ExpressionParser.CanParseExpressionTypeArgumentList())
                {
                    expression = new GenericTypeExpressionSyntax(
                        expression,
                        State.TypeParser.ParseTypeArgumentList());
                    continue;
                }

                if (Current.Kind == SyntaxKind.Dot)
                {
                    var dot = NextToken();
                    var name = State.ParserUtilities.ParseMemberNameToken();
                    var questionToken = State.ParserUtilities.ParseCallableQuestionSuffix(name);
                    expression = new MemberAccessExpressionSyntax(
                        expression,
                        dot,
                        name,
                        questionToken);
                    continue;
                }

                if (Current.Kind == SyntaxKind.LeftParen)
                {
                    expression = State.ExpressionParser.ParseCallExpression(expression);
                    continue;
                }

                if (Current.Kind == SyntaxKind.LeftBracket)
                {
                    var openBracket = NextToken();
                    var index = State.ExpressionParser.ParseExpression();
                    var closeBracket = MatchToken(SyntaxKind.RightBracket);
                    expression = new ElementAccessExpressionSyntax(
                        expression,
                        openBracket,
                        index,
                        closeBracket);
                    continue;
                }

                if (Current.Kind == SyntaxKind.LeftBrace &&
                    SuppressAggregateInitializerDepth == 0 &&
                    (expression is NameExpressionSyntax ||
                     expression is MemberAccessExpressionSyntax ||
                     expression is GenericTypeExpressionSyntax))
                {
                    expression = State.ExpressionParser.ParseAggregateInitializerExpression(expression);
                    continue;
                }

                break;
            }

            return expression;
        }

        internal bool CanParseExpressionTypeArgumentList()
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
                {
                    // A comparison must not scan past its expression into a later
                    // declaration or statement looking for a closing type argument.
                    if (token.Kind != SyntaxKind.Identifier &&
                        token.Kind != SyntaxKind.SelfTypeKeyword &&
                        token.Kind != SyntaxKind.Dot &&
                        token.Kind != SyntaxKind.Comma &&
                        token.Kind != SyntaxKind.LeftParen &&
                        token.Kind != SyntaxKind.RightParen &&
                        token.Kind != SyntaxKind.LeftBracket &&
                        token.Kind != SyntaxKind.RightBracket)
                        return false;
                    continue;
                }

                if (depth != 0)
                    continue;

                var following = Peek(offset + 1).Kind;
                return following == SyntaxKind.Dot ||
                    following == SyntaxKind.LeftBrace ||
                    following == SyntaxKind.LeftParen;
            }
        }

        internal ExpressionSyntax ParseExpression(int parentPrecedence = 0)
        {
            ExpressionSyntax left;

            var unaryPrecedence = SyntaxFacts.GetUnaryOperatorPrecedence(Current.Kind);
            if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
            {
                var operatorToken = NextToken();
                var operand = State.ExpressionParser.ParseExpression(unaryPrecedence);
                left = new UnaryExpressionSyntax(operatorToken, operand);
            }
            else
            {
                left = State.ExpressionParser.ParsePostfixExpression();
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
                var right = State.ExpressionParser.ParseExpression(rightPrecedence);

                left = SyntaxFacts.IsAssignmentOperator(operatorKind)
                    ? new AssignmentExpressionSyntax(left, operatorToken, right)
                    : new BinaryExpressionSyntax(left, operatorToken, right);
            }

            return left;
        }
    }
}
