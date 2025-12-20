using System;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources;
using UdonSharp.Localization;
using UdonSharpEditor;
using System.Collections.Immutable;
using JetBrains.Annotations;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Skytomo221.Sobakasu
{
    // Copy from Packages/com.vrchat.worlds/Integrations/UdonSharp/Runtime/Libraries/CompilerInternal/CompilerConstants.cs
    public static class CompilerConstants
    {
        public const string UsbTypeIDHeapKey = "__refl_typeid";
        public const string UsbTypeNameHeapKey = "__refl_typename";
        public const string UsbTypeIDArrayHeapKey = "__refl_typeids";
    }

    class SobakasuCompiler
    {
        public static UdonProgramAsset Compile(string source)
        {
            // Sobakasu Compilerの実装
            return null;
        }
    }

    [CreateAssetMenu(menuName = "VRChat/Udon/Sobakasu Program Asset", fileName = "New Sobakasu Program Asset")]
    public class SobakasuProgramAsset : UdonAssemblyProgramAsset
    {
        [HideInInspector]
        private TextAsset sourceSobakasuScript;

        [SerializeField]
        private string compileError = null;

        private UdonBehaviour currentBehaviour;

        public void SetSobakasuAssembly()
        {
            udonAssembly = sourceSobakasuScript.text;
        }

        protected override void RefreshProgramImpl()
        {
            SetSobakasuAssembly();
            AssembleProgram();
        }

        internal void DrawErrorTextAreas()
        {
            UdonSharpGUI.DrawCompileErrorTextArea();
            DrawAssemblyErrorTextArea();
        }

        internal new void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            DrawSobakasuScript(udonBehaviour);

            currentBehaviour = udonBehaviour;

            if (!udonBehaviour)
            {

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(Loc.Get(LocStr.UI_SerializedUdonProgramAsset), serializedUdonProgramAsset, typeof(AbstractSerializedUdonProgramAsset), false);
                EditorGUI.EndDisabledGroup();
            }

            bool shouldUseRuntimeValue = EditorApplication.isPlaying && currentBehaviour != null;

            // UdonBehaviours won't have valid heap values unless they have been enabled once to run their initialization. 
            // So we check against a value we know will exist to make sure we can use the heap variables.
            if (shouldUseRuntimeValue)
            {
                var behaviourID = currentBehaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (behaviourID == null)
                    shouldUseRuntimeValue = false;
            }

            // Just manually break the disabled scope in the UdonBehaviourEditor default drawing for now
            GUI.enabled = GUI.enabled || shouldUseRuntimeValue;
            shouldUseRuntimeValue &= GUI.enabled;

            DrawErrorTextAreas();
            UdonSharpGUI.DrawUtilities(this);
            currentBehaviour = null;
        }

        [PublicAPI]
        protected virtual void DrawSobakasuScript(UdonBehaviour udonBehaviour)
        {
            EditorGUILayout.LabelField("Sobakasu Script", EditorStyles.boldLabel);
            sourceSobakasuScript = (TextAsset)EditorGUILayout.ObjectField("Sobakasu Script", sourceSobakasuScript, typeof(TextAsset), false);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SobakasuProgramAsset))]
    internal class SobakasuProgramAssetEditor : Editor
    {
        // public override void OnInspectorGUI()
        // {
        //     var programAsset = (SobakasuProgramAsset)target;

        //     bool refBool = false;
        //     programAsset.DrawProgramSourceGUI(null, ref refBool);
        // }
    }
#endif
}
