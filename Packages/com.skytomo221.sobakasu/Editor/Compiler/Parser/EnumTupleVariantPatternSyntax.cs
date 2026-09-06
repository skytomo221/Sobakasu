using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class EnumTupleVariantPatternSyntax : EnumVariantPatternSyntax
    {
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<PatternBindingSyntax> Bindings { get; }
        public IReadOnlyList<SyntaxToken> Separators { get; }
        public SyntaxToken CloseParenToken { get; }

        public EnumTupleVariantPatternSyntax(
            TypeSyntax enumType,
            SyntaxToken dotToken,
            SyntaxToken variantIdentifier,
            SyntaxToken openParenToken,
            IReadOnlyList<PatternBindingSyntax> bindings,
            IReadOnlyList<SyntaxToken> separators,
            SyntaxToken closeParenToken)
            : base(enumType, dotToken, variantIdentifier)
        {
            OpenParenToken = openParenToken;
            Bindings = bindings;
            Separators = separators;
            CloseParenToken = closeParenToken;
        }
    }
}
