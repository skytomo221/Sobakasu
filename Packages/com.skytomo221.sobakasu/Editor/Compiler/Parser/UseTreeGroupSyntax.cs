using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
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
}
