using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    abstract class EnumVariantPatternSyntax : PatternSyntax
    {
        protected EnumVariantPatternSyntax(
            TypeSyntax enumType,
            SyntaxToken dotToken,
            SyntaxToken variantIdentifier)
        {
            EnumType = enumType;
            DotToken = dotToken;
            VariantIdentifier = variantIdentifier;
        }

        public TypeSyntax EnumType { get; }
        public SyntaxToken DotToken { get; }
        public SyntaxToken VariantIdentifier { get; }
    }
}
