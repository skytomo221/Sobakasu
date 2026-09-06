using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class WildcardPatternSyntax : PatternSyntax
    {
        public SyntaxToken UnderscoreToken { get; }

        public WildcardPatternSyntax(SyntaxToken underscoreToken)
        {
            UnderscoreToken = underscoreToken;
        }
    }
}
