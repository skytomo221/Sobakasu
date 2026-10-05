using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class ConstDeclarationSyntax : MemberSyntax, IDocumentableSyntax
    {
        public DocumentationCommentSyntax Documentation { get; set; }
        public Syntax.SyntaxToken PublicKeyword { get; }
        public SynchronizationModifierSyntax RejectedSynchronizationModifier { get; }
        public Syntax.SyntaxToken ConstKeyword { get; }
        public Syntax.SyntaxToken Identifier { get; }
        public TypeClauseSyntax TypeClause { get; }
        public Syntax.SyntaxToken EqualsToken { get; }
        public ExpressionSyntax Initializer { get; }
        public Syntax.SyntaxToken SemicolonToken { get; }

        public ConstDeclarationSyntax(
            Syntax.SyntaxToken publicKeyword,
            SynchronizationModifierSyntax rejectedSynchronizationModifier,
            Syntax.SyntaxToken constKeyword,
            Syntax.SyntaxToken identifier,
            TypeClauseSyntax typeClause,
            Syntax.SyntaxToken equalsToken,
            ExpressionSyntax initializer,
            Syntax.SyntaxToken semicolonToken)
        {
            PublicKeyword = publicKeyword;
            RejectedSynchronizationModifier = rejectedSynchronizationModifier;
            ConstKeyword = constKeyword;
            Identifier = identifier;
            TypeClause = typeClause;
            EqualsToken = equalsToken;
            Initializer = initializer;
            SemicolonToken = semicolonToken;
        }
    }
}
