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
            var place = CapturePlace(expression.Target, context);
            if (place == null)
                return null;
            var source = LowerValueExpression(expression.Expression, context, expression.Type);
            if (source == null)
                return null;

            place.Write(source);
            return source;
        }

        internal IrValue LowerAggregateFieldAssignmentExpression(
            BoundAggregateFieldAssignmentExpression expression,
            LoweringContext context)
        {
            return LowerPlaceAssignment(expression.Target, expression.Value, expression.CompoundOperator, context);
        }

        internal IrValue LowerElementAssignmentExpression(
            BoundElementAssignmentExpression expression,
            LoweringContext context)
        {
            return LowerPlaceAssignment(expression.Target, expression.Value, expression.CompoundOperator, context);
        }

        private IrValue LowerPlaceAssignment(
            BoundExpression target,
            BoundExpression valueExpression,
            FunctionSymbol compoundOperator,
            LoweringContext context)
        {
            var place = CapturePlace(target, context);
            if (place == null)
                return null;
            IrValue value;
            if (compoundOperator == null)
            {
                value = LowerValueExpression(valueExpression, context, target.Type);
            }
            else
            {
                var oldValue = place.Read();
                var right = LowerValueExpression(valueExpression, context, compoundOperator.Parameters[0].Type);
                if (right == null)
                    return null;
                value = Session.CallLowerer.LowerUserFunctionInvocation(
                    compoundOperator, oldValue, new[] { right }, context, preserveResult: true,
                    receiverPlace: place);
            }
            if (value == null)
                return null;
            var result = context.CreateTemporary(target.Type);
            context.EmitCopy(result, value);
            place.Write(result);
            return result;
        }

        // A captured place owns evaluated location operands, never source expressions.
        internal sealed class CapturedPlace
        {
            private readonly Func<IrValue> read;
            private readonly Action<IrValue> write;

            internal CapturedPlace(Func<IrValue> read, Action<IrValue> write)
            {
                this.read = read;
                this.write = write;
            }

            internal IrValue Read() => read();
            internal void Write(IrValue value) => write(value);
        }

        internal CapturedPlace CapturePlace(BoundExpression expression, LoweringContext context)
        {
            if (TryLowerDirectStorage(expression, context, out var storage))
            {
                return new CapturedPlace(
                    () => {
                        var value = context.CreateTemporary(expression.Type);
                        context.EmitCopy(value, storage);
                        return value;
                    },
                    value => context.EmitCopy(storage, value));
            }

            BoundElementAccessExpression element;
            IReadOnlyList<AggregateFieldSymbol> fields;
            if (expression is BoundAggregateFieldAccessExpression field &&
                TryGetAggregateArrayElementRoot(field, out element, out fields))
            {
                return CaptureAggregateArrayPlace(element, fields, expression.Type, context);
            }
            if (expression is BoundElementAccessExpression arrayElement)
            {
                if (IsAggregateStorageType(arrayElement.Array.Type))
                    return CaptureAggregateArrayPlace(arrayElement, Array.Empty<AggregateFieldSymbol>(), expression.Type, context);
                var arrayValue = LowerValueExpression(arrayElement.Array, context, arrayElement.Array.Type);
                if (arrayValue == null)
                    return null;
                var array = context.CreateTemporary(arrayElement.Array.Type);
                context.EmitCopy(array, arrayValue);
                var indexValue = LowerValueExpression(arrayElement.Index, context, arrayElement.Intrinsics.IndexType);
                if (indexValue == null)
                    return null;
                var index = context.CreateTemporary(arrayElement.Intrinsics.IndexType);
                context.EmitCopy(index, indexValue);
                return CaptureArrayPlace(new IrValue[] { array },
                    new[] { arrayElement.Intrinsics }, index, expression.Type, context);
            }
            Diagnostics.ReportLoweringError("Expression has no writable storage location.");
            return null;
        }

        private CapturedPlace CaptureAggregateArrayPlace(
            BoundElementAccessExpression element,
            IReadOnlyList<AggregateFieldSymbol> fields,
            TypeSymbol type,
            LoweringContext context)
        {
            if (!TryLowerAggregateArrayLocation(element, fields, context,
                    out var arrays, out var intrinsics, out var index))
                return null;
            return CaptureArrayPlace(arrays, intrinsics, index, type, context);
        }

        private CapturedPlace CaptureArrayPlace(
            IReadOnlyList<IrValue> arrays,
            IReadOnlyList<ArrayIntrinsicSymbols> intrinsics,
            IrValue index,
            TypeSymbol type,
            LoweringContext context)
        {
            return new CapturedPlace(
                () => {
                    var value = context.CreateTemporary(type);
                    var leaves = IsAggregateStorageType(type) ? GetAggregateLeaves(value) : new IrValue[] { value };
                    for (var i = 0; i < arrays.Count; i++)
                        context.Emit(new IrExternCallInstruction(intrinsics[i].GetterExternSignature,
                            new IrValue[] { arrays[i], index }, (IrStorage)leaves[i]));
                    return value;
                },
                value => Session.AggregateStorageLowerer.EmitArrayElementSet(
                    arrays, intrinsics, index, type, value, context));
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

            var currentArrays = new List<IrValue>();
            foreach (var leaf in GetAggregateLeaves(array))
            {
                var capturedArray = context.CreateTemporary(leaf.Type);
                context.EmitCopy(capturedArray, leaf);
                currentArrays.Add(capturedArray);
            }
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
