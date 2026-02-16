using System;
using JetBrains.Annotations;
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

        protected override void RefreshProgramImpl()
        {
            compileError = null;

            if (sourceAsset == null)
            {
                compileError = "Sobakasu Source Asset is not assigned.";
                return;
            }

            // いまは「Sobakasu = 生UASM」として扱う（スタブ）
            // 将来ここを SobakasuCompiler.Compile(sourceAsset.SourceText) に差し替える
            udonAssembly = sourceAsset.SourceText;

            try
            {
                AssembleProgram();
            }
            catch (Exception e)
            {
                compileError = e.Message;
                throw;
            }
        }

#if UNITY_EDITOR
        internal void DrawErrorTextAreas()
        {
            if (!string.IsNullOrEmpty(compileError))
            {
                EditorGUILayout.LabelField("Sobakasu Compile Error", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(compileError, MessageType.Error);
            }

            DrawAssemblyErrorTextArea();
        }

        internal new void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
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

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rebuild Program"))
                    {
                        RefreshProgram();
                        dirty = true;
                    }
                    if (GUILayout.Button("Clear Errors"))
                    {
                        compileError = null;
                        dirty = true;
                    }
                }

                EditorGUILayout.Space(6);
            }

            DrawErrorTextAreas();

            // 任意：UASM表示や逆アセンブルが使えるなら呼ぶ
            // DrawAssemblyText();
            // DrawProgramDisassembly();
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SobakasuProgramAsset))]
    internal class SobakasuProgramAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var programAsset = (SobakasuProgramAsset)target;
            bool dirty = false;
            programAsset.DrawProgramSourceGUI(null, ref dirty);
        }
    }
#endif
}
