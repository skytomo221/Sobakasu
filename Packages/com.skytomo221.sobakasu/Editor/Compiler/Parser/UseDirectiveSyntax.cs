using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class UseDirectiveSyntax : MemberSyntax
    {
        public SyntaxToken PubKeyword { get; }
        public SyntaxToken UseKeyword { get; }
        public UseTreeSyntax UseTree { get; }
        public QualifiedNameSyntax Path => UseTree.Path;
        public SyntaxToken AsKeyword => UseTree.AsKeyword;
        public SyntaxToken Alias => UseTree.Alias;
        public SyntaxToken SemicolonToken { get; }
        public bool IsMalformed { get; }
        public bool IsReExport => PubKeyword != null;

        public UseDirectiveSyntax(
            SyntaxToken pubKeyword,
            SyntaxToken useKeyword,
            UseTreeSyntax useTree,
            SyntaxToken semicolonToken,
            bool isMalformed)
        {
            PubKeyword = pubKeyword;
            UseKeyword = useKeyword;
            UseTree = useTree ?? throw new ArgumentNullException(nameof(useTree));
            SemicolonToken = semicolonToken;
            IsMalformed = isMalformed;
        }
    }

    sealed class UseTreeSyntax : SyntaxNode
    {
        public QualifiedNameSyntax Path { get; }
        public SyntaxToken SelfKeyword { get; }
        public SyntaxToken DotToken { get; }
        public UseTreeGroupSyntax Group { get; }
        public SyntaxToken StarToken { get; }
        public SyntaxToken AsKeyword { get; }
        public SyntaxToken Alias { get; }
        public bool IsSelf => SelfKeyword != null;
        public bool IsGlob => StarToken != null;
        public bool IsGroup => Group != null;

        public UseTreeSyntax(
            QualifiedNameSyntax path,
            SyntaxToken selfKeyword,
            SyntaxToken dotToken,
            UseTreeGroupSyntax group,
            SyntaxToken starToken,
            SyntaxToken asKeyword,
            SyntaxToken alias)
        {
            Path = path;
            SelfKeyword = selfKeyword;
            DotToken = dotToken;
            Group = group;
            StarToken = starToken;
            AsKeyword = asKeyword;
            Alias = alias;
        }

        public TextSpan GetSpan()
        {
            var start = Path?.Identifiers[0].Span.Start ??
                SelfKeyword?.Span.Start ??
                StarToken?.Span.Start ?? 0;
            var end = Alias?.Span.End ??
                Group?.CloseBraceToken.Span.End ??
                StarToken?.Span.End ??
                SelfKeyword?.Span.End ??
                Path?.Identifiers[^1].Span.End ?? start;
            return TextSpan.FromBounds(start, end);
        }
    }

    sealed class UseTreeGroupSyntax : SyntaxNode
    {
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<UseTreeSyntax> Items { get; }
        public IReadOnlyList<SyntaxToken> CommaTokens { get; }
        public SyntaxToken CloseBraceToken { get; }

        public UseTreeGroupSyntax(
            SyntaxToken openBraceToken,
            IReadOnlyList<UseTreeSyntax> items,
            IReadOnlyList<SyntaxToken> commaTokens,
            SyntaxToken closeBraceToken)
        {
            OpenBraceToken = openBraceToken;
            Items = items ?? Array.Empty<UseTreeSyntax>();
            CommaTokens = commaTokens ?? Array.Empty<SyntaxToken>();
            CloseBraceToken = closeBraceToken;
        }
    }

    sealed class ModDeclarationSyntax : MemberSyntax
    {
        public SyntaxToken PubKeyword { get; }
        public SyntaxToken ModKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken SemicolonToken { get; }
        public bool IsMalformed { get; }
        public bool IsPublic => PubKeyword != null;

        public ModDeclarationSyntax(
            SyntaxToken pubKeyword,
            SyntaxToken modKeyword,
            SyntaxToken identifier,
            SyntaxToken semicolonToken,
            bool isMalformed)
        {
            PubKeyword = pubKeyword;
            ModKeyword = modKeyword;
            Identifier = identifier;
            SemicolonToken = semicolonToken;
            IsMalformed = isMalformed;
        }
    }
}
