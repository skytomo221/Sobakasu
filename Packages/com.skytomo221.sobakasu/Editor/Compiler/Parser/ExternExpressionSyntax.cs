using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ExternExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken ExternKeyword { get; }
        public ExpressionSyntax Expression { get; }

        public ExternExpressionSyntax(
            SyntaxToken externKeyword,
            ExpressionSyntax expression)
        {
            ExternKeyword = externKeyword;
            Expression = expression;
        }
    }
}
