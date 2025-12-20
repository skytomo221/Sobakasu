using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;

namespace Skytomo221.Sobakasu
{
  [ScriptedImporter(1, "sobakasu")]
  public class SobakasuScriptImporter : ScriptedImporter
  {
    public override void OnImportAsset(AssetImportContext sobakasu)
    {
      var subAsset = new TextAsset(File.ReadAllText(sobakasu.assetPath));
      sobakasu.AddObjectToAsset("sobakasu", subAsset);
      sobakasu.SetMainObject(subAsset);
      Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Skytomo221/Sobakasu/sobakasu-icon.png");
      if (icon != null)
      {
        EditorGUIUtility.SetIconForObject(subAsset, icon);
        AssetDatabase.SetLabels(subAsset, new string[] { "sobakasu", "icon" });
        EditorUtility.SetDirty(subAsset);
      }
      else
      {
        Debug.LogWarning("Icon not found at specified path.");
      }
      Debug.Log($"Sobakasu file imported: {sobakasu.assetPath}");
    }
  }
}
