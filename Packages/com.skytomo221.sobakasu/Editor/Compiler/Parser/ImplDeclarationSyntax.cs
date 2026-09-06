using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class ImplDeclarationSyntax : MemberSyntax
    {
        public LanguageItemSyntax LanguageItem { get; }
        public Syntax.SyntaxToken PubKeyword { get; }
        public Syntax.SyntaxToken ImplKeyword { get; }
        public GenericParameterListSyntax GenericParameters { get; }
        public TypeSyntax TargetType { get; }
        public Syntax.SyntaxToken EqualsToken { get; }
        public Syntax.SyntaxToken ExternKeyword { get; }
        public QualifiedNameSyntax ExternalTypeName { get; }
        public Syntax.SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<FunctionDeclarationSyntax> Methods { get; }
        public Syntax.SyntaxToken CloseBraceToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public ImplDeclarationSyntax(
            LanguageItemSyntax languageItem,
            Syntax.SyntaxToken pubKeyword,
            Syntax.SyntaxToken implKeyword,
            GenericParameterListSyntax genericParameters,
            TypeSyntax targetType,
            Syntax.SyntaxToken equalsToken,
            Syntax.SyntaxToken externKeyword,
            QualifiedNameSyntax externalTypeName,
            Syntax.SyntaxToken openBraceToken,
            IReadOnlyList<FunctionDeclarationSyntax> methods,
            Syntax.SyntaxToken closeBraceToken)
        {
            LanguageItem = languageItem;
            PubKeyword = pubKeyword;
            ImplKeyword = implKeyword;
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
