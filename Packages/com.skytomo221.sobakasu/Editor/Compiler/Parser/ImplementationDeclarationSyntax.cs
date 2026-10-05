using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class ImplementationDeclarationSyntax : MemberSyntax
    {
        public LanguageItemSyntax LanguageItem { get; }
        public Syntax.SyntaxToken PublicKeyword { get; }
        public Syntax.SyntaxToken ImplementationKeyword { get; }
        public GenericParameterListSyntax GenericParameters { get; }
        public TypeSyntax TargetType { get; }
        public Syntax.SyntaxToken EqualsToken { get; }
        public Syntax.SyntaxToken ExternKeyword { get; }
        public ExternalQualifiedNameSyntax ExternalTypeName { get; }
        public Syntax.SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<FunctionDeclarationSyntax> Methods { get; }
        public Syntax.SyntaxToken CloseBraceToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public ImplementationDeclarationSyntax(
            LanguageItemSyntax languageItem,
            Syntax.SyntaxToken publicKeyword,
            Syntax.SyntaxToken implementationKeyword,
            GenericParameterListSyntax genericParameters,
            TypeSyntax targetType,
            Syntax.SyntaxToken equalsToken,
            Syntax.SyntaxToken externKeyword,
            ExternalQualifiedNameSyntax externalTypeName,
            Syntax.SyntaxToken openBraceToken,
            IReadOnlyList<FunctionDeclarationSyntax> methods,
            Syntax.SyntaxToken closeBraceToken)
        {
            LanguageItem = languageItem;
            PublicKeyword = publicKeyword;
            ImplementationKeyword = implementationKeyword;
            GenericParameters = genericParameters;
            TargetType = targetType;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalTypeName = externalTypeName;
            OpenBraceToken = openBraceToken;
            Methods = methods;
            CloseBraceToken = closeBraceToken;
        }
    }
}
