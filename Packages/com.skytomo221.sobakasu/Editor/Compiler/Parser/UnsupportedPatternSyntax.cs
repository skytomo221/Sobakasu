using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class UnsupportedPatternSyntax : PatternSyntax
    {
        public SyntaxToken Token { get; }

        public UnsupportedPatternSyntax(SyntaxToken token)
        {
            Token = token;
        }
    }
}
