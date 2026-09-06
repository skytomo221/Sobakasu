using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class InvalidLocalDeclarationStatementSyntax : StatementSyntax
    {
        public SyntaxToken Keyword { get; }
        public SyntaxToken SemicolonToken { get; }

        public InvalidLocalDeclarationStatementSyntax(
            SyntaxToken keyword,
            SyntaxToken semicolonToken)
        {
            Keyword = keyword;
            SemicolonToken = semicolonToken;
        }
    }
}
