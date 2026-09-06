using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class WhileExpressionSyntax : ExpressionSyntax
    {
        public LoopLabelSyntax Label { get; }
        public SyntaxToken WhileKeyword { get; }
        public ExpressionSyntax Condition { get; }
        public BlockStatementSyntax Body { get; }

        public WhileExpressionSyntax(
            LoopLabelSyntax label,
            SyntaxToken whileKeyword,
            ExpressionSyntax condition,
            BlockStatementSyntax body)
        {
            Label = label;
            WhileKeyword = whileKeyword;
            Condition = condition;
            Body = body;
        }
    }
}
