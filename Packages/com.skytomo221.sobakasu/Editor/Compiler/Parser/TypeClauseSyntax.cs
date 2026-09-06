using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class TypeClauseSyntax : SyntaxNode
    {
        public SyntaxToken ColonToken { get; }
        public TypeSyntax Type { get; }

        public TypeClauseSyntax(
            SyntaxToken colonToken,
            TypeSyntax type)
        {
            ColonToken = colonToken;
            Type = type;
        }
    }
}
