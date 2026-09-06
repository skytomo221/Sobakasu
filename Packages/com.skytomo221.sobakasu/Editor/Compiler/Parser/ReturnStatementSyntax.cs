using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ReturnStatementSyntax : StatementSyntax
    {
        public SyntaxToken ReturnKeyword { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken SemicolonToken { get; }

        public ReturnStatementSyntax(
            SyntaxToken returnKeyword,
            ExpressionSyntax expression,
            SyntaxToken semicolonToken)
        {
            ReturnKeyword = returnKeyword;
            Expression = expression;
            SemicolonToken = semicolonToken;
        }
    }
}
