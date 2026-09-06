using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundElementAssignmentExpression : BoundExpression
    {
        public BoundElementAccessExpression Target { get; }
        public BoundExpression Value { get; }
        public FunctionSymbol CompoundOperator { get; }
        public override TypeSymbol Type => Target.Type;

        public BoundElementAssignmentExpression(
            BoundElementAccessExpression target,
            BoundExpression value,
            FunctionSymbol compoundOperator = null)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            CompoundOperator = compoundOperator;
        }
    }
}
