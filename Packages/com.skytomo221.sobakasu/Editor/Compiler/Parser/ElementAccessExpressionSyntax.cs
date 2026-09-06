using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ElementAccessExpressionSyntax : ExpressionSyntax
    {
        public ExpressionSyntax Expression { get; }
        public SyntaxToken OpenBracketToken { get; }
        public ExpressionSyntax Index { get; }
        public SyntaxToken CloseBracketToken { get; }

        public ElementAccessExpressionSyntax(
            ExpressionSyntax expression,
            SyntaxToken openBracketToken,
            ExpressionSyntax index,
            SyntaxToken closeBracketToken)
        {
            Expression = expression;
            OpenBracketToken = openBracketToken;
            Index = index;
            CloseBracketToken = closeBracketToken;
        }
    }
}
