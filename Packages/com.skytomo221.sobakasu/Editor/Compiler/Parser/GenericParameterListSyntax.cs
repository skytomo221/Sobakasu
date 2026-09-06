using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class GenericParameterListSyntax : SyntaxNode
    {
        public SyntaxToken LessToken { get; }
        public IReadOnlyList<SyntaxToken> Parameters { get; }
        public IReadOnlyList<SyntaxToken> Separators { get; }
        public SyntaxToken GreaterToken { get; }

        public GenericParameterListSyntax(
            SyntaxToken lessToken,
            IReadOnlyList<SyntaxToken> parameters,
            IReadOnlyList<SyntaxToken> separators,
            SyntaxToken greaterToken)
        {
            LessToken = lessToken;
            Parameters = parameters;
            Separators = separators;
            GreaterToken = greaterToken;
        }
    }
}
