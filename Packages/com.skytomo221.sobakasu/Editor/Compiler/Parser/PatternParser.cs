using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class PatternParser : ParserComponent
    {
        internal PatternParser(ParserState state) : base(state) { }

        internal PatternSyntax ParsePattern()
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
                    return State.PatternParser.ParseEnumVariantPattern();

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

        internal void SkipUnsupportedPatternTail()
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

        internal PatternSyntax ParseEnumVariantPattern()
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
                    bindings.Add(State.PatternParser.ParsePatternBinding(SyntaxKind.RightParen));
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
                    fields.Add(State.PatternParser.ParsePatternBinding(SyntaxKind.RightBrace));
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

        internal PatternBindingSyntax ParsePatternBinding(SyntaxKind terminator)
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
                State.PatternParser.SkipPatternPayloadItem(terminator);
                return new PatternBindingSyntax(identifier, isSupported: false);
            }

            var unsupported = NextToken();
            Diagnostics.ReportUnsupportedPatternForm(
                unsupported.Span,
                unsupported.Text);
            State.PatternParser.SkipPatternPayloadItem(terminator);
            return new PatternBindingSyntax(unsupported, isSupported: false);
        }

        internal void SkipPatternPayloadItem(SyntaxKind terminator)
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
    }
}
