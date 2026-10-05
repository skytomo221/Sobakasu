using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ReceiveDeclarationSyntax : MemberSyntax, IDocumentableSyntax
    {
        public DocumentationCommentSyntax Documentation { get; set; }
        public SyntaxToken PublicKeyword { get; }
        public SyntaxToken ReceiveKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<ParameterSyntax> Parameters { get; }
        public StateCapabilitySyntax StateCapability { get; }
        public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }
        public SyntaxToken CloseParenToken { get; }
        public FunctionReturnTypeSyntax RejectedReturnTypeAnnotation { get; }
        public BlockStatementSyntax Body { get; }

        public ReceiveDeclarationSyntax(
            SyntaxToken publicKeyword,
            SyntaxToken receiveKeyword,
            SyntaxToken identifier,
            SyntaxToken openParenToken,
            IReadOnlyList<ParameterSyntax> parameters,
            IReadOnlyList<SyntaxToken> parameterSeparators,
            SyntaxToken closeParenToken,
            FunctionReturnTypeSyntax rejectedReturnTypeAnnotation,
            BlockStatementSyntax body,
            StateCapabilitySyntax stateCapability = null)
        {
            PublicKeyword = publicKeyword;
            ReceiveKeyword = receiveKeyword;
            Identifier = identifier;
            OpenParenToken = openParenToken;
            Parameters = parameters;
            StateCapability = stateCapability;
            ParameterSeparators = parameterSeparators;
            CloseParenToken = closeParenToken;
            RejectedReturnTypeAnnotation = rejectedReturnTypeAnnotation;
            Body = body;
        }
    }
}
