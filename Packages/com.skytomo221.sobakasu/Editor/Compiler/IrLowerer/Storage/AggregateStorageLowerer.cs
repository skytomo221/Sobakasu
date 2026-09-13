using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class AggregateStorageLowerer
    {
        internal bool IsAggregateStorageType(TypeSymbol type)
        {
            return type?.UsesFlattenedAggregateStorage == true ||
                type?.TypeKind == TypeKind.Array &&
                type.ElementType?.UsesFlattenedAggregateStorage == true;
        }
        internal IReadOnlyList<IrValue> ExpandValueLeaves(
            TypeSymbol type,
            IrValue value)
        {
            return IsAggregateStorageType(type)
                ? GetAggregateLeaves(value)
                : new[] { value };
        }

        internal IReadOnlyList<IrValue> GetAggregateLeaves(IrValue value)
        {
            if (value is IrAggregateValue aggregateValue)
                return aggregateValue.Leaves;
            if (value is IrAggregateStorage aggregateStorage)
            {
                var result = new IrValue[aggregateStorage.Leaves.Count];
                for (var index = 0; index < result.Length; index++)
                    result[index] = aggregateStorage.Leaves[index];
                return result;
            }
            throw new InvalidOperationException(
                $"IR value '{value?.GetType().Name ?? "<null>"}' is not aggregate storage.");
        }

        internal IrStorage CreateTemporary(TypeSymbol type, IrModuleBuilder builder)
        {
            if (!IsAggregateStorageType(type))
                return builder.CreateTemporary(type);

            var leaves = new List<IrStorage>();
            foreach (var descriptor in AggregateLayout.GetLeaves(type))
                leaves.Add(builder.CreateTemporary(descriptor.Type));
            return new IrAggregateStorage(type, leaves);
        }

        internal IrAggregateStorage CreateStorage(
            TypeSymbol type,
            IReadOnlyList<IrStorage> leaves)
        {
            return new IrAggregateStorage(type, leaves);
        }

        internal void EmitCopy(
            IrStorage target,
            IrValue source,
            IrModuleBuilder builder)
        {
            if (target is not IrAggregateStorage aggregateTarget)
            {
                builder.Emit(new IrCopyInstruction(target, source));
                return;
            }

            var sourceLeaves = GetAggregateLeaves(source);
            var descriptors = AggregateLayout.GetLeaves(target.Type);
            for (var pass = 0; pass < 2; pass++)
            {
                for (var index = 0; index < aggregateTarget.Leaves.Count; index++)
                {
                    var isTag = index < descriptors.Count && descriptors[index].IsEnumTag;
                    if ((pass == 0 && isTag) || (pass == 1 && !isTag) ||
                        index >= sourceLeaves.Count || sourceLeaves[index] == null)
                    {
                        continue;
                    }

                    builder.Emit(new IrCopyInstruction(
                        aggregateTarget.Leaves[index],
                        sourceLeaves[index]));
                }
            }
        }

        internal void EmitArrayElementSet(
            IReadOnlyList<IrValue> arrays,
            IReadOnlyList<ArrayIntrinsicSymbols> intrinsics,
            IrValue index,
            TypeSymbol elementType,
            IrValue value,
            LoweringContext context)
        {
            var valueLeaves = IsAggregateStorageType(elementType)
                ? GetAggregateLeaves(value)
                : new[] { value };
            var descriptors = IsAggregateStorageType(elementType)
                ? AggregateLayout.GetLeaves(elementType)
                : new[] { new AggregateLeafDescriptor(elementType, Array.Empty<string>()) };
            for (var pass = 0; pass < 2; pass++)
            {
                for (var leafIndex = 0; leafIndex < arrays.Count; leafIndex++)
                {
                    var isTag = leafIndex < descriptors.Count && descriptors[leafIndex].IsEnumTag;
                    if ((pass == 0 && isTag) || (pass == 1 && !isTag) ||
                        leafIndex >= valueLeaves.Count || valueLeaves[leafIndex] == null)
                    {
                        continue;
                    }

                    context.Emit(new IrExternCallInstruction(
                        intrinsics[leafIndex].SetterExternSignature,
                        new[] { arrays[leafIndex], index, valueLeaves[leafIndex] },
                        null));
                }
            }
        }

    }
}
