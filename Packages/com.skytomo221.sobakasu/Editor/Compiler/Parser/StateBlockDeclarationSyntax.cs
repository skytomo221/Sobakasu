using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class StateBlockDeclarationSyntax : MemberSyntax
    {
        public SyntaxToken StateKeyword { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<StateDeclarationSyntax> Members { get; }
        public SyntaxToken CloseBraceToken { get; }

        public StateBlockDeclarationSyntax(SyntaxToken stateKeyword, SyntaxToken openBraceToken,
            IReadOnlyList<StateDeclarationSyntax> members, SyntaxToken closeBraceToken)
        {
            StateKeyword = stateKeyword;
            OpenBraceToken = openBraceToken;
            Members = members;
            CloseBraceToken = closeBraceToken;
        }
    }
}
