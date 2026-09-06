using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class DeclarationParser : ParserComponent
    {
        internal DeclarationParser(ParserState state) : base(state) { }

        internal AggregateFieldDeclarationSyntax ParseAggregateFieldDeclaration()
        {
            var identifier = MatchToken(SyntaxKind.Identifier);
            var colon = MatchToken(SyntaxKind.Colon);
            var type = State.TypeParser.ParseTypeSyntax();
            SyntaxToken equals = null;
            SyntaxToken externKeyword = null;
            SyntaxToken externalMemberName = null;
            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                equals = NextToken();
                externKeyword = MatchToken(SyntaxKind.ExternKeyword);
                externalMemberName = State.ParserUtilities.ParseMemberNameToken();
            }
            SyntaxToken comma = null;
            if (Current.Kind == SyntaxKind.Comma)
                comma = NextToken();
            else if (Current.Kind != SyntaxKind.RightBrace)
                comma = MatchToken(SyntaxKind.Comma);

            return new AggregateFieldDeclarationSyntax(identifier, colon, type, equals, externKeyword, externalMemberName, comma);
        }

        internal StructDeclarationSyntax ParseStructDeclaration(
        LanguageItemSyntax languageItem = null)
        {
            var pubKeyword = Current.Kind == SyntaxKind.PubKeyword ? NextToken() : null;
            var structKeyword = MatchToken(SyntaxKind.StructKeyword);
            var identifier = MatchToken(SyntaxKind.Identifier);
            var genericParameters = Current.Kind == SyntaxKind.LessToken
                ? State.TypeParser.ParseGenericParameterList()
                : null;
            SyntaxToken equals = null;
            SyntaxToken externKeyword = null;
            QualifiedNameSyntax externalTypeName = null;
            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                equals = NextToken();
                externKeyword = MatchToken(SyntaxKind.ExternKeyword);
                externalTypeName = State.ParserUtilities.ParseQualifiedName(out _);
            }
            var openBrace = MatchToken(SyntaxKind.LeftBrace);
            var fields = new List<AggregateFieldDeclarationSyntax>();
            while (Current.Kind != SyntaxKind.RightBrace &&
                   Current.Kind != SyntaxKind.EndOfFile &&
                   !State.DeclarationParser.IsMemberStart(Current.Kind))
            {
                var start = Position;
                fields.Add(State.DeclarationParser.ParseAggregateFieldDeclaration());
                if (Position == start)
                    NextToken();
            }

            var closeBrace = MatchToken(SyntaxKind.RightBrace);
            return new StructDeclarationSyntax(
                languageItem,
                pubKeyword,
                structKeyword,
                identifier,
                genericParameters,
                equals,
                externKeyword,
                externalTypeName,
                openBrace,
                fields,
                closeBrace);
        }

        internal EnumVariantDeclarationSyntax ParseEnumVariantDeclaration()
        {
            var identifier = Current.Kind == SyntaxKind.SelfTypeKeyword
                ? NextToken()
                : MatchToken(SyntaxKind.Identifier);
            var kind = EnumVariantSyntaxKind.Unit;
            SyntaxToken openParen = null;
            SyntaxToken closeParen = null;
            SyntaxToken openBrace = null;
            SyntaxToken closeBrace = null;
            SyntaxToken equals = null;
            SyntaxToken externKeyword = null;
            SyntaxToken externalMemberName = null;
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
                       !State.DeclarationParser.IsMemberStart(Current.Kind))
                {
                    tupleTypes.Add(State.TypeParser.ParseTypeSyntax());
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
                       !State.DeclarationParser.IsMemberStart(Current.Kind))
                {
                    var start = Position;
                    namedFields.Add(State.DeclarationParser.ParseAggregateFieldDeclaration());
                    if (Position == start)
                        NextToken();
                }
                closeBrace = MatchToken(SyntaxKind.RightBrace);
            }

            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                equals = NextToken();
                externKeyword = MatchToken(SyntaxKind.ExternKeyword);
                externalMemberName = State.ParserUtilities.ParseMemberNameToken();
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
                equals,
                externKeyword,
                externalMemberName,
                comma);
        }

        internal EnumDeclarationSyntax ParseEnumDeclaration(
        LanguageItemSyntax languageItem = null)
        {
            var pubKeyword = Current.Kind == SyntaxKind.PubKeyword ? NextToken() : null;
            var enumKeyword = MatchToken(SyntaxKind.EnumKeyword);
            var identifier = MatchToken(SyntaxKind.Identifier);
            var genericParameters = Current.Kind == SyntaxKind.LessToken
                ? State.TypeParser.ParseGenericParameterList()
                : null;
            SyntaxToken equals = null;
            SyntaxToken externKeyword = null;
            QualifiedNameSyntax externalTypeName = null;
            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                equals = NextToken();
                externKeyword = MatchToken(SyntaxKind.ExternKeyword);
                externalTypeName = State.ParserUtilities.ParseQualifiedName(out _);
            }
            var openBrace = MatchToken(SyntaxKind.LeftBrace);
            var variants = new List<EnumVariantDeclarationSyntax>();
            while (Current.Kind != SyntaxKind.RightBrace &&
                   Current.Kind != SyntaxKind.EndOfFile &&
                   !State.DeclarationParser.IsMemberStart(Current.Kind))
            {
                var start = Position;
                variants.Add(State.DeclarationParser.ParseEnumVariantDeclaration());
                if (Position == start)
                    NextToken();
            }

            var closeBrace = MatchToken(SyntaxKind.RightBrace);
            return new EnumDeclarationSyntax(
                languageItem,
                pubKeyword,
                enumKeyword,
                identifier,
                genericParameters,
                equals,
                externKeyword,
                externalTypeName,
                openBrace,
                variants,
                closeBrace);
        }

        internal bool IsMemberStart(SyntaxKind kind)
        {
            return kind == SyntaxKind.FnKeyword ||
                kind == SyntaxKind.ReceiveKeyword ||
                kind == SyntaxKind.On ||
                kind == SyntaxKind.UseKeyword ||
                kind == SyntaxKind.ModKeyword ||
                kind == SyntaxKind.LangKeyword ||
                kind == SyntaxKind.ImplKeyword ||
                kind == SyntaxKind.StructKeyword ||
                kind == SyntaxKind.EnumKeyword ||
                kind == SyntaxKind.ConstKeyword ||
                kind == SyntaxKind.StateKeyword ||
                kind == SyntaxKind.LetKeyword ||
                kind == SyntaxKind.SyncKeyword ||
                kind == SyntaxKind.PubKeyword;
        }

        internal SynchronizationModifierSyntax ParseSynchronizationModifier()
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

        internal StateDeclarationSyntax ParseStateDeclaration()
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

                var currentSynchronizationModifier = State.DeclarationParser.ParseSynchronizationModifier();
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
            State.DeclarationParser.ConsumeMisplacedStateModifiers();

            SyntaxToken mutKeyword = null;
            if (Current.Kind == SyntaxKind.MutKeyword)
            {
                mutKeyword = NextToken();
                Diagnostics.ReportStateCannotUseMut(mutKeyword.Span);
            }

            State.DeclarationParser.ConsumeMisplacedStateModifiers();
            var identifier = MatchToken(SyntaxKind.Identifier);
            State.ParserUtilities.RejectQuestionMarkInName("top-level state");

            TypeClauseSyntax typeClause = null;
            if (Current.Kind == SyntaxKind.Colon)
                typeClause = State.TypeParser.ParseTypeClause();

            if (pubKeyword != null && typeClause == null)
                Diagnostics.ReportPublicStateRequiresExplicitType(identifier.Span);

            SyntaxToken equalsToken = null;
            ExpressionSyntax initializer = null;
            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                equalsToken = NextToken();
                if (pubKeyword != null)
                    Diagnostics.ReportPublicStateCannotHaveSourceInitializer(equalsToken.Span);

                if (Current.Kind == SyntaxKind.Semicolon)
                {
                    if (pubKeyword == null)
                    {
                        Diagnostics.ReportMissingTopLevelStateInitializer(
                            Current.Span,
                            identifier.Text ?? string.Empty);
                    }
                }
                else
                {
                    initializer = State.ExpressionParser.ParseExpression();
                }
            }
            else if (pubKeyword == null)
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

        internal ConstDeclarationSyntax ParseConstDeclaration()
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

                var currentSynchronizationModifier = State.DeclarationParser.ParseSynchronizationModifier();
                Diagnostics.ReportSynchronizationOnlyOnState(
                    currentSynchronizationModifier.SyncKeyword.Span);
                synchronizationModifier ??= currentSynchronizationModifier;
            }

            var constKeyword = MatchToken(SyntaxKind.ConstKeyword);
            var identifier = MatchToken(SyntaxKind.Identifier);
            State.ParserUtilities.RejectQuestionMarkInName("constant");

            TypeClauseSyntax typeClause = null;
            if (Current.Kind == SyntaxKind.Colon)
                typeClause = State.TypeParser.ParseTypeClause();

            SyntaxToken equalsToken = null;
            ExpressionSyntax initializer = null;
            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                equalsToken = NextToken();
                if (Current.Kind != SyntaxKind.Semicolon)
                    initializer = State.ExpressionParser.ParseExpression();
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

        internal LegacyTopLevelLetDeclarationSyntax ParseLegacyTopLevelLetDeclaration()
        {
            var firstToken = Current;
            while (Current.Kind == SyntaxKind.PubKeyword ||
                   Current.Kind == SyntaxKind.SyncKeyword)
            {
                if (Current.Kind == SyntaxKind.SyncKeyword)
                    State.DeclarationParser.ParseSynchronizationModifier();
                else
                    NextToken();
            }

            var letKeyword = MatchToken(SyntaxKind.LetKeyword);
            Diagnostics.ReportTopLevelLetNoLongerSupported(letKeyword.Span);
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
            return new LegacyTopLevelLetDeclarationSyntax(
                firstToken,
                letKeyword,
                semicolon);
        }

        internal void ConsumeMisplacedStateModifiers()
        {
            while (Current.Kind == SyntaxKind.PubKeyword ||
                   Current.Kind == SyntaxKind.SyncKeyword)
            {
                Diagnostics.ReportStateModifierOrder(Current.Span);
                if (Current.Kind == SyntaxKind.SyncKeyword)
                    State.DeclarationParser.ParseSynchronizationModifier();
                else
                    NextToken();
            }
        }

        internal ParameterSyntax ParseParameter()
        {
            var parameterName = Current.Kind == SyntaxKind.SelfKeyword
                ? NextToken()
                : MatchToken(SyntaxKind.Identifier);
            State.ParserUtilities.RejectQuestionMarkInName("parameter");
            var colon = MatchToken(SyntaxKind.Colon);
            var type = State.TypeParser.ParseTypeSyntax();
            return new ParameterSyntax(parameterName, colon, type);
        }

        internal void ParseParameterList(
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
                parameters.Add(State.DeclarationParser.ParseParameter());

                if (Current.Kind != SyntaxKind.Comma)
                    break;

                separators.Add(NextToken());
                if (Current.Kind == SyntaxKind.RightParen)
                    break;
            }
        }

        internal FunctionReturnTypeSyntax ParseFunctionReturnType()
        {
            var arrowToken = MatchToken(SyntaxKind.ArrowToken);
            var type = State.TypeParser.ParseTypeSyntax();
            return new FunctionReturnTypeSyntax(arrowToken, type);
        }

        internal bool IsOperatorFunctionName(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.StarToken or SyntaxKind.SlashToken or SyntaxKind.PercentToken or SyntaxKind.EqualsEqualsToken or SyntaxKind.BangEqualsToken or SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or SyntaxKind.GreaterToken or SyntaxKind.GreaterOrEqualsToken or SyntaxKind.BangToken or SyntaxKind.TildeToken or SyntaxKind.AmpersandToken or SyntaxKind.PipeToken or SyntaxKind.CaretToken or SyntaxKind.LessLessToken or SyntaxKind.GreaterGreaterToken or SyntaxKind.AmpersandAmpersandToken or SyntaxKind.PipePipeToken or SyntaxKind.EqualsToken or SyntaxKind.PlusEqualsToken or SyntaxKind.MinusEqualsToken or SyntaxKind.StarEqualsToken or SyntaxKind.SlashEqualsToken or SyntaxKind.PercentEqualsToken or SyntaxKind.AmpersandEqualsToken or SyntaxKind.PipeEqualsToken or SyntaxKind.CaretEqualsToken or SyntaxKind.LessLessEqualsToken or SyntaxKind.GreaterGreaterEqualsToken => true,
                _ => false,
            };
        }

        internal FunctionDeclarationSyntax ParseFunctionDeclaration()
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
                if (State.DeclarationParser.IsOperatorFunctionName(Current.Kind))
                    operatorToken = NextToken();
                else
                    operatorToken = MatchToken(SyntaxKind.PlusToken);
            }
            else if (State.DeclarationParser.IsOperatorFunctionName(Current.Kind))
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
                questionToken = State.ParserUtilities.ParseCallableQuestionSuffix(identifier);
            }

            var genericParameters = Current.Kind == SyntaxKind.LessToken
                ? State.TypeParser.ParseGenericParameterList()
                : null;

            var parameters = new List<ParameterSyntax>();
            var separators = new List<SyntaxToken>();
            State.ParserUtilities.ParseOptionalParameterList(
                "function",
                SyntaxKind.ArrowToken,
                true,
                parameters,
                separators,
                out var openParenToken,
                out var closeParenToken);
            FunctionReturnTypeSyntax returnTypeAnnotation = null;
            if (Current.Kind == SyntaxKind.ArrowToken)
                returnTypeAnnotation = State.DeclarationParser.ParseFunctionReturnType();

            BlockStatementSyntax body = null;
            ExternalFunctionBindingSyntax externalBinding = null;
            if (Current.Kind == SyntaxKind.EqualsToken)
                externalBinding = State.DeclarationParser.ParseExternalFunctionBinding();
            else
                body = State.StatementParser.ParseBlockStatement(allowTrailingExpression: true);

            return new FunctionDeclarationSyntax(
                pubKeyword,
                staticKeyword,
                fnKeyword,
                identifier,
                questionToken,
                atToken,
                operatorToken,
                genericParameters,
                openParenToken,
                parameters,
                separators,
                closeParenToken,
                returnTypeAnnotation,
                body,
                externalBinding);
        }

        internal ExternalFunctionBindingSyntax ParseExternalFunctionBinding()
        {
            var equalsToken = MatchToken(SyntaxKind.EqualsToken);
            SyntaxToken maybeKeyword = null;
            if (Current.Kind == SyntaxKind.Identifier &&
                string.Equals(Current.Text, "maybe", StringComparison.Ordinal))
                maybeKeyword = NextToken();

            if (Current.Kind != SyntaxKind.ExternKeyword)
            {
                var start = maybeKeyword?.Span.Start ?? Current.Span.Start;
                _ = State.ExpressionParser.ParseExpression();
                Diagnostics.ReportInvalidExternalFunctionBinding(
                    TextSpan.FromBounds(start, Current.Span.Start));
                return new ExternalFunctionBindingSyntax(
                    equalsToken,
                    maybeKeyword,
                    null,
                    isMalformed: true);
            }

            var externKeyword = NextToken();
            if (State.DeclarationParser.LooksLikeExternalAbiSignature())
            {
                var abiSignature = State.DeclarationParser.ParseExternalAbiSignature();
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

            var target = State.ExpressionParser.ParseExpression();
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

        internal bool LooksLikeExternalAbiSignature()
        {
            var openParenOffset = 0;
            var firstTargetKind = Peek(openParenOffset).Kind;
            if (firstTargetKind == SyntaxKind.NewKeyword)
            {
                openParenOffset++;
                while (Peek(openParenOffset).Kind != SyntaxKind.LeftParen &&
                       Peek(openParenOffset).Kind != SyntaxKind.EndOfFile)
                {
                    openParenOffset++;
                }
                if (Peek(openParenOffset).Kind != SyntaxKind.LeftParen)
                    return false;
                return State.DeclarationParser.LooksLikeExternalAbiParameterList(openParenOffset);
            }

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

            return State.DeclarationParser.LooksLikeExternalAbiParameterList(openParenOffset);
        }

        internal bool LooksLikeExternalAbiParameterList(int openParenOffset)
        {
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

        internal ExternalAbiSignatureSyntax ParseExternalAbiSignature()
        {
            ExpressionSyntax target = null;
            SyntaxToken newKeyword = null;
            TypeSyntax constructorType = null;
            if (Current.Kind == SyntaxKind.NewKeyword)
            {
                newKeyword = NextToken();
                constructorType = State.TypeParser.ParseTypeSyntax();
            }
            else
            {
                target = new NameExpressionSyntax(
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
                        State.ParserUtilities.ParseMemberNameToken(),
                        null);
                }
            }

            var openParen = MatchToken(SyntaxKind.LeftParen);
            var parameters = new List<ExternalAbiParameterSyntax>();
            var separators = new List<SyntaxToken>();
            while (Current.Kind != SyntaxKind.RightParen &&
                   Current.Kind != SyntaxKind.EndOfFile)
            {
                SyntaxToken maybeKeyword = null;
                if (Current.Kind == SyntaxKind.Identifier &&
                    string.Equals(Current.Text, "maybe", StringComparison.Ordinal))
                {
                    maybeKeyword = NextToken();
                    if (Current.Kind != SyntaxKind.OutKeyword)
                    {
                        Diagnostics.ReportInvalidMaybeExternalAbiParameter(
                            TextSpan.FromBounds(
                                maybeKeyword.Span.Start,
                                Current.Span.End));
                    }
                }

                SyntaxToken modifier = null;
                if (Current.Kind == SyntaxKind.RefKeyword ||
                    Current.Kind == SyntaxKind.OutKeyword)
                {
                    modifier = NextToken();
                }
                var type = State.TypeParser.ParseTypeSyntax();
                var identifier = MatchToken(SyntaxKind.Identifier);
                parameters.Add(new ExternalAbiParameterSyntax(
                    maybeKeyword,
                    modifier,
                    type,
                    identifier));
                if (Current.Kind != SyntaxKind.Comma)
                    break;
                separators.Add(NextToken());
                if (Current.Kind == SyntaxKind.RightParen)
                    break;
            }

            return new ExternalAbiSignatureSyntax(
                target,
                newKeyword,
                constructorType,
                openParen,
                parameters,
                separators,
                MatchToken(SyntaxKind.RightParen));
        }

        internal ImplDeclarationSyntax ParseImplDeclaration(
        LanguageItemSyntax languageItem = null)
        {
            SyntaxToken pubKeyword = null;
            if (Current.Kind == SyntaxKind.PubKeyword)
                pubKeyword = NextToken();

            var implKeyword = MatchToken(SyntaxKind.ImplKeyword);
            var genericParameters = Current.Kind == SyntaxKind.LessToken
                ? State.TypeParser.ParseGenericParameterList()
                : null;
            var targetType = State.TypeParser.ParseTypeSyntax();
            SyntaxToken equalsToken = null;
            SyntaxToken externKeyword = null;
            QualifiedNameSyntax externalTypeName = null;

            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                equalsToken = NextToken();
                externKeyword = MatchToken(SyntaxKind.ExternKeyword);
                externalTypeName = State.ParserUtilities.ParseQualifiedName(out _);
            }

            var openBrace = MatchToken(SyntaxKind.LeftBrace);
            var methods = new List<FunctionDeclarationSyntax>();

            while (Current.Kind != SyntaxKind.RightBrace &&
                   Current.Kind != SyntaxKind.EndOfFile)
            {
                var start = Position;
                if (Current.Kind == SyntaxKind.PubKeyword ||
                    Current.Kind == SyntaxKind.StaticKeyword ||
                    Current.Kind == SyntaxKind.FnKeyword)
                {
                    methods.Add(State.DeclarationParser.ParseFunctionDeclaration());
                }
                else
                {
                    Diagnostics.ReportUnexpectedImplMember(Current.Span, Current.Kind);
                    NextToken();
                }

                if (Position == start)
                    NextToken();
            }

            var closeBrace = MatchToken(SyntaxKind.RightBrace);
            return new ImplDeclarationSyntax(
                languageItem,
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

        internal EventDeclarationSyntax ParseEventDeclaration()
        {
            var onKeyword = MatchToken(SyntaxKind.On);
            var identifier = MatchToken(SyntaxKind.Identifier);
            State.ParserUtilities.RejectQuestionMarkInName("event");
            var parameters = new List<ParameterSyntax>();
            var separators = new List<SyntaxToken>();
            State.ParserUtilities.ParseOptionalParameterList(
                "event",
                SyntaxKind.Colon,
                false,
                parameters,
                separators,
                out var openParenToken,
                out var closeParenToken);
            TypeClauseSyntax returnTypeAnnotation = null;
            if (Current.Kind == SyntaxKind.Colon)
                returnTypeAnnotation = State.TypeParser.ParseTypeClause();

            var body = State.StatementParser.ParseBlockStatement();

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

        internal ReceiveDeclarationSyntax ParseReceiveDeclaration()
        {
            var receiveKeyword = MatchToken(SyntaxKind.ReceiveKeyword);
            var identifier = MatchToken(SyntaxKind.Identifier);
            State.ParserUtilities.RejectQuestionMarkInName("network receiver");
            var parameters = new List<ParameterSyntax>();
            var separators = new List<SyntaxToken>();
            State.ParserUtilities.ParseOptionalParameterList(
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
                rejectedReturnType = State.DeclarationParser.ParseFunctionReturnType();
                Diagnostics.ReportReceiveReturnTypeNotAllowed(
                    rejectedReturnType.ArrowToken.Span);
            }

            var body = State.StatementParser.ParseBlockStatement();
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

        internal MemberSyntax ParseMember()
        {
            if (Current.Kind == SyntaxKind.LangKeyword)
            {
                var languageItem = new LanguageItemSyntax(
                    NextToken(),
                    MatchToken(SyntaxKind.String));
                if (Current.Kind == SyntaxKind.StructKeyword ||
                    Current.Kind == SyntaxKind.PubKeyword &&
                    Peek(1).Kind == SyntaxKind.StructKeyword)
                {
                    return State.DeclarationParser.ParseStructDeclaration(languageItem);
                }
                if (Current.Kind == SyntaxKind.EnumKeyword ||
                    Current.Kind == SyntaxKind.PubKeyword &&
                    Peek(1).Kind == SyntaxKind.EnumKeyword)
                {
                    return State.DeclarationParser.ParseEnumDeclaration(languageItem);
                }
                if (Current.Kind == SyntaxKind.ImplKeyword ||
                    Current.Kind == SyntaxKind.PubKeyword &&
                    Peek(1).Kind == SyntaxKind.ImplKeyword)
                {
                    return State.DeclarationParser.ParseImplDeclaration(languageItem);
                }

                Diagnostics.ReportInvalidLanguageItemTarget(
                    languageItem.LangKeyword.Span,
                    Current.Kind);
                if (Current.Kind == SyntaxKind.EndOfFile)
                    return new SkippedMemberSyntax(languageItem.LangKeyword);
                return State.DeclarationParser.ParseMember();
            }

            if (Current.Kind == SyntaxKind.StructKeyword ||
                Current.Kind == SyntaxKind.PubKeyword &&
                Peek(1).Kind == SyntaxKind.StructKeyword)
            {
                return State.DeclarationParser.ParseStructDeclaration();
            }

            if (Current.Kind == SyntaxKind.EnumKeyword ||
                Current.Kind == SyntaxKind.PubKeyword &&
                Peek(1).Kind == SyntaxKind.EnumKeyword)
            {
                return State.DeclarationParser.ParseEnumDeclaration();
            }

            if (Current.Kind == SyntaxKind.ModKeyword ||
                Current.Kind == SyntaxKind.PubKeyword &&
                Peek(1).Kind == SyntaxKind.ModKeyword)
            {
                return State.ModuleParser.ParseModDeclaration();
            }

            if (Current.Kind == SyntaxKind.UseKeyword ||
                Current.Kind == SyntaxKind.PubKeyword &&
                Peek(1).Kind == SyntaxKind.UseKeyword)
            {
                return State.ModuleParser.ParseUseDirective();
            }

            var declarationKind = State.DeclarationParser.PeekModifiedDeclarationKind();
            if (declarationKind == SyntaxKind.ConstKeyword)
                return State.DeclarationParser.ParseConstDeclaration();
            if (declarationKind == SyntaxKind.StateKeyword)
                return State.DeclarationParser.ParseStateDeclaration();
            if (declarationKind == SyntaxKind.LetKeyword)
                return State.DeclarationParser.ParseLegacyTopLevelLetDeclaration();

            if (State.DeclarationParser.TryFindModifiedNonStateMember(out var modifiedMemberKind))
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
                        State.DeclarationParser.ParseSynchronizationModifier();
                        Diagnostics.ReportUnsupportedTopLevelModifier(
                            syncKeyword.Span,
                            "sync",
                            modifiedMemberKind);
                    }
                }

                return State.DeclarationParser.ParseMember();
            }

            if (Current.Kind == SyntaxKind.ImplKeyword ||
                Current.Kind == SyntaxKind.PubKeyword &&
                Peek(1).Kind == SyntaxKind.ImplKeyword)
            {
                return State.DeclarationParser.ParseImplDeclaration();
            }

            if (Current.Kind == SyntaxKind.FnKeyword ||
                Current.Kind == SyntaxKind.PubKeyword &&
                Peek(1).Kind == SyntaxKind.FnKeyword)
                return State.DeclarationParser.ParseFunctionDeclaration();

            if (Current.Kind == SyntaxKind.On)
                return State.DeclarationParser.ParseEventDeclaration();

            if (Current.Kind == SyntaxKind.ReceiveKeyword)
                return State.DeclarationParser.ParseReceiveDeclaration();

            Diagnostics.ReportUnexpectedMember(Current.Span, Current.Kind);

            var badToken = NextToken();
            return new SkippedMemberSyntax(badToken);
        }

        internal SyntaxKind PeekModifiedDeclarationKind()
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

        internal bool TryFindModifiedNonStateMember(out SyntaxKind memberKind)
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

        internal CompilationUnitSyntax ParseCompilationUnit()
        {
            var members = new List<MemberSyntax>();

            while (Current.Kind != SyntaxKind.EndOfFile)
            {
                var start = Position;
                var member = State.DeclarationParser.ParseMember();
                members.Add(member);

                if (Position == start)
                    NextToken();
            }

            var eof = MatchToken(SyntaxKind.EndOfFile);
            return new CompilationUnitSyntax(members, eof);
        }
    }
}
