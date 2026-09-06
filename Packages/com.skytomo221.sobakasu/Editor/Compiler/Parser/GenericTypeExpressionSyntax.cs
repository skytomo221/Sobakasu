using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    sealed class GenericTypeExpressionSyntax : ExpressionSyntax
    {
        public ExpressionSyntax Target { get; }
        public TypeArgumentListSyntax TypeArgumentList { get; }

        public GenericTypeExpressionSyntax(
            ExpressionSyntax target,
            TypeArgumentListSyntax typeArgumentList)
        {
            Target = target;
            TypeArgumentList = typeArgumentList;
        }
    }
}
