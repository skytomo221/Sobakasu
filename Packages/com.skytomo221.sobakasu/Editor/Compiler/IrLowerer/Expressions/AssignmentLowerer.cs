using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class AssignmentLowerer : LoweringComponent
    {
        public AssignmentLowerer(LoweringSession session) : base(session) { }

        internal IrValue Lower(BoundAssignmentExpression expression, LoweringContext context)
        {
            var source = LowerValueExpression(expression.Expression, context, expression.Variable.Type);
            if (source == null)
                return null;

            var target = context.GetVariableStorage(expression.Variable);
            context.EmitCopy(target, source);
            return target;
        }

        internal IrValue LowerAggregateFieldAssignmentExpression(
            BoundAggregateFieldAssignmentExpression expression,
            LoweringContext context)
        {
            if (TryGetAggregateArrayElementRoot(
                    expression.Target,
                    out var element,
                    out var fields))
            {
                return LowerAggregateArrayFieldAssignment(
                    expression,
                    element,
                    fields,
                    context);
            }

            if (!TryLowerDirectStorage(expression.Target, context, out var target))
            {
                Diagnostics.ReportLoweringError("Aggregate field assignment has no writable storage.");
                return null;
            }

            IrValue value;
            if (expression.CompoundOperator == null)
            {
                value = LowerValueExpression(expression.Value, context, expression.Target.Type);
            }
            else
            {
                var oldValue = context.CreateTemporary(expression.Target.Type);
                context.EmitCopy(oldValue, target);
                var right = LowerValueExpression(
                    expression.Value,
                    context,
                    expression.CompoundOperator.Parameters[0].Type);
                if (right == null)
                    return null;
                value = Session.CallLowerer.LowerUserFunctionInvocation(
                    expression.CompoundOperator,
                    oldValue,
                    new[] { right },
                    context,
                    preserveResult: true);
            }

            if (value == null)
                return null;
            context.EmitCopy(target, value);
            return target;
        }

        internal IrValue LowerElementAssignmentExpression(
            BoundElementAssignmentExpression expression,
            LoweringContext context)
        {
            if (IsAggregateStorageType(expression.Target.Array.Type))
            {
                return LowerAggregateArrayElementAssignment(
                    expression,
                    expression.Target,
                    Array.Empty<AggregateFieldSymbol>(),
                    context);
            }

            var loweredArray = LowerValueExpression(
                expression.Target.Array,
                context,
                expression.Target.Array.Type);
            if (loweredArray == null)
                return null;

            var array = context.CreateTemporary(expression.Target.Array.Type);
            context.EmitCopy(array, loweredArray);

            var loweredIndex = LowerValueExpression(
                expression.Target.Index,
                context,
                expression.Target.Intrinsics.IndexType);
            if (loweredIndex == null)
                return null;

            var index = context.CreateTemporary(expression.Target.Intrinsics.IndexType);
            context.EmitCopy(index, loweredIndex);

            IrValue value;
            if (expression.CompoundOperator == null)
            {
                value = LowerValueExpression(
                    expression.Value,
                    context,
                    expression.Target.Type);
            }
            else
            {
                var oldValue = context.CreateTemporary(expression.Target.Type);
                context.Emit(new IrExternCallInstruction(
                    expression.Target.Intrinsics.GetterExternSignature,
                    new IrValue[] { array, index },
                    oldValue));

                var right = LowerValueExpression(
                    expression.Value,
                    context,
                    expression.CompoundOperator.Parameters[0].Type);
                if (right == null)
                    return null;
                value = Session.CallLowerer.LowerUserFunctionInvocation(
                    expression.CompoundOperator,
                    oldValue,
                    new[] { right },
                    context,
                    preserveResult: true);
            }

            if (value == null)
                return null;

            var result = context.CreateTemporary(expression.Target.Type);
            context.EmitCopy(result, value);
            context.Emit(new IrExternCallInstruction(
                expression.Target.Intrinsics.SetterExternSignature,
                new IrValue[] { array, index, result },
                null));
            return result;
        }

        internal IrValue LowerAggregateArrayFieldAssignment(
            BoundAggregateFieldAssignmentExpression expression,
            BoundElementAccessExpression element,
            IReadOnlyList<AggregateFieldSymbol> fields,
            LoweringContext context)
        {
            return LowerAggregateArrayAssignmentCore(
                element,
                fields,
                expression.Target.Type,
                expression.Value,
                expression.CompoundOperator,
                context);
        }

        private IrValue LowerAggregateArrayElementAssignment(
            BoundElementAssignmentExpression expression,
            BoundElementAccessExpression element,
            IReadOnlyList<AggregateFieldSymbol> fields,
            LoweringContext context)
        {
            return LowerAggregateArrayAssignmentCore(
                element,
                fields,
                expression.Target.Type,
                expression.Value,
                expression.CompoundOperator,
                context);
        }

        private IrValue LowerAggregateArrayAssignmentCore(
            BoundElementAccessExpression element,
            IReadOnlyList<AggregateFieldSymbol> fields,
            TypeSymbol targetType,
            BoundExpression valueExpression,
            FunctionSymbol compoundOperator,
            LoweringContext context)
        {
            if (!TryLowerAggregateArrayLocation(
                    element,
                    fields,
                    context,
                    out var arrays,
                    out var intrinsics,
                    out var index))
            {
                return null;
            }

            IrValue value;
            if (compoundOperator == null)
            {
                value = LowerValueExpression(valueExpression, context, targetType);
            }
            else
            {
                if (arrays.Count != 1)
                    return null;
                var oldValue = context.CreateTemporary(targetType);
                context.Emit(new IrExternCallInstruction(
                    intrinsics[0].GetterExternSignature,
                    new IrValue[] { arrays[0], index },
                    oldValue));
                var right = LowerValueExpression(
                    valueExpression,
                    context,
                    compoundOperator.Parameters[0].Type);
                if (right == null)
                    return null;
                value = Session.CallLowerer.LowerUserFunctionInvocation(
                    compoundOperator,
                    oldValue,
                    new[] { right },
                    context,
                    preserveResult: true);
            }

            if (value == null)
                return null;
            Session.AggregateStorageLowerer.EmitArrayElementSet(
                arrays,
                intrinsics,
                index,
                targetType,
                value,
                context);
            return value;
        }

        private bool TryLowerDirectStorage(
            BoundExpression expression,
            LoweringContext context,
            out IrStorage storage)
        {
            if (expression is BoundNameExpression name)
            {
                storage = name.Symbol switch
                {
                    VariableSymbol variable => context.GetVariableStorage(variable),
                    ParameterSymbol parameter => context.GetParameterStorage(parameter),
                    _ => null
                };
                return storage != null;
            }

            if (expression is BoundAggregateFieldAccessExpression field &&
                TryLowerDirectStorage(field.Receiver, context, out var receiverStorage))
            {
                storage = Session.AggregateLowerer.ProjectAggregateField(
                    receiverStorage,
                    field.Receiver.Type,
                    field.Field) as IrStorage;
                return storage != null;
            }

            storage = null;
            return false;
        }

        private bool TryLowerAggregateArrayLocation(
            BoundElementAccessExpression element,
            IReadOnlyList<AggregateFieldSymbol> fields,
            LoweringContext context,
            out IReadOnlyList<IrValue> arrays,
            out IReadOnlyList<ArrayIntrinsicSymbols> intrinsics,
            out IrStorage index)
        {
            arrays = null;
            intrinsics = null;
            index = null;
            var array = LowerValueExpression(element.Array, context, element.Array.Type);
            if (array == null || element.AggregateLeafIntrinsics == null)
                return false;

            var currentArrays = new List<IrValue>(GetAggregateLeaves(array));
            var currentIntrinsics = new List<ArrayIntrinsicSymbols>(element.AggregateLeafIntrinsics);
            var currentType = element.Type;
            foreach (var field in fields)
            {
                var indices = AggregateLayout.GetFieldLeafIndices(currentType, field);
                var selectedArrays = new List<IrValue>(indices.Count);
                var selectedIntrinsics = new List<ArrayIntrinsicSymbols>(indices.Count);
                foreach (var fieldIndex in indices)
                {
                    selectedArrays.Add(currentArrays[fieldIndex]);
                    selectedIntrinsics.Add(currentIntrinsics[fieldIndex]);
                }
                currentArrays = selectedArrays;
                currentIntrinsics = selectedIntrinsics;
                currentType = field.Type;
            }

            var loweredIndex = LowerValueExpression(element.Index, context, TypeSymbol.I32);
            if (loweredIndex == null)
                return false;
            index = context.CreateTemporary(TypeSymbol.I32);
            context.EmitCopy(index, loweredIndex);
            arrays = currentArrays;
            intrinsics = currentIntrinsics;
            return true;
        }

        internal bool TryGetAggregateArrayElementRoot(
            BoundAggregateFieldAccessExpression target,
            out BoundElementAccessExpression element,
            out IReadOnlyList<AggregateFieldSymbol> fields)
        {
            var reversed = new List<AggregateFieldSymbol>();
            BoundExpression current = target;
            while (current is BoundAggregateFieldAccessExpression field)
            {
                reversed.Add(field.Field);
                current = field.Receiver;
            }

            if (current is not BoundElementAccessExpression arrayElement ||
                !IsAggregateStorageType(arrayElement.Array.Type))
            {
                element = null;
                fields = null;
                return false;
            }

            reversed.Reverse();
            element = arrayElement;
            fields = reversed;
            return true;
        }

    }
}
