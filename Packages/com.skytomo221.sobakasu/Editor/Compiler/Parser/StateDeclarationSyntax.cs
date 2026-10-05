using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class StateDeclarationSyntax : MemberSyntax, IDocumentableSyntax
    {
        public DocumentationCommentSyntax Documentation { get; set; }
        public Syntax.SyntaxToken PublicKeyword { get; }
        public SynchronizationModifierSyntax SynchronizationModifier { get; }
        public Syntax.SyntaxToken StateKeyword { get; }
        public Syntax.SyntaxToken MutableKeyword { get; }
        public Syntax.SyntaxToken Identifier { get; }
        public TypeClauseSyntax TypeClause { get; }
        public Syntax.SyntaxToken EqualsToken { get; }
        public ExpressionSyntax Initializer { get; }
        public Syntax.SyntaxToken FieldKeyword { get; }
        public Syntax.SyntaxToken SemicolonToken { get; }

        public StateDeclarationSyntax(
            Syntax.SyntaxToken publicKeyword,
            SynchronizationModifierSyntax synchronizationModifier,
            Syntax.SyntaxToken stateKeyword,
            Syntax.SyntaxToken mutableKeyword,
            Syntax.SyntaxToken identifier,
            TypeClauseSyntax typeClause,
            Syntax.SyntaxToken equalsToken,
            ExpressionSyntax initializer,
            Syntax.SyntaxToken semicolonToken,
            Syntax.SyntaxToken fieldKeyword = null)
        {
            PublicKeyword = publicKeyword;
            SynchronizationModifier = synchronizationModifier;
            StateKeyword = stateKeyword;
            MutableKeyword = mutableKeyword;
            Identifier = identifier;
            TypeClause = typeClause;
            EqualsToken = equalsToken;
            Initializer = initializer;
            FieldKeyword = fieldKeyword;
            SemicolonToken = semicolonToken;
        }
    }
}
