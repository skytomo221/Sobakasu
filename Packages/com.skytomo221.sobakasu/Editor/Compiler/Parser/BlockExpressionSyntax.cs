using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class BlockExpressionSyntax : ExpressionSyntax
    {
        public BlockStatementSyntax Block { get; }

        public BlockExpressionSyntax(BlockStatementSyntax block)
        {
            Block = block;
        }
    }
}
