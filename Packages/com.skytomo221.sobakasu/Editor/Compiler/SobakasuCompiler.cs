using System;
using System.Collections.Generic;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Desugar;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Optimizer;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler
{
    public static class SobakasuCompiler
    {
        public readonly struct CompileResult
        {
            public readonly bool Success;
            public readonly string Uasm;
            public readonly string ErrorText;
            public readonly IReadOnlyList<HeapPatchEntry> HeapPatches;
            public readonly IReadOnlyList<NetworkReceiveMetadata> NetworkReceivers;
            public readonly IReadOnlyList<ExternalBindingMetadata> ExternalBindings;
            public readonly IReadOnlyList<DiagnosticItem> Diagnostics;

            public CompileResult(
                bool success,
                string uasm,
                string errorText,
                IReadOnlyList<HeapPatchEntry> heapPatches,
                IReadOnlyList<NetworkReceiveMetadata> networkReceivers,
                IReadOnlyList<ExternalBindingMetadata> externalBindings,
                IReadOnlyList<DiagnosticItem> diagnostics)
            {
                Success = success;
                Uasm = uasm;
                ErrorText = errorText;
                HeapPatches = heapPatches ?? Array.Empty<HeapPatchEntry>();
                NetworkReceivers = networkReceivers ?? Array.Empty<NetworkReceiveMetadata>();
                ExternalBindings = externalBindings ?? Array.Empty<ExternalBindingMetadata>();
                Diagnostics = diagnostics ?? Array.Empty<DiagnosticItem>();
            }

            public CompileResult(
                bool success,
                string uasm,
                string errorText,
                IReadOnlyList<HeapPatchEntry> heapPatches,
                IReadOnlyList<NetworkReceiveMetadata> networkReceivers,
                IReadOnlyList<DiagnosticItem> diagnostics)
                : this(
                    success,
                    uasm,
                    errorText,
                    heapPatches,
                    networkReceivers,
                    Array.Empty<ExternalBindingMetadata>(),
                    diagnostics)
            {
            }

            public CompileResult(
                bool success,
                string uasm,
                string errorText,
                IReadOnlyList<HeapPatchEntry> heapPatches,
                IReadOnlyList<DiagnosticItem> diagnostics)
                : this(
                    success,
                    uasm,
                    errorText,
                    heapPatches,
                    Array.Empty<NetworkReceiveMetadata>(),
                    Array.Empty<ExternalBindingMetadata>(),
                    diagnostics)
            {
            }

            public static CompileResult Ok(
                string uasm,
                IReadOnlyList<HeapPatchEntry> heapPatches,
                IReadOnlyList<NetworkReceiveMetadata> networkReceivers,
                IReadOnlyList<ExternalBindingMetadata> externalBindings,
                IReadOnlyList<DiagnosticItem> diagnostics)
            {
                return new CompileResult(
                    true,
                    uasm,
                    "",
                    heapPatches ?? Array.Empty<HeapPatchEntry>(),
                    networkReceivers ?? Array.Empty<NetworkReceiveMetadata>(),
                    externalBindings ?? Array.Empty<ExternalBindingMetadata>(),
                    diagnostics ?? Array.Empty<DiagnosticItem>());
            }

            public static CompileResult Ok(
                string uasm,
                IReadOnlyList<HeapPatchEntry> heapPatches,
                IReadOnlyList<NetworkReceiveMetadata> networkReceivers,
                IReadOnlyList<DiagnosticItem> diagnostics)
            {
                return Ok(
                    uasm,
                    heapPatches,
                    networkReceivers,
                    Array.Empty<ExternalBindingMetadata>(),
                    diagnostics);
            }

            public static CompileResult Ok(
                string uasm,
                IReadOnlyList<HeapPatchEntry> heapPatches,
                IReadOnlyList<DiagnosticItem> diagnostics)
            {
                return Ok(
                    uasm,
                    heapPatches,
                    Array.Empty<NetworkReceiveMetadata>(),
                    diagnostics);
            }

            public static CompileResult Fail(
                string errorText,
                IReadOnlyList<DiagnosticItem> diagnostics)
            {
                return new CompileResult(
                    false,
                    "",
                    errorText,
                    Array.Empty<HeapPatchEntry>(),
                    Array.Empty<NetworkReceiveMetadata>(),
                    Array.Empty<ExternalBindingMetadata>(),
                    diagnostics ?? Array.Empty<DiagnosticItem>());
            }
        }

        public static CompileResult CompileToUasm(string sourceText)
        {
            return CompileToUasm(sourceText, null);
        }

        public static CompileResult CompileToUasm(
            string sourceText,
            string standardLibraryRoot)
        {
            var resolver = new StandardLibraryResolver();
            var resolution = resolver.Resolve(
                sourceText ?? string.Empty,
                standardLibraryRoot);
            var graph = resolution.Graph;
            var text = graph.EntryModule.SourceText;

            var diagnostics = new DiagnosticBag();
            diagnostics.AddRange(resolution.Diagnostics);

            var binder = new SobakasuBinder();
            var boundProgram = binder.BindProgram(graph);
            diagnostics.AddRange(binder.Diagnostics);

            if (diagnostics.HasErrors)
            {
                var errorText = FormatDiagnostics(text, graph, diagnostics);
                return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
            }

            var desugarer = new SobakasuDesugarer();
            var desugaredProgram = desugarer.Desugar(boundProgram);
            diagnostics.AddRange(desugarer.Diagnostics);

            if (diagnostics.HasErrors)
            {
                var errorText = FormatDiagnostics(text, graph, diagnostics);
                return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
            }

            var irLowerer = new SobakasuIrLowerer();
            var irProgram = irLowerer.Lower(desugaredProgram);
            diagnostics.AddRange(irLowerer.Diagnostics);

            if (diagnostics.HasErrors)
            {
                var errorText = FormatDiagnostics(text, graph, diagnostics);
                return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
            }

            var optimizer = new SobakasuOptimizer();
            var optimizedProgram = optimizer.Optimize(irProgram);

            var uasmAssembler = new SobakasuUasmAssembler();
            var uasm = uasmAssembler.Assemble(optimizedProgram);
            diagnostics.AddRange(uasmAssembler.Diagnostics);

            if (diagnostics.HasErrors)
            {
                var errorText = FormatDiagnostics(text, graph, diagnostics);
                return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
            }

            return CompileResult.Ok(
                uasm,
                CopyHeapPatches(uasmAssembler.HeapPatches),
                CopyNetworkReceivers(boundProgram.NetworkReceivers),
                CopyExternalBindings(boundProgram.Functions),
                CopyDiagnostics(diagnostics));
        }

        private static string FormatDiagnostics(
            SourceText entrySourceText,
            StandardLibraryModuleGraph graph,
            DiagnosticBag diagnostics)
        {
            var builder = new StringBuilder();

            foreach (var diagnostic in diagnostics.Diagnostics)
            {
                var sourceText = entrySourceText;
                var sourcePath = diagnostic.SourcePath;
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    foreach (var module in graph.Modules)
                    {
                        if (string.Equals(
                                module.SourcePath,
                                sourcePath,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            sourceText = module.SourceText;
                            break;
                        }
                    }
                }

                var line = sourceText.GetLineFromPosition(diagnostic.Span.Start);
                var lineIndex = GetLineIndex(sourceText, line);
                var column = diagnostic.Span.Start - line.Start + 1;

                builder.AppendFormat(
                    "{0}{1} {2} (line {3}, col {4}): {5}\n",
                    string.IsNullOrEmpty(sourcePath) ? string.Empty : sourcePath + ": ",
                    diagnostic.Severity,
                    diagnostic.Code,
                    lineIndex + 1,
                    column,
                    diagnostic.Message);

                if (!string.IsNullOrWhiteSpace(diagnostic.Hint))
                    builder.AppendFormat("  hint: {0}\n", diagnostic.Hint);
            }

            return TrimTrailingLineBreaks(builder.ToString());
        }

        private static int GetLineIndex(SourceText sourceText, TextLine targetLine)
        {
            for (var index = 0; index < sourceText.Lines.Count; index++)
            {
                if (ReferenceEquals(sourceText.Lines[index], targetLine))
                    return index;
            }

            return 0;
        }

        private static string TrimTrailingLineBreaks(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var end = text.Length;
            while (end > 0 &&
                   (text[end - 1] == '\r' || text[end - 1] == '\n'))
            {
                end--;
            }

            if (end == text.Length)
                return text;

            return text[..end];
        }

        private static IReadOnlyList<DiagnosticItem> CopyDiagnostics(DiagnosticBag diagnostics)
        {
            if (diagnostics.Diagnostics.Count == 0)
                return Array.Empty<DiagnosticItem>();

            return new List<DiagnosticItem>(diagnostics.Diagnostics).ToArray();
        }

        private static IReadOnlyList<HeapPatchEntry> CopyHeapPatches(
            IReadOnlyList<HeapPatchEntry> heapPatches)
        {
            if (heapPatches == null || heapPatches.Count == 0)
                return Array.Empty<HeapPatchEntry>();

            return new List<HeapPatchEntry>(heapPatches).ToArray();
        }

        private static IReadOnlyList<NetworkReceiveMetadata> CopyNetworkReceivers(
            IReadOnlyList<BoundNetworkReceiveDeclaration> receivers)
        {
            if (receivers == null || receivers.Count == 0)
                return Array.Empty<NetworkReceiveMetadata>();

            var result = new List<NetworkReceiveMetadata>(receivers.Count);
            foreach (var receiver in receivers)
            {
                var parameters = new List<NetworkReceiveParameterMetadata>(
                    receiver.ReceiveSymbol.PhysicalParameters.Count);
                foreach (var physical in receiver.ReceiveSymbol.PhysicalParameters)
                {
                    parameters.Add(new NetworkReceiveParameterMetadata(
                        physical.PhysicalParameter.UdonStorageName,
                        physical.PhysicalParameter.Type.TypeKind,
                        physical.PhysicalParameter.Type.RuntimeQualifiedName));
                }

                result.Add(new NetworkReceiveMetadata(
                    receiver.ReceiveSymbol.ExportName,
                    parameters));
            }

            return result.ToArray();
        }

        private static IReadOnlyList<ExternalBindingMetadata> CopyExternalBindings(
            IReadOnlyList<BoundFunctionDeclaration> functions)
        {
            if (functions == null || functions.Count == 0)
                return Array.Empty<ExternalBindingMetadata>();

            var result = new List<ExternalBindingMetadata>();
            foreach (var declaration in functions)
            {
                var function = declaration.FunctionSymbol;
                var binding = function.ExternalBinding;
                if (binding == null)
                    continue;

                var sobakasuParameters = new string[function.Parameters.Count];
                for (var index = 0; index < function.Parameters.Count; index++)
                    sobakasuParameters[index] = function.Parameters[index].Type.QualifiedName;

                var abiParameters = binding.ExternalMethod.AbiParameters;
                var externalParameterCount = abiParameters?.Count ?? 0;
                var externalParameters = new string[externalParameterCount];
                var externalParameterModes =
                    new ExternalParameterPassingMode[externalParameterCount];
                var externalParameterProjections =
                    new ExternalParameterOutputProjection[externalParameterCount];
                for (var index = 0; index < externalParameterCount; index++)
                {
                    externalParameters[index] = abiParameters[index].Type.RuntimeQualifiedName;
                    externalParameterModes[index] = abiParameters[index].PassingMode switch
                    {
                        Binder.ExternParameterPassingMode.Ref => ExternalParameterPassingMode.Ref,
                        Binder.ExternParameterPassingMode.Out => ExternalParameterPassingMode.Out,
                        Binder.ExternParameterPassingMode.In => ExternalParameterPassingMode.In,
                        Binder.ExternParameterPassingMode.GenericTypeArgument =>
                            ExternalParameterPassingMode.GenericTypeArgument,
                        _ => ExternalParameterPassingMode.Normal
                    };
                    externalParameterProjections[index] =
                        abiParameters[index].LogicalOutputProjection ==
                            Binder.ExternLogicalOutputProjection.Maybe
                            ? ExternalParameterOutputProjection.Maybe
                            : ExternalParameterOutputProjection.Raw;
                }

                result.Add(new ExternalBindingMetadata(
                    function.InternalIdentity,
                    function.DisplayName,
                    function.DeclaringModule,
                    sobakasuParameters,
                    function.ReturnType.QualifiedName,
                    binding.ExternalDeclaringType.RuntimeQualifiedName,
                    binding.ExternalMemberName,
                    externalParameters,
                    externalParameterModes,
                    binding.ExternalMethod.AbiReturnType.RuntimeQualifiedName,
                    binding.ResolvedExternalSignature,
                    binding.InvocationKind == ExternalInvocationKind.Static
                        ? ExternalBindingInvocationKind.Static
                        : ExternalBindingInvocationKind.Instance,
                    binding.MemberKind switch
                    {
                        ExternMemberKind.Getter => ExternalBindingMemberKind.Getter,
                        ExternMemberKind.Setter => ExternalBindingMemberKind.Setter,
                        ExternMemberKind.Constructor => ExternalBindingMemberKind.Constructor,
                        ExternMemberKind.Operator => ExternalBindingMemberKind.Operator,
                        _ => ExternalBindingMemberKind.Method
                    },
                    binding.ReturnBindingMode == ExternalReturnBindingMode.Maybe
                        ? ExternalBindingReturnMode.Maybe
                        : ExternalBindingReturnMode.Raw,
                    externalParameterProjections));
            }

            return result.ToArray();
        }
    }
}
