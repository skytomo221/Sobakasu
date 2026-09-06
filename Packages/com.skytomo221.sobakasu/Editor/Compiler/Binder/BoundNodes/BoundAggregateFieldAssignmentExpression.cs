using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundAggregateFieldAssignmentExpression : BoundExpression
    {
        public BoundAggregateFieldAccessExpression Target { get; }
        public BoundExpression Value { get; }
        public FunctionSymbol CompoundOperator { get; }
        public override TypeSymbol Type => Target.Type;

        public BoundAggregateFieldAssignmentExpression(
            BoundAggregateFieldAccessExpression target,
            BoundExpression value,
            FunctionSymbol compoundOperator = null)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            CompoundOperator = compoundOperator;
        }
    }
}
