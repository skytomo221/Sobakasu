using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class TupleExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<ExpressionSyntax> Elements { get; }
        public IReadOnlyList<SyntaxToken> Separators { get; }
        public SyntaxToken CloseParenToken { get; }

        public TupleExpressionSyntax(
            SyntaxToken openParenToken,
            IReadOnlyList<ExpressionSyntax> elements,
            IReadOnlyList<SyntaxToken> separators,
            SyntaxToken closeParenToken)
        {
            OpenParenToken = openParenToken;
            Elements = elements;
            Separators = separators;
            CloseParenToken = closeParenToken;
        }
    }
}
