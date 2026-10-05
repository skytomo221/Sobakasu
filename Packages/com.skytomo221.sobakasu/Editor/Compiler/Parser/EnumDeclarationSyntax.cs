using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class EnumDeclarationSyntax : MemberSyntax, IDocumentableSyntax
    {
        public DocumentationCommentSyntax Documentation { get; set; }
        public LanguageItemSyntax LanguageItem { get; }
        public SyntaxToken PublicKeyword { get; }
        public SyntaxToken EnumKeyword { get; }
        public SyntaxToken Identifier { get; }
        public GenericParameterListSyntax GenericParameters { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public ExternalQualifiedNameSyntax ExternalTypeName { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<EnumVariantDeclarationSyntax> Variants { get; }
        public SyntaxToken CloseBraceToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public EnumDeclarationSyntax(
            LanguageItemSyntax languageItem,
            SyntaxToken publicKeyword,
            SyntaxToken enumKeyword,
            SyntaxToken identifier,
            GenericParameterListSyntax genericParameters,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            ExternalQualifiedNameSyntax externalTypeName,
            SyntaxToken openBraceToken,
            IReadOnlyList<EnumVariantDeclarationSyntax> variants,
            SyntaxToken closeBraceToken)
        {
            LanguageItem = languageItem;
            PublicKeyword = publicKeyword;
            EnumKeyword = enumKeyword;
            Identifier = identifier;
            GenericParameters = genericParameters;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalTypeName = externalTypeName;
            OpenBraceToken = openBraceToken;
            Variants = variants;
            CloseBraceToken = closeBraceToken;
        }
    }
}
