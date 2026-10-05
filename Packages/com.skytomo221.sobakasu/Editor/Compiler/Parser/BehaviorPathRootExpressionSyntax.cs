using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    // Compile-time root for the current script's behavior callable namespace.
    sealed class BehaviorPathRootExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken BehaviorKeyword { get; }

        public BehaviorPathRootExpressionSyntax(SyntaxToken behaviorKeyword)
        {
            BehaviorKeyword = behaviorKeyword;
        }
    }
}
