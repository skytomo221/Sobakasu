#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Skytomo221.Sobakasu.Compiler; // ← ここで参照（EditorなのでOK）

namespace Skytomo221.Sobakasu
{
    [CustomEditor(typeof(SobakasuProgramAsset))]
    internal class SobakasuProgramAssetEditor : Editor
    {
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
                    dirty = true;
                }
            }

            if (dirty)
                EditorUtility.SetDirty(programAsset);
        }

        private static void CompileAndAssemble(SobakasuProgramAsset programAsset)
        {
            if (programAsset.SourceAsset == null)
            {
                programAsset.SetCompileError("Sobakasu Source Asset is not assigned.");
                return;
            }

            var source = programAsset.SourceAsset.SourceText ?? "";

            // ここが “書いてほしい場所” の本体
            var result = SobakasuCompiler.CompileToUasm(source);

            if (!result.Success)
            {
                programAsset.SetCompileError(result.ErrorText);
                return;
            }

            if (!programAsset.SetUasmAndAssemble(result.Uasm, out var asmErr))
            {
                programAsset.SetCompileError("Udon Assembly error:\n" + asmErr);
                return;
            }

            programAsset.SetCompileError(null);
        }
    }
}
#endif
