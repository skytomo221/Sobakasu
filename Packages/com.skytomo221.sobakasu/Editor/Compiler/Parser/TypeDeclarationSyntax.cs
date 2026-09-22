using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class TypeDeclarationSyntax : MemberSyntax
    {
        public LanguageItemSyntax LanguageItem { get; }
        public SyntaxToken PubKeyword { get; }
        public SyntaxToken TypeKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public ExternalQualifiedNameSyntax ExternalTypeName { get; }
        public SyntaxToken SemicolonToken { get; }

        public TypeDeclarationSyntax(
            LanguageItemSyntax languageItem,
            SyntaxToken pubKeyword,
            SyntaxToken typeKeyword,
            SyntaxToken identifier,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            ExternalQualifiedNameSyntax externalTypeName,
            SyntaxToken semicolonToken)
        {
            LanguageItem = languageItem;
            PubKeyword = pubKeyword;
            TypeKeyword = typeKeyword;
            Identifier = identifier;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalTypeName = externalTypeName;
            SemicolonToken = semicolonToken;
        }
    }
}
