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

    sealed class SelfParameterSyntax : ParameterSyntax
    {
        public SyntaxToken SelfKeyword => Identifier;
        public TypeSyntax RejectedTypeAnnotation => Type;

        public SelfParameterSyntax(
            SyntaxToken selfKeyword,
            SyntaxToken colonToken = null,
            TypeSyntax rejectedTypeAnnotation = null)
            : base(selfKeyword, colonToken, rejectedTypeAnnotation)
        {
        }
    }
}
