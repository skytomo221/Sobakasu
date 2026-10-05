using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class UseDirectiveSyntax : MemberSyntax
    {
        public SyntaxToken PublicKeyword { get; }
        public SyntaxToken UseKeyword { get; }
        public UseTreeSyntax UseTree { get; }
        public QualifiedNameSyntax Path => UseTree.Path;
        public SyntaxToken AsKeyword => UseTree.AsKeyword;
        public SyntaxToken Alias => UseTree.Alias;
        public SyntaxToken SemicolonToken { get; }
        public bool IsMalformed { get; }
        public bool IsReExport => PublicKeyword != null;

        public UseDirectiveSyntax(
            SyntaxToken publicKeyword,
            SyntaxToken useKeyword,
            UseTreeSyntax useTree,
            SyntaxToken semicolonToken,
            bool isMalformed)
        {
            PublicKeyword = publicKeyword;
            UseKeyword = useKeyword;
            UseTree = useTree ?? throw new ArgumentNullException(nameof(useTree));
            SemicolonToken = semicolonToken;
            IsMalformed = isMalformed;
        }
    }
}
