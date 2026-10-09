using Skytomo221.Sobakasu.Compiler.Text;
using System;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class SendStatementSyntax : StatementSyntax
    {
        public SyntaxToken SendKeyword { get; }
        public CallExpressionSyntax Call { get; }
        public SyntaxToken ToKeyword { get; }
        public ExpressionSyntax Target { get; }
        public SyntaxToken SemicolonToken { get; }

        public SendStatementSyntax(
            SyntaxToken sendKeyword,
            CallExpressionSyntax call,
            SyntaxToken toKeyword,
            ExpressionSyntax target,
            SyntaxToken semicolonToken)
        {
            SendKeyword = sendKeyword;
            Call = call;
            ToKeyword = toKeyword;
            Target = target;
            SemicolonToken = semicolonToken;
        }
    }
}
