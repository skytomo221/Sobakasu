using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
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
}
