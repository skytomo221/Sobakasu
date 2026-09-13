using System;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class ArrayLowerer : LoweringComponent
    {
        public ArrayLowerer(LoweringSession session) : base(session) { }
        internal IrValue LowerArrayLiteralExpression(
            BoundArrayLiteralExpression expression,
            LoweringContext context)
        {
            if (IsAggregateStorageType(expression.Type))
                return LowerAggregateArrayLiteralExpression(expression, context);

            var result = context.CreateTemporary(expression.Type);
            context.Emit(new IrExternCallInstruction(
                expression.Intrinsics.ConstructorExternSignature,
                new IrValue[]
                {
            new IrConstantValue(
                expression.Elements.Count,
                expression.Intrinsics.IndexType)
                },
                result));

            for (var index = 0; index < expression.Elements.Count; index++)
            {
                var element = LowerValueExpression(
                    expression.Elements[index],
                    context,
                    expression.ElementType);
                if (element == null)
                    return null;

                context.Emit(new IrExternCallInstruction(
                    expression.Intrinsics.SetterExternSignature,
                    new IrValue[]
                    {
              result,
              new IrConstantValue(index, expression.Intrinsics.IndexType),
              element
                    },
                    null));
            }

            return result;
        }

        internal IrValue LowerArrayRepeatExpression(
            BoundArrayRepeatExpression expression,
            LoweringContext context)
        {
            if (IsAggregateStorageType(expression.Type))
                return LowerAggregateArrayRepeatExpression(expression, context);

            var loweredLength = LowerValueExpression(
                expression.Length,
                context,
                expression.Intrinsics.IndexType);
            if (loweredLength == null)
                return null;

            var length = context.CreateTemporary(expression.Intrinsics.IndexType);
            context.EmitCopy(length, loweredLength);

            var result = context.CreateTemporary(expression.Type);
            context.Emit(new IrExternCallInstruction(
                expression.Intrinsics.ConstructorExternSignature,
                new IrValue[] { length },
                result));

            if (expression.UsesDefaultValue)
                return result;

            var index = context.CreateTemporary(expression.Intrinsics.IndexType);
            context.EmitCopy(
                index,
                new IrConstantValue(0, expression.Intrinsics.IndexType));

            var conditionBlock = context.CreateBlock("array_repeat_condition");
            var bodyBlock = context.CreateBlock("array_repeat_body");
            var exitBlock = context.CreateBlock("array_repeat_exit");
            context.TerminateWithJump(conditionBlock.Label);

            context.SwitchTo(conditionBlock);
            var condition = context.CreateTemporary(TypeSymbol.Bool);
            context.Emit(new IrExternCallInstruction(
                expression.IndexLessThanOperator.ExternSignature,
                new IrValue[] { index, length },
                condition));
            context.TerminateWithCondition(
                condition,
                bodyBlock.Label,
                exitBlock.Label);

            context.SwitchTo(bodyBlock);
            var element = LowerValueExpression(
                expression.Operand,
                context,
                expression.Type.ElementType);
            if (element == null)
                return null;

            context.Emit(new IrExternCallInstruction(
                expression.Intrinsics.SetterExternSignature,
                new IrValue[] { result, index, element },
                null));

            var nextIndex = context.CreateTemporary(expression.Intrinsics.IndexType);
            context.Emit(new IrExternCallInstruction(
                expression.IndexIncrementOperator.ExternSignature,
                new IrValue[]
                {
            index,
            new IrConstantValue(1, expression.Intrinsics.IndexType)
                },
                nextIndex));
            context.EmitCopy(index, nextIndex);
            context.TerminateWithJump(conditionBlock.Label);

            context.SwitchTo(exitBlock);
            return result;
        }

        internal IrValue LowerElementAccessExpression(
            BoundElementAccessExpression expression,
            LoweringContext context)
        {
            if (IsAggregateStorageType(expression.Array.Type))
                return LowerAggregateArrayElementAccess(expression, context);

            var array = LowerValueExpression(
                expression.Array,
                context,
                expression.Array.Type);
            if (array == null)
                return null;

            var index = LowerValueExpression(
                expression.Index,
                context,
                expression.Intrinsics.IndexType);
            if (index == null)
                return null;

            var result = context.CreateTemporary(expression.Type);
            context.Emit(new IrExternCallInstruction(
                expression.Intrinsics.GetterExternSignature,
                new IrValue[] { array, index },
                result));
            return result;
        }

        internal IrValue LowerArrayLengthExpression(
            BoundArrayLengthExpression expression,
            LoweringContext context)
        {
            if (IsAggregateStorageType(expression.Array.Type))
            {
                var aggregateArray = LowerValueExpression(
                    expression.Array,
                    context,
                    expression.Array.Type);
                if (aggregateArray == null)
                    return null;
                var leaves = GetAggregateLeaves(aggregateArray);
                if (leaves.Count == 0 || expression.AggregateLeafIntrinsics?.Count == 0)
                    return null;
                var aggregateResult = context.CreateTemporary(TypeSymbol.I32);
                context.Emit(new IrExternCallInstruction(
                    expression.AggregateLeafIntrinsics[0].LengthExternSignature,
                    new[] { leaves[0] },
                    aggregateResult));
                return aggregateResult;
            }

            var array = LowerValueExpression(
                expression.Array,
                context,
                expression.Array.Type);
            if (array == null)
                return null;

            var result = context.CreateTemporary(expression.Type);
            context.Emit(new IrExternCallInstruction(
                expression.Intrinsics.LengthExternSignature,
                new IrValue[] { array },
                result));
            return result;
        }

        internal IrValue LowerAggregateArrayLiteralExpression(
            BoundArrayLiteralExpression expression,
            LoweringContext context)
        {
            if (context.CreateTemporary(expression.Type) is not IrAggregateStorage result || expression.AggregateLeafIntrinsics == null)
                return null;

            for (var leafIndex = 0; leafIndex < result.Leaves.Count; leafIndex++)
            {
                context.Emit(new IrExternCallInstruction(
                    expression.AggregateLeafIntrinsics[leafIndex].ConstructorExternSignature,
                    new IrValue[]
                    {
              new IrConstantValue(expression.Elements.Count, TypeSymbol.I32)
                    },
                    result.Leaves[leafIndex]));
            }

            for (var index = 0; index < expression.Elements.Count; index++)
            {
                var element = LowerValueExpression(
                    expression.Elements[index],
                    context,
                    expression.ElementType);
                if (element == null)
                    return null;
                Session.AggregateStorageLowerer.EmitArrayElementSet(
                    result.Leaves,
                    expression.AggregateLeafIntrinsics,
                    new IrConstantValue(index, TypeSymbol.I32),
                    expression.ElementType,
                    element,
                    context);
            }

            return result;
        }

        internal IrValue LowerAggregateArrayRepeatExpression(
            BoundArrayRepeatExpression expression,
            LoweringContext context)
        {
            var loweredLength = LowerValueExpression(expression.Length, context, TypeSymbol.I32);
            if (loweredLength == null)
                return null;
            var length = context.CreateTemporary(TypeSymbol.I32);
            context.EmitCopy(length, loweredLength);

            if (context.CreateTemporary(expression.Type) is not IrAggregateStorage result || expression.AggregateLeafIntrinsics == null)
                return null;
            for (var leafIndex = 0; leafIndex < result.Leaves.Count; leafIndex++)
            {
                context.Emit(new IrExternCallInstruction(
                    expression.AggregateLeafIntrinsics[leafIndex].ConstructorExternSignature,
                    new[] { (IrValue)length },
                    result.Leaves[leafIndex]));
            }

            if (expression.UsesDefaultValue)
                return result;

            var index = context.CreateTemporary(TypeSymbol.I32);
            context.EmitCopy(index, new IrConstantValue(0, TypeSymbol.I32));
            var conditionBlock = context.CreateBlock("aggregate_array_repeat_condition");
            var bodyBlock = context.CreateBlock("aggregate_array_repeat_body");
            var exitBlock = context.CreateBlock("aggregate_array_repeat_exit");
            context.TerminateWithJump(conditionBlock.Label);

            context.SwitchTo(conditionBlock);
            var condition = context.CreateTemporary(TypeSymbol.Bool);
            context.Emit(new IrExternCallInstruction(
                expression.IndexLessThanOperator.ExternSignature,
                new IrValue[] { index, length },
                condition));
            context.TerminateWithCondition(condition, bodyBlock.Label, exitBlock.Label);

            context.SwitchTo(bodyBlock);
            var element = LowerValueExpression(
                expression.Operand,
                context,
                expression.Type.ElementType);
            if (element == null)
                return null;
            Session.AggregateStorageLowerer.EmitArrayElementSet(
                result.Leaves,
                expression.AggregateLeafIntrinsics,
                index,
                expression.Type.ElementType,
                element,
                context);
            var nextIndex = context.CreateTemporary(TypeSymbol.I32);
            context.Emit(new IrExternCallInstruction(
                expression.IndexIncrementOperator.ExternSignature,
                new IrValue[] { index, new IrConstantValue(1, TypeSymbol.I32) },
                nextIndex));
            context.EmitCopy(index, nextIndex);
            context.TerminateWithJump(conditionBlock.Label);

            context.SwitchTo(exitBlock);
            return result;
        }

        internal IrValue LowerAggregateArrayElementAccess(
            BoundElementAccessExpression expression,
            LoweringContext context)
        {
            var array = LowerValueExpression(expression.Array, context, expression.Array.Type);
            if (array == null || expression.AggregateLeafIntrinsics == null)
                return null;
            var arrayLeaves = GetAggregateLeaves(array);

            var loweredIndex = LowerValueExpression(expression.Index, context, TypeSymbol.I32);
            if (loweredIndex == null)
                return null;
            var index = context.CreateTemporary(TypeSymbol.I32);
            context.EmitCopy(index, loweredIndex);

            if (context.CreateTemporary(expression.Type) is not IrAggregateStorage result)
                return null;
            for (var leafIndex = 0; leafIndex < result.Leaves.Count; leafIndex++)
            {
                context.Emit(new IrExternCallInstruction(
                    expression.AggregateLeafIntrinsics[leafIndex].GetterExternSignature,
                    new[] { arrayLeaves[leafIndex], (IrValue)index },
                    result.Leaves[leafIndex]));
            }
            return result;
        }

    }
}
