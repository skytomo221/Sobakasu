using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class AggregateInitializerFieldSyntax : SyntaxNode
    {
        public SyntaxToken Identifier { get; }
        public SyntaxToken ColonToken { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken CommaToken { get; }

        public AggregateInitializerFieldSyntax(
            SyntaxToken identifier,
            SyntaxToken colonToken,
            ExpressionSyntax expression,
            SyntaxToken commaToken)
        {
            Identifier = identifier;
            ColonToken = colonToken;
            Expression = expression;
            CommaToken = commaToken;
        }
    }
}
