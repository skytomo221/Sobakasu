using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class EventParameterSyntax : ParameterSyntax
    {
        public EventParameterSyntax(
            SyntaxToken identifier,
            SyntaxToken colonToken,
            TypeSyntax type)
            : base(identifier, colonToken, type)
        {
        }
    }
}
