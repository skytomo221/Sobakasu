using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class StateDeclarationSyntax : MemberSyntax
    {
        public Syntax.SyntaxToken PubKeyword { get; }
        public SynchronizationModifierSyntax SynchronizationModifier { get; }
        public Syntax.SyntaxToken StateKeyword { get; }
        public Syntax.SyntaxToken MutKeyword { get; }
        public Syntax.SyntaxToken Identifier { get; }
        public TypeClauseSyntax TypeClause { get; }
        public Syntax.SyntaxToken EqualsToken { get; }
        public ExpressionSyntax Initializer { get; }
        public Syntax.SyntaxToken SemicolonToken { get; }

        public StateDeclarationSyntax(
            Syntax.SyntaxToken pubKeyword,
            SynchronizationModifierSyntax synchronizationModifier,
            Syntax.SyntaxToken stateKeyword,
            Syntax.SyntaxToken mutKeyword,
            Syntax.SyntaxToken identifier,
            TypeClauseSyntax typeClause,
            Syntax.SyntaxToken equalsToken,
            ExpressionSyntax initializer,
            Syntax.SyntaxToken semicolonToken)
        {
            PubKeyword = pubKeyword;
            SynchronizationModifier = synchronizationModifier;
            StateKeyword = stateKeyword;
            MutKeyword = mutKeyword;
            Identifier = identifier;
            TypeClause = typeClause;
            EqualsToken = equalsToken;
            Initializer = initializer;
            SemicolonToken = semicolonToken;
        }
    }
}
