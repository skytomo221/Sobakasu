using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class MatchExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken MatchKeyword { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<MatchArmSyntax> Arms { get; }
        public SyntaxToken CloseBraceToken { get; }

        public MatchExpressionSyntax(
            SyntaxToken matchKeyword,
            ExpressionSyntax expression,
            SyntaxToken openBraceToken,
            IReadOnlyList<MatchArmSyntax> arms,
            SyntaxToken closeBraceToken)
        {
            MatchKeyword = matchKeyword;
            Expression = expression;
            OpenBraceToken = openBraceToken;
            Arms = arms;
            CloseBraceToken = closeBraceToken;
        }
    }
}
