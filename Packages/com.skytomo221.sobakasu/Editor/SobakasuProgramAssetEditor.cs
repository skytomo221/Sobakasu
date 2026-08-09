#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Skytomo221.Sobakasu.Compiler; // ← ここで参照（EditorなのでOK）

namespace Skytomo221.Sobakasu
{
    [CustomEditor(typeof(SobakasuProgramAsset))]
    internal class SobakasuProgramAssetEditor : Editor
    {
        private const string PreviewImagePath =
            "Packages/com.skytomo221.sobakasu/Editor/Icons/SobakasuAsset.png";

        public override void OnInspectorGUI()
        {
            var programAsset = (SobakasuProgramAsset)target;
            bool dirty = false;

            programAsset.DrawProgramSourceGUI(null, ref dirty);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Compile (Sobakasu → UASM)"))
                {
                    CompileAndAssemble(programAsset);
                    dirty = true;
                }

                if (GUILayout.Button("Clear Errors"))
                {
                    programAsset.SetCompileError(null);
                    programAsset.SetPatchError(null);
                    dirty = true;
                }
            }

            if (dirty)
                EditorUtility.SetDirty(programAsset);
        }

        public override Texture2D RenderStaticPreview(
            string assetPath,
            Object[] subAssets,
            int width,
            int height)
        {
            var sourceTexture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(PreviewImagePath);

            if (sourceTexture == null)
            {
                Debug.LogWarning(
                    $"Sobakasu preview image was not found: {PreviewImagePath}");

                return null;
            }

            return CreatePreviewTexture(sourceTexture, width, height);
        }

        private static Texture2D CreatePreviewTexture(
            Texture2D sourceTexture,
            int width,
            int height)
        {
            var previousRenderTexture = RenderTexture.active;
            var temporaryRenderTexture = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32);

            try
            {
                Graphics.Blit(sourceTexture, temporaryRenderTexture);
                RenderTexture.active = temporaryRenderTexture;

                var previewTexture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false);

                previewTexture.ReadPixels(
                    new Rect(0, 0, width, height),
                    0,
                    0);

                previewTexture.Apply();

                return previewTexture;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(temporaryRenderTexture);
            }
        }

        private static void CompileAndAssemble(SobakasuProgramAsset programAsset)
        {
            if (programAsset.SourceAsset == null)
            {
                programAsset.SetCompileError("Sobakasu Source Asset is not assigned.");
                programAsset.SetPatchError(null);
                return;
            }

            var source = programAsset.SourceAsset.SourceText ?? "";
            var result = SobakasuCompiler.CompileToUasm(source);

            if (!result.Success)
            {
                programAsset.SetCompileError(result.ErrorText);
                programAsset.SetPatchError(null);
                return;
            }

            if (!programAsset.SetUasmAndAssemble(
                    result.Uasm,
                    result.NetworkReceivers,
                    out var asmErr))
            {
                programAsset.SetCompileError("Udon Assembly error:\n" + asmErr);
                programAsset.SetPatchError(null);
                return;
            }

            if (!programAsset.ApplyHeapPatches(result.HeapPatches, out var patchErr))
            {
                programAsset.SetCompileError(null);
                programAsset.SetPatchError(patchErr);
                return;
            }

            if (!programAsset.CommitProgram(result.HeapPatches, out var commitErr))
            {
                programAsset.SetCompileError(null);
                programAsset.SetPatchError(commitErr);
                return;
            }

            programAsset.SetCompileError(null);
            programAsset.SetPatchError(null);
        }
    }
}
#endif
