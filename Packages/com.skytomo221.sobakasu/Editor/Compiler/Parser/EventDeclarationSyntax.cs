using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class EventDeclarationSyntax : MemberSyntax
    {
        public SyntaxToken OnKeyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<ParameterSyntax> Parameters { get; }
        public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }
        public SyntaxToken CloseParenToken { get; }
        public TypeClauseSyntax ReturnTypeAnnotation { get; }
        public BlockStatementSyntax Body { get; }

        public EventDeclarationSyntax(
            SyntaxToken onKeyword,
            SyntaxToken identifier,
            SyntaxToken openParenToken,
            IReadOnlyList<ParameterSyntax> parameters,
            IReadOnlyList<SyntaxToken> parameterSeparators,
            SyntaxToken closeParenToken,
            TypeClauseSyntax returnTypeAnnotation,
            BlockStatementSyntax body)
        {
            OnKeyword = onKeyword;
            Identifier = identifier;
            OpenParenToken = openParenToken;
            Parameters = parameters;
            ParameterSeparators = parameterSeparators;
            CloseParenToken = closeParenToken;
            ReturnTypeAnnotation = returnTypeAnnotation;
            Body = body;
        }
    }
}
