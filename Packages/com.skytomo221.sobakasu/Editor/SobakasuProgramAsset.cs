using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Skytomo221.Sobakasu
{
    [CreateAssetMenu(menuName = "VRChat/Udon/Sobakasu Program Asset", fileName = "New Sobakasu Program Asset")]
    public class SobakasuProgramAsset : UdonAssemblyProgramAsset
    {
        [Serializable]
        private sealed class SerializedHeapPatchEntry
        {
            public string symbolName;
            public TypeKind symbolType;
            public bool hasRuntimeValue;
            public string runtimeValueText;
            public HeapPatchKind kind;
            public bool hasSourceSpan;
            public int sourceSpanStart;
            public int sourceSpanLength;
        }

        [SerializeField]
        private SobakasuSourceAsset sourceAsset;

        public SobakasuSourceAsset SourceAsset => sourceAsset;

        [SerializeField, TextArea]
        private string compileError = null;

        public string CompileError => compileError;

        [SerializeField, TextArea]
        private string patchError = null;

        public string PatchError => patchError;

        [SerializeField]
        private bool hasStoredHeapPatchManifest;

        [SerializeField]
        private List<SerializedHeapPatchEntry> serializedHeapPatches = new();

        public void SetCompileError(string text)
        {
            compileError = text;
        }

        public void SetPatchError(string text)
        {
            patchError = text;
        }

        public bool SetUasmAndAssemble(string uasm, out string error)
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

                    var systemType = SobakasuTypeMapper.ToSystemType(patch.SymbolType);
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

                var serializedProgram = SerializedProgramAsset;
                serializedProgram.StoreProgram(program);

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
            compileError = null;
            patchError = null;

            if (string.IsNullOrWhiteSpace(udonAssembly))
            {
                compileError = "Sobakasu Udon Assembly is empty.";
                program = null;
                return;
            }

            AssembleProgram();
            if (program == null)
            {
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
                    hasRuntimeValue = patch.RuntimeValue != null,
                    runtimeValueText = patch.RuntimeValue == null
                        ? null
                        : HeapPatchValueSerializer.SerializeRuntimeValue(
                            patch.RuntimeValue,
                            patch.SymbolType),
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
                            serializedEntry.symbolType)
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
                            sourceSpan));
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
            if (!span.HasValue || sourceAsset == null)
            {
                sourceLocation = null;
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(sourceAsset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                sourceLocation = null;
                return false;
            }

            var sourceText = SourceText.From(sourceAsset.SourceText ?? string.Empty);
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

        public new void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            if (!udonBehaviour)
            {
                EditorGUILayout.LabelField("Sobakasu Source", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                var newSource = (SobakasuSourceAsset)EditorGUILayout.ObjectField(
                    "Source Asset", sourceAsset, typeof(SobakasuSourceAsset), false);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Changed Sobakasu source");
                    sourceAsset = newSource;
                    EditorUtility.SetDirty(this);
                    dirty = true;
                }

                EditorGUILayout.Space(6);
            }

            DrawErrorTextAreas();
        }
#endif
    }
}
