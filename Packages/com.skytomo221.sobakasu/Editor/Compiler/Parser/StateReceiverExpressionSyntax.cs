using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    // Compile-time receiver marker. It is only valid as the receiver of a
    // state member access or as the explicit receiver in behavior:: calls.
    sealed class StateReceiverExpressionSyntax : ExpressionSyntax
    {
        public SyntaxToken StateKeyword { get; }

        public StateReceiverExpressionSyntax(SyntaxToken stateKeyword)
        {
            StateKeyword = stateKeyword;
        }
    }
}
