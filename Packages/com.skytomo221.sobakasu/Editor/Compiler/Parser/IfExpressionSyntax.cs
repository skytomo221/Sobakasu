using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class IfExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken IfKeyword { get; }
        public ExpressionSyntax Condition { get; }
        public BlockStatementSyntax ThenBlock { get; }
        public SyntaxToken ElseKeyword { get; }
        public ExpressionSyntax ElseExpression { get; }

        public IfExpressionSyntax(
            SyntaxToken ifKeyword,
            ExpressionSyntax condition,
            BlockStatementSyntax thenBlock,
            SyntaxToken elseKeyword,
            ExpressionSyntax elseExpression)
        {
            IfKeyword = ifKeyword;
            Condition = condition;
            ThenBlock = thenBlock;
            ElseKeyword = elseKeyword;
            ElseExpression = elseExpression;
        }
    }
}
