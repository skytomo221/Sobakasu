using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class EnumDeclarationSyntax : MemberSyntax
    {
        public LanguageItemSyntax LanguageItem { get; }
        public SyntaxToken PubKeyword { get; }
        public SyntaxToken EnumKeyword { get; }
        public SyntaxToken Identifier { get; }
        public GenericParameterListSyntax GenericParameters { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public QualifiedNameSyntax ExternalTypeName { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<EnumVariantDeclarationSyntax> Variants { get; }
        public SyntaxToken CloseBraceToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public EnumDeclarationSyntax(
            LanguageItemSyntax languageItem,
            SyntaxToken pubKeyword,
            SyntaxToken enumKeyword,
            SyntaxToken identifier,
            GenericParameterListSyntax genericParameters,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            QualifiedNameSyntax externalTypeName,
            SyntaxToken openBraceToken,
            IReadOnlyList<EnumVariantDeclarationSyntax> variants,
            SyntaxToken closeBraceToken)
        {
            LanguageItem = languageItem;
            PubKeyword = pubKeyword;
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
