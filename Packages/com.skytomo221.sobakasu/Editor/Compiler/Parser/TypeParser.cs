using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class TypeParser : ParserComponent
    {
        internal TypeParser(ParserState state) : base(state) { }

        internal TypeClauseSyntax ParseTypeClause()
        {
            var colonToken = MatchToken(SyntaxKind.Colon);
            var type = State.TypeParser.ParseTypeSyntax();
            return new TypeClauseSyntax(colonToken, type);
        }

        internal TypeSyntax ParseTypeSyntax()
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

                var first = State.TypeParser.ParseTypeSyntax();
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
                    elements.Add(State.TypeParser.ParseTypeSyntax());
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
                var elementType = State.TypeParser.ParseTypeSyntax();
                var closeBracket = MatchToken(SyntaxKind.RightBracket);
                return new TypeSyntax(openBracket, elementType, closeBracket);
            }

            var parts = new List<SyntaxToken>();
            var dots = new List<SyntaxToken>();

            parts.Add(State.TypeParser.ParseTypeIdentifierToken());
            State.ParserUtilities.RejectQuestionMarkInName("type");

            while (Current.Kind == SyntaxKind.Dot)
            {
                dots.Add(NextToken());
                parts.Add(MatchToken(SyntaxKind.Identifier));
                State.ParserUtilities.RejectQuestionMarkInName("type");
            }

            var typeArguments = Current.Kind == SyntaxKind.LessToken
                ? State.TypeParser.ParseTypeArgumentList()
                : null;
            return new TypeSyntax(parts, dots, typeArguments);
        }

        internal GenericParameterListSyntax ParseGenericParameterList()
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

        internal TypeArgumentListSyntax ParseTypeArgumentList()
        {
            var lessToken = MatchToken(SyntaxKind.LessToken);
            var arguments = new List<TypeSyntax>();
            var separators = new List<SyntaxToken>();
            while (Current.Kind != SyntaxKind.GreaterToken &&
                   Current.Kind != SyntaxKind.GreaterGreaterToken &&
                   Current.Kind != SyntaxKind.EndOfFile)
            {
                arguments.Add(State.TypeParser.ParseTypeSyntax());
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

        internal SyntaxToken ParseTypeIdentifierToken()
        {
            if (Current.Kind == SyntaxKind.Identifier ||
                Current.Kind == SyntaxKind.SelfTypeKeyword)
            {
                return NextToken();
            }

            return MatchToken(SyntaxKind.Identifier);
        }
    }
}
