using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class NewExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken NewKeyword { get; }
        public TypeSyntax Type { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<ExpressionSyntax> Arguments { get; }
        public SyntaxToken CloseParenToken { get; }

        public NewExpressionSyntax(
            SyntaxToken newKeyword,
            TypeSyntax type,
            SyntaxToken openParenToken,
            IReadOnlyList<ExpressionSyntax> arguments,
            SyntaxToken closeParenToken)
        {
            NewKeyword = newKeyword;
            Type = type;
            OpenParenToken = openParenToken;
            Arguments = arguments;
            CloseParenToken = closeParenToken;
        }
    }
}
