using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class MatchArmSyntax : SyntaxNode
    {
        public PatternSyntax Pattern { get; }
        public SyntaxToken FatArrowToken { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken CommaToken { get; }

        public MatchArmSyntax(
            PatternSyntax pattern,
            SyntaxToken fatArrowToken,
            ExpressionSyntax expression,
            SyntaxToken commaToken)
        {
            Pattern = pattern;
            FatArrowToken = fatArrowToken;
            Expression = expression;
            CommaToken = commaToken;
        }
    }
}
