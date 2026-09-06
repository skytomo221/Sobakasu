using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ModDeclarationSyntax : MemberSyntax
    {
        public SyntaxToken PubKeyword { get; }
        public SyntaxToken ModKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken SemicolonToken { get; }
        public bool IsMalformed { get; }
        public bool IsPublic => PubKeyword != null;

        public ModDeclarationSyntax(
            SyntaxToken pubKeyword,
            SyntaxToken modKeyword,
            SyntaxToken identifier,
            SyntaxToken semicolonToken,
            bool isMalformed)
        {
            PubKeyword = pubKeyword;
            ModKeyword = modKeyword;
            Identifier = identifier;
            SemicolonToken = semicolonToken;
            IsMalformed = isMalformed;
        }
    }
}
