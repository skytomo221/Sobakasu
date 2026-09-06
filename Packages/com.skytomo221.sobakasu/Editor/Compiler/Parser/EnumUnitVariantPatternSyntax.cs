using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class EnumUnitVariantPatternSyntax : EnumVariantPatternSyntax
    {
        public EnumUnitVariantPatternSyntax(
            TypeSyntax enumType,
            SyntaxToken dotToken,
            SyntaxToken variantIdentifier)
            : base(enumType, dotToken, variantIdentifier)
        {
        }
    }
}
