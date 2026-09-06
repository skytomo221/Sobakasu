using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class AggregateInitializerExpressionSyntax : ExpressionSyntax
    {
        public ExpressionSyntax Target { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<AggregateInitializerFieldSyntax> Fields { get; }
        public SyntaxToken CloseBraceToken { get; }

        public AggregateInitializerExpressionSyntax(
            ExpressionSyntax target,
            SyntaxToken openBraceToken,
            IReadOnlyList<AggregateInitializerFieldSyntax> fields,
            SyntaxToken closeBraceToken)
        {
            Target = target;
            OpenBraceToken = openBraceToken;
            Fields = fields;
            CloseBraceToken = closeBraceToken;
        }
    }
}
