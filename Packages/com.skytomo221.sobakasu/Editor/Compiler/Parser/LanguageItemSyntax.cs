using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class LanguageItemSyntax : SyntaxNode
    {
        public Syntax.SyntaxToken LangKeyword { get; }
        public Syntax.SyntaxToken Item { get; }

        public LanguageItemSyntax(
            Syntax.SyntaxToken langKeyword,
            Syntax.SyntaxToken item)
        {
            LangKeyword = langKeyword;
            Item = item;
        }
    }
}
