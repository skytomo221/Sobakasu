using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class EnumStructVariantPatternSyntax : EnumVariantPatternSyntax
    {
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<PatternBindingSyntax> Fields { get; }
        public IReadOnlyList<SyntaxToken> Separators { get; }
        public SyntaxToken CloseBraceToken { get; }

        public EnumStructVariantPatternSyntax(
            TypeSyntax enumType,
            SyntaxToken dotToken,
            SyntaxToken variantIdentifier,
            SyntaxToken openBraceToken,
            IReadOnlyList<PatternBindingSyntax> fields,
            IReadOnlyList<SyntaxToken> separators,
            SyntaxToken closeBraceToken)
            : base(enumType, dotToken, variantIdentifier)
        {
            OpenBraceToken = openBraceToken;
            Fields = fields;
            Separators = separators;
            CloseBraceToken = closeBraceToken;
        }
    }
}
