using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class LanguageItemSyntax : SyntaxNode
    {
        public Syntax.SyntaxToken LanguageKeyword { get; }
        public Syntax.SyntaxToken ItemKeyword { get; }
        public Syntax.SyntaxToken Item { get; }

        public LanguageItemSyntax(
            Syntax.SyntaxToken languageKeyword,
            Syntax.SyntaxToken itemKeyword,
            Syntax.SyntaxToken item)
        {
            LanguageKeyword = languageKeyword;
            ItemKeyword = itemKeyword;
            Item = item;
        }
    }
}
