using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class PatternBindingSyntax : SyntaxNode
    {
        public SyntaxToken Identifier { get; }
        public bool IsWildcard => Identifier.Text == "_";
        public bool IsSupported { get; }

        public PatternBindingSyntax(
            SyntaxToken identifier,
            bool isSupported = true)
        {
            Identifier = identifier;
            IsSupported = isSupported;
        }
    }
}
