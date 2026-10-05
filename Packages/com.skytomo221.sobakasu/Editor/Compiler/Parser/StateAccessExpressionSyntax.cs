using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class StateAccessExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken StateKeyword { get; }
        public SyntaxToken DotToken { get; }
        public SyntaxToken Name { get; }

        public StateAccessExpressionSyntax(SyntaxToken stateKeyword, SyntaxToken dotToken, SyntaxToken name)
        {
            StateKeyword = stateKeyword;
            DotToken = dotToken;
            Name = name;
        }
    }
}
