using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Bindings;

namespace Skytomo221.Sobakasu
{

  public class SobakasuScriptPostprocessor : AssetPostprocessor
  {
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
      foreach (string asset in importedAssets)
      {
        if (asset.EndsWith(".sobakasu"))
        {
          Debug.Log($"Sobakasu file imported: {asset}");
          // 必要に応じて処理を追加
          
        }
      }
    }
  }

  internal class SobakasuScriptModificationPreProcessor : AssetModificationProcessor
  {
    private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
    {
      if (!assetPath.EndsWith(".sobakasu", StringComparison.OrdinalIgnoreCase) || AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath) == null)
      {
        return AssetDeleteResult.DidNotDelete;
      }

      // SobakasuProgramAsset.ClearProgramAssetCache();

      return AssetDeleteResult.DidNotDelete;
    }
  }
}
