using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ReceiveDeclarationSyntax : MemberSyntax
    {
        public SyntaxToken ReceiveKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<ParameterSyntax> Parameters { get; }
        public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }
        public SyntaxToken CloseParenToken { get; }
        public FunctionReturnTypeSyntax RejectedReturnTypeAnnotation { get; }
        public BlockStatementSyntax Body { get; }

        public ReceiveDeclarationSyntax(
            SyntaxToken receiveKeyword,
            SyntaxToken identifier,
            SyntaxToken openParenToken,
            IReadOnlyList<ParameterSyntax> parameters,
            IReadOnlyList<SyntaxToken> parameterSeparators,
            SyntaxToken closeParenToken,
            FunctionReturnTypeSyntax rejectedReturnTypeAnnotation,
            BlockStatementSyntax body)
        {
            ReceiveKeyword = receiveKeyword;
            Identifier = identifier;
            OpenParenToken = openParenToken;
            Parameters = parameters;
            ParameterSeparators = parameterSeparators;
            CloseParenToken = closeParenToken;
            RejectedReturnTypeAnnotation = rejectedReturnTypeAnnotation;
            Body = body;
        }
    }
}
