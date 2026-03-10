using System;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Skytomo221.Sobakasu
{
    [CreateAssetMenu(menuName = "VRChat/Udon/Sobakasu Program Asset", fileName = "New Sobakasu Program Asset")]
    public class SobakasuProgramAsset : UdonAssemblyProgramAsset
    {
        [SerializeField]
        private SobakasuSourceAsset sourceAsset;

        public SobakasuSourceAsset SourceAsset => sourceAsset;

        [SerializeField, TextArea]
        private string compileError = null;

        public string CompileError => compileError;

        // Editor側から結果を書き込むためのAPI（Compiler参照しない）
        public void SetCompileError(string text)
        {
            compileError = text;
        }

        public bool SetUasmAndAssemble(string uasm, out string error)
        {
            error = null;
            compileError = null;
            udonAssembly = uasm ?? "";

            AssembleProgram();

            if (program == null)
            {
                error = assemblyError;
                return false;
            }

            SerializedProgramAsset.StoreProgram(program);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(SerializedProgramAsset);
#endif

            return true;
        }

        protected override void RefreshProgramImpl()
        {
            // ここで Compiler を呼ばない（Editor拡張側でやる）
            // 既存 udonAssembly を再アセンブルしたいなら以下を残してもOK
            compileError = null;

            if (sourceAsset == null)
            {
                compileError = "Sobakasu Source Asset is not assigned.";
                return;
            }

            // ここは「既にudonAssemblyがセットされている」前提で再アセンブルのみ。
            // 自動再コンパイルしたいなら CustomEditor/Importer で呼ぶ。
            try
            {
                AssembleProgram();
            }
            catch (Exception e)
            {
                compileError = "Udon Assembly error:\n" + e.Message;
            }
        }

#if UNITY_EDITOR
        public void DrawErrorTextAreas()
        {
            if (!string.IsNullOrEmpty(compileError))
            {
                EditorGUILayout.LabelField("Sobakasu Compile Error", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(compileError, MessageType.Error);
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
