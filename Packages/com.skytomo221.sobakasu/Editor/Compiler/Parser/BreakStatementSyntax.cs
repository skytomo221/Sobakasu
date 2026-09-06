using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class BreakStatementSyntax : StatementSyntax
    {
        public SyntaxToken BreakKeyword { get; }
        public SyntaxToken Label { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken SemicolonToken { get; }

        public BreakStatementSyntax(
            SyntaxToken breakKeyword,
            SyntaxToken label,
            ExpressionSyntax expression,
            SyntaxToken semicolonToken)
        {
            BreakKeyword = breakKeyword;
            Label = label;
            Expression = expression;
            SemicolonToken = semicolonToken;
        }
    }
}
