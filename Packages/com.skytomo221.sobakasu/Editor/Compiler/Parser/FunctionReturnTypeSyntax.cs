using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class FunctionReturnTypeSyntax : SyntaxNode
    {
        public SyntaxToken ArrowToken { get; }
        public TypeSyntax Type { get; }

        public FunctionReturnTypeSyntax(
            SyntaxToken arrowToken,
            TypeSyntax type)
        {
            ArrowToken = arrowToken;
            Type = type;
        }
    }
}
