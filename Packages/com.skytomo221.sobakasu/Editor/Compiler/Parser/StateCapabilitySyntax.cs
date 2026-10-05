using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class StateCapabilitySyntax : SyntaxNode
    {
        public SyntaxToken StateKeyword { get; }

        public StateCapabilitySyntax(SyntaxToken stateKeyword)
        {
            StateKeyword = stateKeyword;
        }
    }
}
