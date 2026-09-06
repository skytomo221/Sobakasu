using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ExternalAbiParameterSyntax : SyntaxNode
    {
        public SyntaxToken MaybeKeyword { get; }
        public SyntaxToken Modifier { get; }
        public TypeSyntax Type { get; }
        public SyntaxToken Identifier { get; }
        public bool IsMaybe => MaybeKeyword != null;

        public ExternalAbiParameterSyntax(
            SyntaxToken maybeKeyword,
            SyntaxToken modifier,
            TypeSyntax type,
            SyntaxToken identifier)
        {
            MaybeKeyword = maybeKeyword;
            Modifier = modifier;
            Type = type;
            Identifier = identifier;
        }
    }
}
