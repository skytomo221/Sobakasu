using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class StructDeclarationSyntax : MemberSyntax
    {
        public LanguageItemSyntax LanguageItem { get; }
        public SyntaxToken PubKeyword { get; }
        public SyntaxToken StructKeyword { get; }
        public SyntaxToken Identifier { get; }
        public GenericParameterListSyntax GenericParameters { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public QualifiedNameSyntax ExternalTypeName { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<AggregateFieldDeclarationSyntax> Fields { get; }
        public SyntaxToken CloseBraceToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public StructDeclarationSyntax(
            LanguageItemSyntax languageItem,
            SyntaxToken pubKeyword,
            SyntaxToken structKeyword,
            SyntaxToken identifier,
            GenericParameterListSyntax genericParameters,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            QualifiedNameSyntax externalTypeName,
            SyntaxToken openBraceToken,
            IReadOnlyList<AggregateFieldDeclarationSyntax> fields,
            SyntaxToken closeBraceToken)
        {
            LanguageItem = languageItem;
            PubKeyword = pubKeyword;
            StructKeyword = structKeyword;
            Identifier = identifier;
            GenericParameters = genericParameters;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalTypeName = externalTypeName;
            OpenBraceToken = openBraceToken;
            Fields = fields;
            CloseBraceToken = closeBraceToken;
        }
    }
}
