using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class AggregateLowerer : LoweringComponent
    {
        public AggregateLowerer(LoweringSession session) : base(session) { }
        internal IrValue LowerStructConstructionExpression(
            BoundStructConstructionExpression expression,
            LoweringContext context)
        {
            var values = new IrValue[AggregateLayout.GetLeaves(expression.Type).Count];
            foreach (var initializer in expression.Initializers)
            {
                var value = LowerValueExpression(
                    initializer.Expression,
                    context,
                    initializer.Field.Type);
                if (value == null)
                    return null;

                var fieldLeaves = Session.AggregateStorageLowerer.ExpandValueLeaves(
                    initializer.Field.Type,
                    value);
                var indices = AggregateLayout.GetFieldLeafIndices(
                    expression.Type,
                    initializer.Field);
                for (var index = 0; index < indices.Count && index < fieldLeaves.Count; index++)
                    values[indices[index]] = fieldLeaves[index];
            }

            return new IrAggregateValue(expression.Type, values);
        }

        internal IrValue LowerEnumConstructionExpression(
            BoundEnumConstructionExpression expression,
            LoweringContext context)
        {
            var descriptors = AggregateLayout.GetLeaves(expression.Type);
            var values = new IrValue[descriptors.Count];
            foreach (var initializer in expression.Initializers)
            {
                var value = LowerValueExpression(
                    initializer.Expression,
                    context,
                    initializer.Field.Type);
                if (value == null)
                    return null;

                var fieldLeaves = Session.AggregateStorageLowerer.ExpandValueLeaves(
                    initializer.Field.Type,
                    value);
                var leafIndex = 0;
                for (var index = 0; index < descriptors.Count; index++)
                {
                    var path = descriptors[index].Path;
                    if (path.Count < 2 ||
                        !string.Equals(path[0], expression.Variant.Name, StringComparison.Ordinal) ||
                        !string.Equals(path[1], initializer.Field.Name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (leafIndex < fieldLeaves.Count)
                        values[index] = fieldLeaves[leafIndex++];
                }
            }

            for (var index = 0; index < descriptors.Count; index++)
            {
                if (descriptors[index].IsEnumTag)
                {
                    values[index] = new IrConstantValue(
                        expression.Variant.Tag,
                        TypeSymbol.I32);
                }
            }

            return new IrAggregateValue(expression.Type, values);
        }

        internal IrValue LowerTupleExpression(
            BoundTupleExpression expression,
            LoweringContext context)
        {
            var values = new IrValue[AggregateLayout.GetLeaves(expression.Type).Count];
            for (var index = 0; index < expression.Elements.Count; index++)
            {
                var element = LowerValueExpression(
                    expression.Elements[index],
                    context,
                    expression.Type.TupleElementTypes[index]);
                if (element == null)
                    return null;

                var indices = AggregateLayout.GetFieldLeafIndices(
                    expression.Type,
                    expression.Type.AggregateFields[index]);
                var elementLeaves = IsAggregateStorageType(element.Type)
                    ? GetAggregateLeaves(element)
                    : new[] { element };
                for (var leafIndex = 0;
                     leafIndex < indices.Count && leafIndex < elementLeaves.Count;
                     leafIndex++)
                {
                    values[indices[leafIndex]] = elementLeaves[leafIndex];
                }
            }
            return new IrAggregateValue(expression.Type, values);
        }

        internal IrValue LowerMaybeExternBindingExpression(
            BoundMaybeExternBindingExpression expression,
            LoweringContext context)
        {
            var rawValue = Session.CallLowerer.LowerCallExpression(
                expression.RawExpression,
                context,
                preserveResult: true);
            if (rawValue == null)
                return null;

            return LowerMaybeOutputProjection(
                rawValue,
                expression.Projection,
                context,
                "maybe_extern");
        }

        internal IrValue LowerMaybeOutputProjection(
            IrValue rawValue,
            ExternMaybeOutputProjection projection,
            LoweringContext context,
            string labelPrefix)
        {
            if (projection.ValidityMethod.Parameters.Count != 1 ||
                projection.ValidityMethod.ReturnType != TypeSymbol.Bool)
            {
                Diagnostics.ReportLoweringError(
                    "The resolved Maybe validity method must accept one value and return bool.");
                return null;
            }

            var isValid = context.CreateTemporary(TypeSymbol.Bool);
            context.Emit(new IrExternCallInstruction(
                projection.ValidityMethod.ExternSignature,
                new[] { rawValue },
                isValid));

            var justBlock = context.CreateBlock($"{labelPrefix}_just");
            var nothingBlock = context.CreateBlock($"{labelPrefix}_nothing");
            var mergeBlock = context.CreateBlock($"{labelPrefix}_merge");
            var result = context.CreateTemporary(projection.Type);
            context.TerminateWithCondition(
                isValid,
                justBlock.Label,
                nothingBlock.Label);

            context.SwitchTo(justBlock);
            context.EmitCopy(
                result,
                CreateMaybeExternEnumValue(projection.JustVariant, rawValue));
            context.TerminateWithJump(mergeBlock.Label);

            context.SwitchTo(nothingBlock);
            context.EmitCopy(
                result,
                CreateMaybeExternEnumValue(projection.NothingVariant, null));
            context.TerminateWithJump(mergeBlock.Label);

            context.SwitchTo(mergeBlock);
            return result;
        }

        private IrAggregateValue CreateMaybeExternEnumValue(
            EnumVariantSymbol variant,
            IrValue payload)
        {
            var descriptors = AggregateLayout.GetLeaves(variant.ContainingType);
            var values = new IrValue[descriptors.Count];
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index];
                if (descriptor.IsEnumTag)
                {
                    values[index] = new IrConstantValue(variant.Tag, TypeSymbol.I32);
                    continue;
                }

                if (payload != null &&
                    descriptor.Path.Count >= 2 &&
                    string.Equals(
                        descriptor.Path[0],
                        variant.Name,
                        StringComparison.Ordinal))
                {
                    values[index] = payload;
                }
            }

            return new IrAggregateValue(variant.ContainingType, values);
        }

        internal IrValue LowerAggregateFieldAccessExpression(
            BoundAggregateFieldAccessExpression expression,
            LoweringContext context)
        {
            var receiver = LowerValueExpression(
                expression.Receiver,
                context,
                expression.Receiver.Type);
            if (receiver == null)
                return null;
            return ProjectAggregateField(receiver, expression.Receiver.Type, expression.Field);
        }

        internal IrValue ProjectAggregateField(
            IrValue receiver,
            TypeSymbol receiverType,
            AggregateFieldSymbol field)
        {
            var receiverLeaves = GetAggregateLeaves(receiver);
            var indices = AggregateLayout.GetFieldLeafIndices(receiverType, field);
            if (!IsAggregateStorageType(field.Type))
                return indices.Count > 0 ? receiverLeaves[indices[0]] : null;

            var storageLeaves = new List<IrStorage>();
            var valueLeaves = new List<IrValue>();
            var allStorage = true;
            foreach (var index in indices)
            {
                var leaf = receiverLeaves[index];
                valueLeaves.Add(leaf);
                if (leaf is IrStorage leafStorage)
                    storageLeaves.Add(leafStorage);
                else
                    allStorage = false;
            }

            return allStorage
                ? Session.AggregateStorageLowerer.CreateStorage(field.Type, storageLeaves)
                : new IrAggregateValue(field.Type, valueLeaves);
        }

        internal IrValue ProjectEnumVariantField(
            IrValue receiver,
            TypeSymbol enumType,
            EnumVariantSymbol variant,
            AggregateFieldSymbol field)
        {
            var receiverLeaves = GetAggregateLeaves(receiver);
            var descriptors = AggregateLayout.GetLeaves(enumType);
            var valueLeaves = new List<IrValue>();
            var storageLeaves = new List<IrStorage>();
            var allStorage = true;
            for (var index = 0; index < descriptors.Count && index < receiverLeaves.Count; index++)
            {
                var path = descriptors[index].Path;
                if (path.Count < 2 ||
                    !string.Equals(path[0], variant.Name, StringComparison.Ordinal) ||
                    !string.Equals(path[1], field.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                var leaf = receiverLeaves[index];
                valueLeaves.Add(leaf);
                if (leaf is IrStorage storage)
                    storageLeaves.Add(storage);
                else
                    allStorage = false;
            }

            if (valueLeaves.Count != AggregateLayout.GetLeaves(field.Type).Count)
                return null;

            if (!IsAggregateStorageType(field.Type))
                return valueLeaves.Count > 0 ? valueLeaves[0] : null;

            return allStorage
                ? Session.AggregateStorageLowerer.CreateStorage(field.Type, storageLeaves)
                : new IrAggregateValue(field.Type, valueLeaves);
        }

    }
}
