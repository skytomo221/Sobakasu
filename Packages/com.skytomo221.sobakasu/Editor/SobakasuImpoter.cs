#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Skytomo221.Sobakasu
{
    [ScriptedImporter(1, "sobakasu")]
    public sealed class SobakasuImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);

            var sourceAsset = ScriptableObject.CreateInstance<SobakasuSourceAsset>();
            sourceAsset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            sourceAsset.SetSourceTextForImport(text);

            ctx.AddObjectToAsset("SobakasuSourceAsset", sourceAsset);
            ctx.SetMainObject(sourceAsset);
        }
    }
}
#endif
