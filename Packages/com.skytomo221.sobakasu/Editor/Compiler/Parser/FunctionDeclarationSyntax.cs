using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class FunctionDeclarationSyntax : MemberSyntax, IDocumentableSyntax
    {
        public DocumentationCommentSyntax Documentation { get; set; }
        public SyntaxToken PublicKeyword { get; }
        public SyntaxToken FunctionKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken QuestionToken { get; }
        public SyntaxToken AtToken { get; }
        public SyntaxToken OperatorToken { get; }
        public GenericParameterListSyntax GenericParameters { get; }
        public string Name =>
            OperatorToken != null
                ? (AtToken == null ? string.Empty : "@") + (OperatorToken.Text ?? string.Empty)
                : (Identifier?.Text ?? string.Empty) +
                  (QuestionToken == null ? string.Empty : "?");
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<ParameterSyntax> Parameters { get; }
        public StateCapabilitySyntax StateCapability { get; }
        public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }
        public SyntaxToken CloseParenToken { get; }
        public FunctionReturnTypeSyntax ReturnTypeAnnotation { get; }
        public BlockStatementSyntax Body { get; }
        public ExternalFunctionBindingSyntax ExternalBinding { get; }
        public bool IsExternalBinding => ExternalBinding != null;

        public FunctionDeclarationSyntax(
            SyntaxToken publicKeyword,
            SyntaxToken functionKeyword,
            SyntaxToken identifier,
            SyntaxToken questionToken,
            SyntaxToken atToken,
            SyntaxToken operatorToken,
            GenericParameterListSyntax genericParameters,
            SyntaxToken openParenToken,
            IReadOnlyList<ParameterSyntax> parameters,
            IReadOnlyList<SyntaxToken> parameterSeparators,
            SyntaxToken closeParenToken,
            FunctionReturnTypeSyntax returnTypeAnnotation,
            BlockStatementSyntax body,
            ExternalFunctionBindingSyntax externalBinding = null,
            StateCapabilitySyntax stateCapability = null)
        {
            PublicKeyword = publicKeyword;
            FunctionKeyword = functionKeyword;
            Identifier = identifier;
            QuestionToken = questionToken;
            AtToken = atToken;
            OperatorToken = operatorToken;
            GenericParameters = genericParameters;
            OpenParenToken = openParenToken;
            Parameters = parameters;
            StateCapability = stateCapability;
            ParameterSeparators = parameterSeparators;
            CloseParenToken = closeParenToken;
            ReturnTypeAnnotation = returnTypeAnnotation;
            Body = body;
            ExternalBinding = externalBinding;
        }
    }
}
