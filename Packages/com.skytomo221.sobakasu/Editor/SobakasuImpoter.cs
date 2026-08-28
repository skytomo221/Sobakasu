#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Skytomo221.Sobakasu
{
    [ScriptedImporter(2, "sobakasu")]
    public sealed class SobakasuImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var sourceText = File.ReadAllText(ctx.assetPath);

            var programAsset = ScriptableObject.CreateInstance<SobakasuProgramAsset>();
            programAsset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

            var serializedProgramAsset =
                ScriptableObject.CreateInstance<VRC.Udon.ProgramSources.SerializedUdonProgramAsset>();
            serializedProgramAsset.name = $"{programAsset.name} Serialized Udon Program";
            serializedProgramAsset.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;

            programAsset.SetSerializedProgramAssetForImport(serializedProgramAsset);
            ctx.AddObjectToAsset("SobakasuProgram", programAsset);
            ctx.AddObjectToAsset("SerializedUdonProgram", serializedProgramAsset);
            ctx.SetMainObject(programAsset);

            SobakasuProgramCompiler.CompileAndStore(programAsset, sourceText);
        }
    }
}
#endif
