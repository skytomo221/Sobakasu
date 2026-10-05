using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class BehaviorDeclarationSyntax : MemberSyntax
    {
        public SyntaxToken BehaviorKeyword { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<MemberSyntax> Members { get; }
        public SyntaxToken CloseBraceToken { get; }

        public BehaviorDeclarationSyntax(SyntaxToken behaviorKeyword, SyntaxToken openBraceToken,
            IReadOnlyList<MemberSyntax> members, SyntaxToken closeBraceToken)
        {
            BehaviorKeyword = behaviorKeyword;
            OpenBraceToken = openBraceToken;
            Members = members;
            CloseBraceToken = closeBraceToken;
        }
    }
}
