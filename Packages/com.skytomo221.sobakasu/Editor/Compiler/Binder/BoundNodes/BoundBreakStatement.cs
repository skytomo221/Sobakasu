using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundBreakStatement : BoundStatement
    {
        public LoopSymbol Target { get; }
        public BoundExpression Expression { get; }

        public BoundBreakStatement(
            LoopSymbol target,
            BoundExpression expression)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Expression = expression;
        }
    }
}
