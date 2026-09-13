using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class CallLowerer : LoweringComponent
    {
        public CallLowerer(LoweringSession session) : base(session) { }
        internal IrValue LowerCallExpression(
            BoundCallExpression callExpression,
            LoweringContext context,
            bool preserveResult)
        {
            if (callExpression.Method == null)
            {
                Diagnostics.ReportLoweringError("Cannot lower unresolved method call.");
                return null;
            }

            if (string.IsNullOrEmpty(callExpression.Method.ExternSignature))
            {
                Diagnostics.ReportLoweringError(
                    $"No extern signature was selected for '{callExpression.Method.DisplayName}'.");
                return null;
            }

            if (callExpression.Arguments.Count != callExpression.Method.Parameters.Count)
            {
                Diagnostics.ReportLoweringError(
                    $"Argument count mismatch for '{callExpression.Method.DisplayName}'.");
                return null;
            }

            if (callExpression.Method is ExternMethodSymbol externMethod &&
                externMethod.UsesAbiAdapter)
            {
                return LowerExternAbiCallExpression(
                    callExpression,
                    externMethod,
                    context,
                    preserveResult);
            }

            var arguments = new IrValue[callExpression.Arguments.Count];
            for (var index = 0; index < callExpression.Arguments.Count; index++)
            {
                arguments[index] = LowerValueExpression(
                    callExpression.Arguments[index],
                    context,
                    callExpression.Method.Parameters[index].Type);
                if (arguments[index] == null)
                    return null;
            }

            if (callExpression.Method.ReturnType == TypeSymbol.Unit)
            {
                context.Emit(new IrExternCallInstruction(
                    callExpression.Method.ExternSignature,
                    arguments,
                    null));
                return preserveResult
                    ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
                    : null;
            }

            var result = context.CreateTemporary(callExpression.Type);
            context.Emit(new IrExternCallInstruction(
                callExpression.Method.ExternSignature,
                arguments,
                result));
            return result;
        }

        internal IrValue LowerExternAbiCallExpression(
            BoundCallExpression callExpression,
            ExternMethodSymbol method,
            LoweringContext context,
            bool preserveResult)
        {
            var logicalArguments = new IrValue[callExpression.Arguments.Count];
            for (var index = 0; index < logicalArguments.Length; index++)
            {
                logicalArguments[index] = LowerValueExpression(
                    callExpression.Arguments[index],
                    context,
                    method.Parameters[index].Type);
                if (logicalArguments[index] == null)
                    return null;
            }

            var logicalOutputTypes = GetExternLogicalOutputTypes(callExpression.Type);
            var physicalArguments = new List<IrValue>();
            if (!method.IsStatic)
                physicalArguments.Add(logicalArguments[0]);

            var outputs = new List<IrValue>();
            var maybeOutputProjections =
                new List<KeyValuePair<int, ExternMaybeOutputProjection>>();
            IrStorage abiReturnStorage = null;
            var outputIndex = 0;
            if (method.AbiReturnType != TypeSymbol.Unit)
            {
                var outputType = outputIndex < logicalOutputTypes.Count
                    ? logicalOutputTypes[outputIndex]
                    : method.AbiReturnType;
                abiReturnStorage = context.CreateTemporary(outputType);
                outputs.Add(abiReturnStorage);
                outputIndex++;
            }

            foreach (var parameter in method.AbiParameters)
            {
                switch (parameter.PassingMode)
                {
                    case ExternParameterPassingMode.Normal:
                        physicalArguments.Add(logicalArguments[parameter.LogicalInputOrdinal]);
                        break;

                    case ExternParameterPassingMode.In:
                        {
                            var input = context.CreateTemporary(parameter.Type);
                            context.EmitCopy(input, logicalArguments[parameter.LogicalInputOrdinal]);
                            physicalArguments.Add(input);
                            break;
                        }

                    case ExternParameterPassingMode.Ref:
                        {
                            var outputType = outputIndex < logicalOutputTypes.Count
                                ? logicalOutputTypes[outputIndex]
                                : parameter.Type;
                            var reference = context.CreateTemporary(outputType);
                            context.EmitCopy(
                                reference,
                                logicalArguments[parameter.LogicalInputOrdinal]);
                            physicalArguments.Add(reference);
                            outputs.Add(reference);
                            outputIndex++;
                            break;
                        }

                    case ExternParameterPassingMode.Out:
                        {
                            var outputType = outputIndex < logicalOutputTypes.Count
                                ? logicalOutputTypes[outputIndex]
                                : parameter.Type;
                            var output = context.CreateTemporary(
                                parameter.MaybeProjection == null
                                    ? outputType
                                    : parameter.Type);
                            physicalArguments.Add(output);
                            outputs.Add(output);
                            if (parameter.MaybeProjection != null)
                            {
                                maybeOutputProjections.Add(
                                    new KeyValuePair<int, ExternMaybeOutputProjection>(
                                        outputs.Count - 1,
                                        parameter.MaybeProjection));
                            }
                            outputIndex++;
                            break;
                        }

                    case ExternParameterPassingMode.GenericTypeArgument:
                        {
                            if (method.TypeArguments.Count <= physicalArguments.Count -
                                (method.IsStatic ? 0 : 1))
                            {
                                Diagnostics.ReportLoweringError(
                                    $"Generic extern '{method.DisplayName}' has incomplete type operand metadata.");
                                return null;
                            }
                            var genericOperandIndex = 0;
                            foreach (var previous in method.AbiParameters)
                            {
                                if (ReferenceEquals(previous, parameter))
                                    break;
                                if (previous.PassingMode == ExternParameterPassingMode.GenericTypeArgument)
                                    genericOperandIndex++;
                            }
                            var argumentType = method.TypeArguments[genericOperandIndex];
                            var runtimeType = argumentType.RuntimeClrType ??
                                SobakasuTypeMapper.ResolveRuntimeType(argumentType.RuntimeQualifiedName);
                            physicalArguments.Add(new IrConstantValue(runtimeType, parameter.Type));
                            break;
                        }
                }
            }

            context.Emit(new IrExternCallInstruction(
                method.ExternSignature,
                physicalArguments,
                abiReturnStorage));

            if (!preserveResult)
                return null;

            foreach (var pair in maybeOutputProjections)
            {
                var projected = Session.AggregateLowerer.LowerMaybeOutputProjection(
                    outputs[pair.Key],
                    pair.Value,
                    context,
                    "maybe_out");
                if (projected == null)
                    return null;
                outputs[pair.Key] = projected;
            }

            if (outputs.Count == 0)
                return new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>());
            if (outputs.Count == 1)
                return outputs[0];
            return CreateExternLogicalAggregateResult(callExpression.Type, outputs);
        }

        private IrAggregateValue CreateExternLogicalAggregateResult(
            TypeSymbol resultType,
            IReadOnlyList<IrValue> outputs)
        {
            var values = new IrValue[AggregateLayout.GetLeaves(resultType).Count];
            for (var outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
            {
                var indices = AggregateLayout.GetFieldLeafIndices(
                    resultType,
                    resultType.AggregateFields[outputIndex]);
                var output = outputs[outputIndex];
                var leaves = IsAggregateStorageType(output.Type)
                    ? GetAggregateLeaves(output)
                    : new[] { output };
                if (indices.Count != leaves.Count)
                {
                    Diagnostics.ReportLoweringError(
                        $"Extern logical output {outputIndex} does not match its aggregate layout.");
                    return null;
                }

                for (var leafIndex = 0; leafIndex < indices.Count; leafIndex++)
                    values[indices[leafIndex]] = leaves[leafIndex];
            }

            return new IrAggregateValue(resultType, values);
        }

        private IReadOnlyList<TypeSymbol> GetExternLogicalOutputTypes(TypeSymbol type)
        {
            if (type == TypeSymbol.Unit)
                return Array.Empty<TypeSymbol>();
            if (type.TypeKind == TypeKind.Tuple)
                return type.TupleElementTypes;
            return new[] { type };
        }

        internal IrValue LowerUserFunctionCallExpression(
            BoundUserFunctionCallExpression callExpression,
            LoweringContext context,
            bool preserveResult)
        {
            if (callExpression.Arguments.Count != callExpression.Function.Parameters.Count)
            {
                Diagnostics.ReportLoweringError(
                    $"Argument count mismatch for function '{callExpression.Function.Name}'.");
                return null;
            }

            IrValue receiverValue = null;
            if (callExpression.Function.SelfParameter != null)
            {
                if (callExpression.Receiver == null)
                {
                    Diagnostics.ReportLoweringError(
                        $"Instance method '{callExpression.Function.DisplayName}' has no receiver.");
                    return null;
                }

                receiverValue = LowerValueExpression(
                    callExpression.Receiver,
                    context,
                    callExpression.Function.ContainingType);
                if (receiverValue == null)
                    return null;
                if (callExpression.Arguments.Count > 0)
                {
                    var capturedReceiver = context.CreateTemporary(callExpression.Function.ContainingType);
                    context.EmitCopy(capturedReceiver, receiverValue);
                    receiverValue = capturedReceiver;
                }
            }

            var argumentValues = new IrValue[callExpression.Arguments.Count];
            for (var index = 0; index < callExpression.Arguments.Count; index++)
            {
                argumentValues[index] = LowerValueExpression(
                    callExpression.Arguments[index],
                    context,
                    callExpression.Function.Parameters[index].Type);
                if (argumentValues[index] == null)
                    return null;
                if (index + 1 < callExpression.Arguments.Count)
                {
                    var capturedArgument = context.CreateTemporary(callExpression.Function.Parameters[index].Type);
                    context.EmitCopy(capturedArgument, argumentValues[index]);
                    argumentValues[index] = capturedArgument;
                }
            }

            return LowerUserFunctionInvocation(
                callExpression.Function,
                receiverValue,
                argumentValues,
                context,
                preserveResult);
        }

        internal IrValue LowerUserFunctionInvocation(
            FunctionSymbol function,
            IrValue receiverValue,
            IReadOnlyList<IrValue> argumentValues,
            LoweringContext context,
            bool preserveResult)
        {
            if (!Session.Functions.TryGetValue(function, out var declaration))
            {
                Diagnostics.ReportLoweringError(
                    $"Cannot lower unresolved user-defined function '{function.Name}'.");
                return null;
            }

            if (argumentValues.Count != function.Parameters.Count)
            {
                Diagnostics.ReportLoweringError(
                    $"Argument count mismatch for function '{function.Name}'.");
                return null;
            }

            if (function.SelfParameter != null && receiverValue == null)
            {
                Diagnostics.ReportLoweringError(
                    $"Instance method '{function.DisplayName}' has no receiver.");
                return null;
            }

            IrStorage resultStorage = null;
            if (function.ReturnType != TypeSymbol.Unit)
                resultStorage = context.CreateTemporary(function.ReturnType);

            var endBlock = context.CreateBlock("fn_end");
            var inlineFrame = new InlineFunctionFrame(
                function,
                endBlock.Label,
                resultStorage);

            if (function.SelfParameter != null)
            {
                var selfStorage = context.CreateTemporary(
                    function.SelfParameter.Type);
                inlineFrame.SetParameterStorage(
                    function.SelfParameter,
                    selfStorage);
                context.EmitCopy(selfStorage, receiverValue);
            }

            for (var index = 0; index < function.Parameters.Count; index++)
            {
                var parameter = function.Parameters[index];
                var parameterStorage = context.CreateTemporary(parameter.Type);
                inlineFrame.SetParameterStorage(parameter, parameterStorage);
                context.EmitCopy(parameterStorage, argumentValues[index]);
            }

            context.PushInlineFrame(inlineFrame);
            try
            {
                LowerBlock(declaration.Body, context);
                if (context.CurrentBlock.Terminator == null)
                {
                    inlineFrame.HasEndIncoming = true;
                    context.TerminateWithJump(endBlock.Label);
                }
            }
            finally
            {
                context.PopInlineFrame();
            }

            if (!inlineFrame.HasEndIncoming)
            {
                context.RemoveBlock(endBlock);
                return null;
            }

            context.SwitchTo(endBlock);
            if (!preserveResult)
                return null;
            return function.ReturnType == TypeSymbol.Unit
                ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
                : resultStorage;
        }
    }
}
