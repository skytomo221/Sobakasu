using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class StatementParser : ParserComponent
    {
        internal StatementParser(ParserState state) : base(state) { }

        internal VariableDeclarationStatementSyntax ParseVariableDeclarationStatement()
        {
            var letKeyword = MatchToken(SyntaxKind.LetKeyword);
            SyntaxToken mutKeyword = null;
            if (Current.Kind == SyntaxKind.MutKeyword)
                mutKeyword = NextToken();

            var pattern = State.StatementParser.ParseBindingPattern();
            if (pattern is NameBindingPatternSyntax)
                State.ParserUtilities.RejectQuestionMarkInName("local variable");

            TypeClauseSyntax typeClause = null;
            if (Current.Kind == SyntaxKind.Colon)
                typeClause = State.TypeParser.ParseTypeClause();

            SyntaxToken equalsToken = null;
            ExpressionSyntax initializer = null;
            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                equalsToken = NextToken();
                initializer = State.ExpressionParser.ParseExpression();
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

        internal BindingPatternSyntax ParseBindingPattern()
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

            var first = State.StatementParser.ParseBindingPattern();
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
                elements.Add(State.StatementParser.ParseBindingPattern());
            }

            return new TupleBindingPatternSyntax(
                openParen,
                elements,
                separators,
                MatchToken(SyntaxKind.RightParen));
        }

        internal ExpressionStatementSyntax ParseExpressionStatement()
        {
            var expression = State.ExpressionParser.ParseExpression();
            SyntaxToken semicolon = null;
            if (Current.Kind == SyntaxKind.Semicolon)
            {
                semicolon = NextToken();
            }
            else if (!State.StatementParser.IsControlExpression(expression))
            {
                semicolon = MatchToken(SyntaxKind.Semicolon);
            }

            return new ExpressionStatementSyntax(expression, semicolon);
        }

        internal ReturnStatementSyntax ParseReturnStatement()
        {
            var returnKeyword = MatchToken(SyntaxKind.ReturnKeyword);
            ExpressionSyntax expression = null;
            if (Current.Kind != SyntaxKind.Semicolon &&
                Current.Kind != SyntaxKind.EndOfFile)
            {
                expression = State.ExpressionParser.ParseExpression();
            }

            var semicolon = MatchToken(SyntaxKind.Semicolon);
            return new ReturnStatementSyntax(returnKeyword, expression, semicolon);
        }

        internal SendStatementSyntax ParseSendStatement()
        {
            var sendKeyword = MatchToken(SyntaxKind.SendKeyword);
            var receiverName = MatchToken(SyntaxKind.Identifier);
            State.ParserUtilities.RejectQuestionMarkInName("network receiver");
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
                        arguments.Add(State.ExpressionParser.ParseExpression());
                        if (Current.Kind != SyntaxKind.Comma)
                            break;
                        separators.Add(NextToken());
                    }
                }

                closeParen = MatchToken(SyntaxKind.RightParen);
            }

            var toKeyword = MatchToken(SyntaxKind.ToKeyword);
            var target = State.ExpressionParser.ParseExpression();
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

        internal BreakStatementSyntax ParseBreakStatement()
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
                expression = State.ExpressionParser.ParseExpression();
            }

            var semicolon = State.StatementParser.RecoverJumpTerminator("break");
            return new BreakStatementSyntax(
                breakKeyword,
                label,
                expression,
                semicolon);
        }

        internal ContinueStatementSyntax ParseContinueStatement()
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

            var semicolon = State.StatementParser.RecoverJumpTerminator("continue");
            return new ContinueStatementSyntax(
                continueKeyword,
                label,
                semicolon);
        }

        internal RedoStatementSyntax ParseRedoStatement()
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

            var semicolon = State.StatementParser.RecoverJumpTerminator("redo");
            return new RedoStatementSyntax(
                redoKeyword,
                label,
                semicolon);
        }

        internal SyntaxToken RecoverJumpTerminator(string statementName)
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

        internal bool CanStartExpression(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.LeftParen or SyntaxKind.IfKeyword or SyntaxKind.MatchKeyword or SyntaxKind.WhileKeyword or SyntaxKind.LoopKeyword or SyntaxKind.LabelIdentifier or SyntaxKind.String or SyntaxKind.Int8Literal or SyntaxKind.UInt8Literal or SyntaxKind.Int16Literal or SyntaxKind.UInt16Literal or SyntaxKind.Int32Literal or SyntaxKind.UInt32Literal or SyntaxKind.Int64Literal or SyntaxKind.UInt64Literal or SyntaxKind.Float32Literal or SyntaxKind.Float64Literal or SyntaxKind.CharacterLiteral or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword or SyntaxKind.ExternKeyword or SyntaxKind.NewKeyword or SyntaxKind.LeftBracket or SyntaxKind.Identifier or SyntaxKind.SelfKeyword or SyntaxKind.SelfTypeKeyword or SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.BangToken or SyntaxKind.TildeToken => true,
                _ => false,
            };
        }

        internal StatementSyntax ParseStatement()
        {
            if (Current.Kind == SyntaxKind.LeftBrace)
                return State.StatementParser.ParseBlockStatement();

            if (Current.Kind == SyntaxKind.ModKeyword ||
                Current.Kind == SyntaxKind.PubKeyword &&
                Peek(1).Kind == SyntaxKind.ModKeyword)
            {
                var declarationStart = Current;
                Diagnostics.ReportModMustBeTopLevel(declarationStart.Span);
                var declaration = State.ModuleParser.ParseModDeclaration();
                return new ExpressionStatementSyntax(
                    new NameExpressionSyntax(declaration.ModKeyword),
                    declaration.SemicolonToken);
            }

            if (Current.Kind == SyntaxKind.PubKeyword ||
                Current.Kind == SyntaxKind.SyncKeyword)
            {
                return State.StatementParser.ParseInvalidLocalStateDeclaration();
            }

            if (Current.Kind == SyntaxKind.LetKeyword)
                return State.StatementParser.ParseVariableDeclarationStatement();

            if (Current.Kind == SyntaxKind.ConstKeyword ||
                Current.Kind == SyntaxKind.StateKeyword)
            {
                return State.StatementParser.ParseInvalidLocalDeclaration();
            }

            if (Current.Kind == SyntaxKind.ReturnKeyword)
                return State.StatementParser.ParseReturnStatement();

            if (Current.Kind == SyntaxKind.SendKeyword)
                return State.StatementParser.ParseSendStatement();

            if (Current.Kind == SyntaxKind.BreakKeyword)
                return State.StatementParser.ParseBreakStatement();

            if (Current.Kind == SyntaxKind.ContinueKeyword)
                return State.StatementParser.ParseContinueStatement();

            if (Current.Kind == SyntaxKind.RedoKeyword)
                return State.StatementParser.ParseRedoStatement();

            return State.StatementParser.ParseExpressionStatement();
        }

        internal StatementSyntax ParseInvalidLocalStateDeclaration()
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
                State.DeclarationParser.ParseSynchronizationModifier();
            }

            if (Current.Kind == SyntaxKind.LetKeyword)
                return State.StatementParser.ParseVariableDeclarationStatement();

            if (Current.Kind == SyntaxKind.ConstKeyword ||
                Current.Kind == SyntaxKind.StateKeyword)
            {
                return State.StatementParser.ParseInvalidLocalDeclaration();
            }

            return State.StatementParser.ParseExpressionStatement();
        }

        internal InvalidLocalDeclarationStatementSyntax ParseInvalidLocalDeclaration()
        {
            var keyword = NextToken();
            Diagnostics.ReportDeclarationMustBeTopLevel(
                keyword.Span,
                keyword.Text ?? string.Empty);
            if (Current.Kind == SyntaxKind.MutKeyword)
                NextToken();
            MatchToken(SyntaxKind.Identifier);
            if (Current.Kind == SyntaxKind.Colon)
                State.TypeParser.ParseTypeClause();
            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                NextToken();
                if (Current.Kind != SyntaxKind.Semicolon)
                    State.ExpressionParser.ParseExpression();
            }
            var semicolon = MatchToken(SyntaxKind.Semicolon);
            return new InvalidLocalDeclarationStatementSyntax(keyword, semicolon);
        }

        internal BlockStatementSyntax ParseBlockStatement(bool allowTrailingExpression = false)
        {
            var openBrace = MatchToken(SyntaxKind.LeftBrace);
            var statements = new List<StatementSyntax>();
            ExpressionSyntax trailingExpression = null;

            while (Current.Kind != SyntaxKind.RightBrace &&
                   Current.Kind != SyntaxKind.EndOfFile)
            {
                if (allowTrailingExpression && State.StatementParser.CanStartExpression(Current.Kind))
                {
                    var expression = State.ExpressionParser.ParseExpression();
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
                    else if (!State.StatementParser.IsControlExpression(expression))
                    {
                        semicolon = MatchToken(SyntaxKind.Semicolon);
                    }

                    statements.Add(new ExpressionStatementSyntax(expression, semicolon));
                    continue;
                }

                statements.Add(State.StatementParser.ParseStatement());
            }

            var closeBrace = MatchToken(SyntaxKind.RightBrace);
            return new BlockStatementSyntax(openBrace, statements, trailingExpression, closeBrace);
        }

        internal bool IsControlExpression(ExpressionSyntax expression)
        {
            return expression is IfExpressionSyntax ||
                   expression is MatchExpressionSyntax ||
                   expression is WhileExpressionSyntax ||
                   expression is LoopExpressionSyntax;
        }
    }
}
