using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class SendStatementSyntax : StatementSyntax
    {
        public SyntaxToken SendKeyword { get; }
        public SyntaxToken ReceiverName { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<ExpressionSyntax> Arguments { get; }
        public IReadOnlyList<SyntaxToken> ArgumentSeparators { get; }
        public SyntaxToken CloseParenToken { get; }
        public SyntaxToken ToKeyword { get; }
        public ExpressionSyntax Target { get; }
        public SyntaxToken SemicolonToken { get; }

        public SendStatementSyntax(
            SyntaxToken sendKeyword,
            SyntaxToken receiverName,
            SyntaxToken openParenToken,
            IReadOnlyList<ExpressionSyntax> arguments,
            IReadOnlyList<SyntaxToken> argumentSeparators,
            SyntaxToken closeParenToken,
            SyntaxToken toKeyword,
            ExpressionSyntax target,
            SyntaxToken semicolonToken)
        {
            SendKeyword = sendKeyword;
            ReceiverName = receiverName;
            OpenParenToken = openParenToken;
            Arguments = arguments;
            ArgumentSeparators = argumentSeparators;
            CloseParenToken = closeParenToken;
            ToKeyword = toKeyword;
            Target = target;
            SemicolonToken = semicolonToken;
        }
    }
}
