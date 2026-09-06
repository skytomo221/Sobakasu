using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class LoopExpressionSyntax : ExpressionSyntax
    {
        public LoopLabelSyntax Label { get; }
        public SyntaxToken LoopKeyword { get; }
        public BlockStatementSyntax Body { get; }

        public LoopExpressionSyntax(
            LoopLabelSyntax label,
            SyntaxToken loopKeyword,
            BlockStatementSyntax body)
        {
            Label = label;
            LoopKeyword = loopKeyword;
            Body = body;
        }
    }
}
