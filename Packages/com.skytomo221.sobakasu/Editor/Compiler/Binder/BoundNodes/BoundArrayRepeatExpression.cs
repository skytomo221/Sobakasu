using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundArrayRepeatExpression : BoundExpression
    {
        public BoundExpression Operand { get; }
        public BoundExpression Length { get; }
        public bool UsesDefaultValue => Operand == null;
        public ArrayIntrinsicSymbols Intrinsics { get; }
        public BoundBinaryOperator IndexLessThanOperator { get; }
        public BoundBinaryOperator IndexIncrementOperator { get; }
        public IReadOnlyList<ArrayIntrinsicSymbols> AggregateLeafIntrinsics { get; }
        public override TypeSymbol Type { get; }

        public BoundArrayRepeatExpression(
            TypeSymbol arrayType,
            BoundExpression operand,
            BoundExpression length,
            ArrayIntrinsicSymbols intrinsics,
            BoundBinaryOperator indexLessThanOperator,
            BoundBinaryOperator indexIncrementOperator,
            IReadOnlyList<ArrayIntrinsicSymbols> aggregateLeafIntrinsics = null)
        {
            Type = arrayType ?? throw new ArgumentNullException(nameof(arrayType));
            Operand = operand;
            Length = length ?? throw new ArgumentNullException(nameof(length));
            Intrinsics = intrinsics;
            IndexLessThanOperator = indexLessThanOperator;
            IndexIncrementOperator = indexIncrementOperator;
            AggregateLeafIntrinsics = aggregateLeafIntrinsics;
        }
    }
}
