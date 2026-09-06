using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class ParserUtilities : ParserComponent
    {
        internal ParserUtilities(ParserState state) : base(state) { }

        internal SyntaxToken ParseCallableQuestionSuffix(SyntaxToken identifier)
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

        internal NameExpressionSyntax ParseNameExpression()
        {
            var identifier = Current.Kind == SyntaxKind.SelfKeyword ||
                             Current.Kind == SyntaxKind.SelfTypeKeyword
                ? NextToken()
                : MatchToken(SyntaxKind.Identifier);
            var questionToken = identifier.Kind == SyntaxKind.Identifier
                ? State.ParserUtilities.ParseCallableQuestionSuffix(identifier)
                : null;
            return new NameExpressionSyntax(identifier, questionToken);
        }

        internal void RejectQuestionMarkInName(string nameKind)
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

        internal void ParseOptionalParameterList(
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
                State.DeclarationParser.ParseParameterList(parameters, separators);
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

        internal QualifiedNameSyntax ParseQualifiedName(out bool isMalformed)
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

        internal SyntaxToken ParseMemberNameToken()
        {
            return Current.Kind == SyntaxKind.NewKeyword ||
                   Current.Kind == SyntaxKind.SelfTypeKeyword ||
                   Current.Kind == SyntaxKind.Int32Literal
                ? NextToken()
                : MatchToken(SyntaxKind.Identifier);
        }
    }
}
