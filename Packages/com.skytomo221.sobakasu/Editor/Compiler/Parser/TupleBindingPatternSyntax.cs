using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class TupleBindingPatternSyntax : BindingPatternSyntax
    {
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<BindingPatternSyntax> Elements { get; }
        public IReadOnlyList<SyntaxToken> Separators { get; }
        public SyntaxToken CloseParenToken { get; }

        public TupleBindingPatternSyntax(
            SyntaxToken openParenToken,
            IReadOnlyList<BindingPatternSyntax> elements,
            IReadOnlyList<SyntaxToken> separators,
            SyntaxToken closeParenToken)
        {
            OpenParenToken = openParenToken;
            Elements = elements;
            Separators = separators;
            CloseParenToken = closeParenToken;
        }
    }
}
