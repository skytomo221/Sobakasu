using System.Collections.Generic;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    // A CLR/Udon identity is not a Sobakasu path. Its namespace separator is
    // '.', while '+' retains the CLR spelling for nested types.
    sealed class ExternalQualifiedNameSyntax : SyntaxNode
    {
        public IReadOnlyList<SyntaxToken> Identifiers { get; }
        public IReadOnlyList<SyntaxToken> SeparatorTokens { get; }

        public ExternalQualifiedNameSyntax(
            IReadOnlyList<SyntaxToken> identifiers,
            IReadOnlyList<SyntaxToken> separatorTokens)
        {
            Identifiers = identifiers;
            SeparatorTokens = separatorTokens;
        }

        public string GetText()
        {
            var builder = new StringBuilder();
            for (var index = 0; index < Identifiers.Count; index++)
            {
                if (index > 0)
                    builder.Append(SeparatorTokens[index - 1].Text);
                builder.Append(Identifiers[index].Text);
            }

            return builder.ToString();
        }
    }
}
