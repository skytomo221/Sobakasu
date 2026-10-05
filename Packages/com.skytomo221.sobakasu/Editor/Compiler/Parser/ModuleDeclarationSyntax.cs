using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ModuleDeclarationSyntax : MemberSyntax
    {
        public SyntaxToken PublicKeyword { get; }
        public SyntaxToken ModuleKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken SemicolonToken { get; }
        public bool IsMalformed { get; }
        public bool IsPublic => PublicKeyword != null;

        public ModuleDeclarationSyntax(
            SyntaxToken publicKeyword,
            SyntaxToken moduleKeyword,
            SyntaxToken identifier,
            SyntaxToken semicolonToken,
            bool isMalformed)
        {
            PublicKeyword = publicKeyword;
            ModuleKeyword = moduleKeyword;
            Identifier = identifier;
            SemicolonToken = semicolonToken;
            IsMalformed = isMalformed;
        }
    }
}
