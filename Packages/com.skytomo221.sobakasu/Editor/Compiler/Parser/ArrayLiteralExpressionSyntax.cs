using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ArrayLiteralExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken OpenBracketToken { get; }
        public IReadOnlyList<ExpressionSyntax> Elements { get; }
        public IReadOnlyList<SyntaxToken> SeparatorTokens { get; }
        public SyntaxToken RepeatSeparatorToken { get; }
        public ExpressionSyntax RepeatLength { get; }
        public SyntaxToken CloseBracketToken { get; }
        public bool IsRepeat => RepeatSeparatorToken != null;
        public ExpressionSyntax RepeatOperand => Elements.Count == 0 ? null : Elements[0];

        public ArrayLiteralExpressionSyntax(
            SyntaxToken openBracketToken,
            IReadOnlyList<ExpressionSyntax> elements,
            IReadOnlyList<SyntaxToken> separatorTokens,
            SyntaxToken closeBracketToken,
            SyntaxToken repeatSeparatorToken = null,
            ExpressionSyntax repeatLength = null)
        {
            OpenBracketToken = openBracketToken;
            Elements = elements;
            SeparatorTokens = separatorTokens;
            RepeatSeparatorToken = repeatSeparatorToken;
            RepeatLength = repeatLength;
            CloseBracketToken = closeBracketToken;
        }
    }
}
