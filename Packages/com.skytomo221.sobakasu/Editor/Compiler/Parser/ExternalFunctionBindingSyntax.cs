using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class ExternalFunctionBindingSyntax : SyntaxNode
    {
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken MaybeKeyword { get; }
        public ExternExpressionSyntax ExternExpression { get; }
        public ExternalAbiSignatureSyntax AbiSignature { get; }
        public SyntaxToken SemicolonToken { get; }
        public bool IsMalformed { get; }
        public bool IsMaybe => MaybeKeyword != null;

        public ExternalFunctionBindingSyntax(
            SyntaxToken equalsToken,
            SyntaxToken maybeKeyword,
            ExternExpressionSyntax externExpression,
            bool isMalformed,
            ExternalAbiSignatureSyntax abiSignature = null,
            SyntaxToken semicolonToken = null)
        {
            EqualsToken = equalsToken;
            MaybeKeyword = maybeKeyword;
            ExternExpression = externExpression;
            AbiSignature = abiSignature;
            SemicolonToken = semicolonToken;
            IsMalformed = isMalformed;
        }
    }
}
