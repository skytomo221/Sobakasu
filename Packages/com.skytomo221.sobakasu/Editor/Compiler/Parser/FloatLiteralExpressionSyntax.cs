using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class FloatLiteralExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken LiteralToken { get; }

        public FloatLiteralExpressionSyntax(SyntaxToken literalToken)
        {
            LiteralToken = literalToken;
        }
    }
}
