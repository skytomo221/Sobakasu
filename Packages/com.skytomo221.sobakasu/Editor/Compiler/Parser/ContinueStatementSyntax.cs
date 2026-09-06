using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ContinueStatementSyntax : StatementSyntax
    {
        public SyntaxToken ContinueKeyword { get; }
        public SyntaxToken Label { get; }
        public SyntaxToken SemicolonToken { get; }

        public ContinueStatementSyntax(
            SyntaxToken continueKeyword,
            SyntaxToken label,
            SyntaxToken semicolonToken)
        {
            ContinueKeyword = continueKeyword;
            Label = label;
            SemicolonToken = semicolonToken;
        }
    }
}
