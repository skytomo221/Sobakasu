using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class ModuleParser : ParserComponent
    {
        internal ModuleParser(ParserState state) : base(state) { }

        internal UseDirectiveSyntax ParseUseDirective()
        {
            SyntaxToken pubKeyword = null;
            if (Current.Kind == SyntaxKind.PubKeyword)
                pubKeyword = NextToken();

            var useKeyword = MatchToken(SyntaxKind.UseKeyword);
            var useTree = State.ModuleParser.ParseUseTree(allowBareSpecial: false, out var isMalformed);

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

        internal UseTreeSyntax ParseUseTree(bool allowBareSpecial, out bool isMalformed)
        {
            isMalformed = false;
            if (allowBareSpecial && Current.Kind == SyntaxKind.SelfKeyword)
            {
                var selfKeyword = NextToken();
                State.ModuleParser.ParseUseTreeAlias(
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
            var separatorTokens = new List<SyntaxToken>();
            var firstIdentifier = Current.Kind == SyntaxKind.TypeKeyword
                ? NextToken()
                : MatchToken(SyntaxKind.Identifier);
            identifiers.Add(firstIdentifier);
            isMalformed = string.IsNullOrEmpty(firstIdentifier.Text);

            SyntaxToken suffixSeparator = null;
            UseTreeGroupSyntax group = null;
            SyntaxToken starToken = null;
            while (Current.Kind == SyntaxKind.DoubleColonToken || Current.Kind == SyntaxKind.Dot)
            {
                var separator = NextToken();
                if (separator.Kind == SyntaxKind.Dot)
                {
                    Diagnostics.ReportDotPathSeparator(separator.Span);
                    isMalformed = true;
                }

                if (Current.Kind == SyntaxKind.LeftBrace)
                {
                    suffixSeparator = separator;
                    group = State.ModuleParser.ParseUseTreeGroup(out var groupMalformed);
                    isMalformed |= groupMalformed;
                    break;
                }

                if (Current.Kind == SyntaxKind.StarToken)
                {
                    suffixSeparator = separator;
                    starToken = NextToken();
                    break;
                }

                separatorTokens.Add(separator);
                var identifier = Current.Kind == SyntaxKind.TypeKeyword
                    ? NextToken()
                    : MatchToken(SyntaxKind.Identifier);
                identifiers.Add(identifier);
                isMalformed |= string.IsNullOrEmpty(identifier.Text);
                if (string.IsNullOrEmpty(identifier.Text))
                    break;
            }

            var path = new QualifiedNameSyntax(identifiers, separatorTokens);
            SyntaxToken asKeyword = null;
            SyntaxToken alias = null;
            if (group == null && starToken == null)
                State.ModuleParser.ParseUseTreeAlias(out asKeyword, out alias, ref isMalformed);

            return new UseTreeSyntax(
                path,
                null,
                suffixSeparator,
                group,
                starToken,
                asKeyword,
                alias);
        }

        internal UseTreeGroupSyntax ParseUseTreeGroup(out bool isMalformed)
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

                var start = Position;
                items.Add(State.ModuleParser.ParseUseTree(allowBareSpecial: true, out var itemMalformed));
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
                    if (Position == start)
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

        internal void ParseUseTreeAlias(
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

        internal ModDeclarationSyntax ParseModDeclaration()
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
    }
}
