using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class LoopLabelSyntax : SyntaxNode
    {
        public SyntaxToken LabelToken { get; }
        public SyntaxToken ColonToken { get; }

        public LoopLabelSyntax(
            SyntaxToken labelToken,
            SyntaxToken colonToken)
        {
            LabelToken = labelToken;
            ColonToken = colonToken;
        }
    }
}
