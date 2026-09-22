using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    // A path is resolved from a module, namespace, or type. It is deliberately
    // distinct from MemberAccessExpressionSyntax, whose receiver is a value.
    sealed class PathExpressionSyntax : ExpressionSyntax
    {
        public ExpressionSyntax Expression { get; }
        public SyntaxToken DoubleColonToken { get; }
        public SyntaxToken Name { get; }
        public SyntaxToken QuestionToken { get; }
        public string MemberName =>
            (Name.Text ?? string.Empty) +
            (QuestionToken == null ? string.Empty : "?");

        public PathExpressionSyntax(
            ExpressionSyntax expression,
            SyntaxToken doubleColonToken,
            SyntaxToken name,
            SyntaxToken questionToken = null)
        {
            Expression = expression;
            DoubleColonToken = doubleColonToken;
            Name = name;
            QuestionToken = questionToken;
        }
    }
}
