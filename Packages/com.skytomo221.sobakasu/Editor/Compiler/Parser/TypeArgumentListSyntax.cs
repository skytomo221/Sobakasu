using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class TypeArgumentListSyntax : SyntaxNode
    {
        public SyntaxToken LessToken { get; }
        public IReadOnlyList<TypeSyntax> Arguments { get; }
        public IReadOnlyList<SyntaxToken> Separators { get; }
        public SyntaxToken GreaterToken { get; }

        public TypeArgumentListSyntax(
            SyntaxToken lessToken,
            IReadOnlyList<TypeSyntax> arguments,
            IReadOnlyList<SyntaxToken> separators,
            SyntaxToken greaterToken)
        {
            LessToken = lessToken;
            Arguments = arguments;
            Separators = separators;
            GreaterToken = greaterToken;
        }

        public string GetText()
        {
            var builder = new StringBuilder();
            builder.Append('<');
            for (var index = 0; index < Arguments.Count; index++)
            {
                if (index > 0)
                    builder.Append(", ");
                builder.Append(Arguments[index].GetText());
            }
            builder.Append('>');
            return builder.ToString();
        }
    }
}
