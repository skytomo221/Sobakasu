using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class EnumVariantDeclarationSyntax : SyntaxNode
    {
        public SyntaxToken Identifier { get; }
        public EnumVariantSyntaxKind VariantKind { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<TypeSyntax> TuplePayloadTypes { get; }
        public IReadOnlyList<SyntaxToken> TupleSeparators { get; }
        public SyntaxToken CloseParenToken { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<AggregateFieldDeclarationSyntax> NamedPayloadFields { get; }
        public SyntaxToken CloseBraceToken { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public SyntaxToken ExternalMemberName { get; }
        public bool IsExternalBinding => EqualsToken != null;
        public SyntaxToken CommaToken { get; }

        public EnumVariantDeclarationSyntax(
            SyntaxToken identifier,
            EnumVariantSyntaxKind variantKind,
            SyntaxToken openParenToken,
            IReadOnlyList<TypeSyntax> tuplePayloadTypes,
            IReadOnlyList<SyntaxToken> tupleSeparators,
            SyntaxToken closeParenToken,
            SyntaxToken openBraceToken,
            IReadOnlyList<AggregateFieldDeclarationSyntax> namedPayloadFields,
            SyntaxToken closeBraceToken,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            SyntaxToken externalMemberName,
            SyntaxToken commaToken)
        {
            Identifier = identifier;
            VariantKind = variantKind;
            OpenParenToken = openParenToken;
            TuplePayloadTypes = tuplePayloadTypes;
            TupleSeparators = tupleSeparators;
            CloseParenToken = closeParenToken;
            OpenBraceToken = openBraceToken;
            NamedPayloadFields = namedPayloadFields;
            CloseBraceToken = closeBraceToken;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalMemberName = externalMemberName;
            CommaToken = commaToken;
        }
    }
}
