using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ExternalAbiSignatureSyntax : SyntaxNode
    {
        public ExpressionSyntax Target { get; }
        public SyntaxToken NewKeyword { get; }
        public TypeSyntax ConstructorType { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<ExternalAbiParameterSyntax> Parameters { get; }
        public IReadOnlyList<SyntaxToken> Separators { get; }
        public SyntaxToken CloseParenToken { get; }
        public bool IsConstructor => NewKeyword != null;

        public ExternalAbiSignatureSyntax(
            ExpressionSyntax target,
            SyntaxToken newKeyword,
            TypeSyntax constructorType,
            SyntaxToken openParenToken,
            IReadOnlyList<ExternalAbiParameterSyntax> parameters,
            IReadOnlyList<SyntaxToken> separators,
            SyntaxToken closeParenToken)
        {
            Target = target;
            NewKeyword = newKeyword;
            ConstructorType = constructorType;
            OpenParenToken = openParenToken;
            Parameters = parameters;
            Separators = separators;
            CloseParenToken = closeParenToken;
        }
    }
}
