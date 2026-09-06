using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class NameBindingPatternSyntax : BindingPatternSyntax
    {
        public SyntaxToken Identifier { get; }
        public bool IsDiscard => Identifier.Text == "_";

        public NameBindingPatternSyntax(SyntaxToken identifier)
        {
            Identifier = identifier;
        }
    }
}
