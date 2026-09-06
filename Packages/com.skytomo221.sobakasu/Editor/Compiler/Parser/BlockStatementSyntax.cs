using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class BlockStatementSyntax : StatementSyntax
    {
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<StatementSyntax> Statements { get; }
        public ExpressionSyntax TrailingExpression { get; }
        public SyntaxToken CloseBraceToken { get; }

        public BlockStatementSyntax(
            SyntaxToken openBraceToken,
            IReadOnlyList<StatementSyntax> statements,
            ExpressionSyntax trailingExpression,
            SyntaxToken closeBraceToken)
        {
            OpenBraceToken = openBraceToken;
            Statements = statements;
            TrailingExpression = trailingExpression;
            CloseBraceToken = closeBraceToken;
        }
    }
}
