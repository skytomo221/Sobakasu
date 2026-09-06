using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class RedoStatementSyntax : StatementSyntax
    {
        public SyntaxToken RedoKeyword { get; }
        public SyntaxToken Label { get; }
        public SyntaxToken SemicolonToken { get; }

        public RedoStatementSyntax(
            SyntaxToken redoKeyword,
            SyntaxToken label,
            SyntaxToken semicolonToken)
        {
            RedoKeyword = redoKeyword;
            Label = label;
            SemicolonToken = semicolonToken;
        }
    }
}
