using System;
using System.Collections.Generic;
using System.IO;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;
using VRC.Udon.ProgramSources;
using VRC.SDK3.UdonNetworkCalling;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Skytomo221.Sobakasu
{
    public class SobakasuProgramAsset : UdonAssemblyProgramAsset
    {
        [Serializable]
        private sealed class SerializedHeapPatchEntry
        {
            public string symbolName;
            public TypeKind symbolType;
            public string runtimeTypeName;
            public bool hasRuntimeValue;
            public string runtimeValueText;
            public HeapPatchKind kind;
            public bool hasSourceSpan;
            public int sourceSpanStart;
            public int sourceSpanLength;
        }

        [Serializable]
        private sealed class SerializedNetworkParameter
        {
            public string storageName;
            public TypeKind type;
            public string runtimeTypeName;
        }

        [Serializable]
        private sealed class SerializedNetworkReceiver
        {
            public string name;
            public List<SerializedNetworkParameter> parameters = new();
        }

        [SerializeField, TextArea]
        private string compileError = null;

        public string CompileError => compileError;

        [SerializeField, TextArea]
        private string patchError = null;

        public string PatchError => patchError;

        [SerializeField]
        private bool hasStoredHeapPatchManifest;

        private readonly List<SerializedHeapPatchEntry> serializedHeapPatches = new();

        private readonly List<SerializedNetworkReceiver> serializedNetworkReceivers = new();

        public override AbstractSerializedUdonProgramAsset SerializedProgramAsset =>
            serializedUdonProgramAsset;

        internal void SetSerializedProgramAssetForImport(
            SerializedUdonProgramAsset serializedProgramAsset)
        {
            this.serializedUdonProgramAsset = serializedProgramAsset != null ? serializedProgramAsset : throw new ArgumentNullException(nameof(serializedProgramAsset));
        }

        internal void SetCompilationFailure(string error)
        {
            program = null;
            udonAssembly = string.Empty;
            assemblyError = null;
            compileError = string.IsNullOrWhiteSpace(error)
                ? "Sobakasu compilation failed."
                : error;
            patchError = null;
            serializedNetworkReceivers.Clear();
            ClearStoredHeapPatchManifest();

            if (!TryInvalidatePersistedProgram(out var invalidationError) &&
                !string.IsNullOrWhiteSpace(invalidationError))
            {
                patchError = $"Failed to invalidate persisted program. {invalidationError}";
            }
        }

        public bool SetUasmAndAssemble(string uasm, out string error)
        {
            return SetUasmAndAssemble(
                uasm,
                Array.Empty<NetworkReceiveMetadata>(),
                out error);
        }

        public bool SetUasmAndAssemble(
            string uasm,
            IEnumerable<NetworkReceiveMetadata> networkReceivers,
            out string error)
        {
            error = null;
            compileError = null;
            patchError = null;

            var previousAssembly = udonAssembly;
            udonAssembly = uasm ?? string.Empty;

            AssembleProgram();

            if (program == null)
            {
                error = assemblyError;
                udonAssembly = previousAssembly;
                return false;
            }


            StoreNetworkReceiveMetadata(networkReceivers);

            return true;
        }

        public IUdonProgram GetRealProgram()
        {
            return program;
        }

        public bool ApplyHeapPatches(IEnumerable<HeapPatchEntry> patches, out string error)
        {
            var realProgram = GetRealProgram();
            if (realProgram == null)
            {
                return FailPatch("IUdonProgram is null.", out error);
            }

            foreach (var patch in EnumeratePatches(patches))
            {
                if (patch == null)
                {
                    return FailPatch("Encountered a null heap patch entry.", out error);
                }

                try
                {
                    if (!realProgram.SymbolTable.TryGetAddressFromSymbol(
                            patch.SymbolName,
                            out var address))
                    {
                        return FailPatch(
                            BuildPatchFailureMessage(
                                patch,
                                $"Symbol '{patch.SymbolName}' was not found in the program symbol table."),
                            out error);
                    }

                    var systemType = SobakasuTypeMapper.ToSystemType(
                        patch.SymbolType,
                        patch.RuntimeTypeName);
                    realProgram.Heap.SetHeapVariable(address, patch.RuntimeValue, systemType);
                }
                catch (Exception ex)
                {
                    return FailPatch(
                        BuildPatchFailureMessage(patch, ex.ToString()),
                        out error);
                }
            }

            patchError = null;
            error = string.Empty;
            return true;
        }

        public bool CommitProgram(IEnumerable<HeapPatchEntry> patches, out string error)
        {
            if (program == null)
            {
                error = "IUdonProgram is null.";
                return false;
            }

            try
            {
                StoreHeapPatchManifest(patches);

                var serializedProgram = serializedUdonProgramAsset;
                if (serializedProgram == null)
                {
                    return FailPatch(
                        "Serialized Udon program is not initialized.",
                        out error);
                }

                serializedProgram.StoreProgram(
                    program,
                    GetLastNetworkCallingMetadata());

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
                if (serializedProgram != null)
                {
                    EditorUtility.SetDirty(serializedProgram);
                }
#endif

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                return FailPatch($"Failed to commit Sobakasu program. {ex}", out error);
            }
        }

        protected override void RefreshProgramImpl()
        {
            patchError = null;

            if (string.IsNullOrWhiteSpace(udonAssembly))
            {
                program = null;
                if (string.IsNullOrWhiteSpace(compileError))
                    compileError = "Sobakasu Udon Assembly is empty.";
                TryInvalidatePersistedProgram(out _);
                return;
            }

            AssembleProgram();
            if (program == null)
            {
                TryInvalidatePersistedProgram(out _);
                return;
            }

            if (!TryLoadHeapPatchManifest(out var patches, out var manifestError))
            {
                FailPatch(manifestError, out _);
                return;
            }

            if (!ApplyHeapPatches(patches, out var applyError))
            {
                patchError = applyError;
                return;
            }

            try
            {
                StoreHeapPatchManifest(patches);
            }
            catch (Exception ex)
            {
                FailPatch($"Failed to store Sobakasu heap patch manifest. {ex}", out _);
            }
        }

        protected override NetworkCallingEntrypointMetadata[] GetLastNetworkCallingMetadata()
        {
            if (serializedNetworkReceivers == null ||
                serializedNetworkReceivers.Count == 0)
            {
                return null;
            }

            var entries = new List<NetworkCallingEntrypointMetadata>(
                serializedNetworkReceivers.Count);
            foreach (var receiver in serializedNetworkReceivers)
            {
                var parameters = new List<NetworkCallingParameterMetadata>(
                    receiver.parameters?.Count ?? 0);
                if (receiver.parameters != null)
                {
                    foreach (var parameter in receiver.parameters)
                    {
                        parameters.Add(new NetworkCallingParameterMetadata(
                            parameter.storageName,
                            SobakasuTypeMapper.ToSystemType(
                                parameter.type,
                                parameter.runtimeTypeName)));
                    }
                }

                entries.Add(new NetworkCallingEntrypointMetadata(
                    receiver.name,
                    new NetworkCallableAttribute(5),
                    parameters.ToArray()));
            }

            return entries.ToArray();
        }

        private void StoreNetworkReceiveMetadata(
            IEnumerable<NetworkReceiveMetadata> receivers)
        {
            serializedNetworkReceivers.Clear();
            if (receivers != null)
            {
                foreach (var receiver in receivers)
                {
                    if (receiver == null)
                        throw new InvalidOperationException(
                            "Network receiver metadata contains a null entry.");

                    var serialized = new SerializedNetworkReceiver
                    {
                        name = receiver.Name
                    };
                    foreach (var parameter in receiver.Parameters)
                    {
                        serialized.parameters.Add(new SerializedNetworkParameter
                        {
                            storageName = parameter.StorageName,
                            type = parameter.Type,
                            runtimeTypeName = parameter.RuntimeTypeName
                        });
                    }
                    serializedNetworkReceivers.Add(serialized);
                }
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        private IReadOnlyList<HeapPatchEntry> EnumeratePatches(IEnumerable<HeapPatchEntry> patches)
        {
            if (patches == null)
            {
                return Array.Empty<HeapPatchEntry>();
            }

            if (patches is IReadOnlyList<HeapPatchEntry> readOnlyList)
            {
                return readOnlyList;
            }

            return new List<HeapPatchEntry>(patches).ToArray();
        }

        private void StoreHeapPatchManifest(IEnumerable<HeapPatchEntry> patches)
        {
            serializedHeapPatches.Clear();

            foreach (var patch in EnumeratePatches(patches))
            {
                if (patch == null)
                {
                    throw new InvalidOperationException("Heap patch manifest contains a null entry.");
                }

                var serializedEntry = new SerializedHeapPatchEntry
                {
                    symbolName = patch.SymbolName,
                    symbolType = patch.SymbolType,
                    runtimeTypeName = patch.RuntimeTypeName,
                    hasRuntimeValue = patch.RuntimeValue != null,
                    runtimeValueText = patch.RuntimeValue == null
                        ? null
                        : HeapPatchValueSerializer.SerializeRuntimeValue(
                            patch.RuntimeValue,
                            patch.SymbolType,
                            patch.RuntimeTypeName),
                    kind = patch.Kind,
                    hasSourceSpan = patch.SourceSpan.HasValue,
                    sourceSpanStart = patch.SourceSpan?.Start ?? 0,
                    sourceSpanLength = patch.SourceSpan?.Length ?? 0
                };

                serializedHeapPatches.Add(serializedEntry);
            }

            hasStoredHeapPatchManifest = true;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        private bool TryLoadHeapPatchManifest(
            out IReadOnlyList<HeapPatchEntry> patches,
            out string error)
        {
            if (!hasStoredHeapPatchManifest)
            {
                patches = Array.Empty<HeapPatchEntry>();
                error = "Heap patch manifest is missing.";
                return false;
            }

            if (serializedHeapPatches.Count == 0)
            {
                patches = Array.Empty<HeapPatchEntry>();
                error = string.Empty;
                return true;
            }

            var entries = new List<HeapPatchEntry>(serializedHeapPatches.Count);
            foreach (var serializedEntry in serializedHeapPatches)
            {
                try
                {
                    var runtimeValue = serializedEntry.hasRuntimeValue
                        ? HeapPatchValueSerializer.DeserializeRuntimeValue(
                            serializedEntry.runtimeValueText ?? string.Empty,
                            serializedEntry.symbolType,
                            serializedEntry.runtimeTypeName)
                        : null;
                    var sourceSpan = serializedEntry.hasSourceSpan
                        ? new TextSpan(serializedEntry.sourceSpanStart, serializedEntry.sourceSpanLength)
                        : (TextSpan?)null;

                    entries.Add(
                        new HeapPatchEntry(
                            serializedEntry.symbolName,
                            serializedEntry.symbolType,
                            runtimeValue,
                            serializedEntry.kind,
                            sourceSpan,
                            serializedEntry.runtimeTypeName));
                }
                catch (Exception ex)
                {
                    patches = Array.Empty<HeapPatchEntry>();
                    error =
                        $"Failed to restore heap patch manifest for symbol '{serializedEntry.symbolName}' as '{serializedEntry.symbolType}' ({serializedEntry.kind}). {ex}";
                    return false;
                }
            }

            patches = entries.ToArray();
            error = string.Empty;
            return true;
        }

        private void ClearStoredHeapPatchManifest()
        {
            hasStoredHeapPatchManifest = false;
            serializedHeapPatches.Clear();
        }

        private bool FailPatch(string message, out string error)
        {
            program = null;
            patchError = message;
            ClearStoredHeapPatchManifest();

            if (!TryInvalidatePersistedProgram(out var invalidationError) &&
                !string.IsNullOrWhiteSpace(invalidationError))
            {
                patchError = $"{message} Failed to invalidate persisted program. {invalidationError}";
            }

            error = patchError;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif

            return false;
        }

        private bool TryInvalidatePersistedProgram(out string error)
        {
            error = string.Empty;

            if (serializedUdonProgramAsset == null)
            {
                return true;
            }

            try
            {
                serializedUdonProgramAsset.StoreProgram(null);

#if UNITY_EDITOR
                EditorUtility.SetDirty(serializedUdonProgramAsset);
#endif

                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private string BuildPatchFailureMessage(HeapPatchEntry patch, string detail)
        {
            var locationPrefix = TryFormatSourceLocation(patch.SourceSpan, out var sourceLocation)
                ? $"{sourceLocation} "
                : string.Empty;
            var spanSuffix = patch.SourceSpan.HasValue
                ? $" span {patch.SourceSpan.Value}"
                : " span <unknown>";

            return
                $"{locationPrefix}symbol '{patch.SymbolName}' patch failed as '{patch.SymbolType}' ({patch.Kind}) at{spanSuffix}. {detail}";
        }

        private bool TryFormatSourceLocation(TextSpan? span, out string sourceLocation)
        {
#if UNITY_EDITOR
            if (!span.HasValue)
            {
                sourceLocation = null;
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            {
                sourceLocation = null;
                return false;
            }

            var sourceText = SourceText.From(File.ReadAllText(assetPath));
            var line = sourceText.GetLineFromPosition(span.Value.Start);
            var lineIndex = GetLineIndex(sourceText, line);
            var column = span.Value.Start - line.Start + 1;
            sourceLocation = $"{assetPath.Replace('\\', '/')}:{lineIndex + 1}:{column}";
            return true;
#else
            sourceLocation = null;
            return false;
#endif
        }

        private static int GetLineIndex(SourceText sourceText, TextLine targetLine)
        {
            for (var index = 0; index < sourceText.Lines.Count; index++)
            {
                if (ReferenceEquals(sourceText.Lines[index], targetLine))
                {
                    return index;
                }
            }

            return 0;
        }

#if UNITY_EDITOR
        public void DrawErrorTextAreas()
        {
            if (!string.IsNullOrEmpty(compileError))
            {
                EditorGUILayout.LabelField("Sobakasu Compile Error", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(compileError, MessageType.Error);
            }

            if (!string.IsNullOrEmpty(patchError))
            {
                EditorGUILayout.LabelField("Sobakasu Patch Error", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(patchError, MessageType.Error);
            }

            DrawAssemblyErrorTextArea();
        }

        protected override void DrawProgramSourceGUI(
            UdonBehaviour udonBehaviour,
            ref bool dirty)
        {
            DrawErrorTextAreas();
            DrawPublicVariables(udonBehaviour, ref dirty);

            if (program != null)
                DrawProgramDisassembly();
        }
#endif
    }
}
