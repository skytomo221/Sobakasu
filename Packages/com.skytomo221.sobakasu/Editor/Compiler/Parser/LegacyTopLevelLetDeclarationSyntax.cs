using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class LegacyTopLevelLetDeclarationSyntax : MemberSyntax
    {
        public Syntax.SyntaxToken FirstToken { get; }
        public Syntax.SyntaxToken LetKeyword { get; }
        public Syntax.SyntaxToken SemicolonToken { get; }

        public LegacyTopLevelLetDeclarationSyntax(
            Syntax.SyntaxToken firstToken,
            Syntax.SyntaxToken letKeyword,
            Syntax.SyntaxToken semicolonToken)
        {
            FirstToken = firstToken;
            LetKeyword = letKeyword;
            SemicolonToken = semicolonToken;
        }
    }
}
