using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class AssignmentExpressionSyntax : ExpressionSyntax
    {
        public ExpressionSyntax Target { get; }
        public SyntaxToken OperatorToken { get; }
        public ExpressionSyntax Expression { get; }

        public AssignmentExpressionSyntax(
            ExpressionSyntax target,
            SyntaxToken operatorToken,
            ExpressionSyntax expression)
        {
            Target = target;
            OperatorToken = operatorToken;
            Expression = expression;
        }
    }
}
