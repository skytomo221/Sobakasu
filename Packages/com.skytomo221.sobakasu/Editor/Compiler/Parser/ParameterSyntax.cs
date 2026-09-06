using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    class ParameterSyntax : SyntaxNode
    {
        public SyntaxToken Identifier { get; }
        public SyntaxToken ColonToken { get; }
        public TypeSyntax Type { get; }

        public ParameterSyntax(
            SyntaxToken identifier,
            SyntaxToken colonToken,
            TypeSyntax type)
        {
            Identifier = identifier;
            ColonToken = colonToken;
            Type = type;
        }
    }
}
