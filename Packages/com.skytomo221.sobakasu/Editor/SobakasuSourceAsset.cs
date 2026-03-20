using UnityEngine;

namespace Skytomo221.Sobakasu
{
  public sealed class SobakasuSourceAsset : ScriptableObject
  {
    [SerializeField, TextArea(8, 200)]
    private string sourceText = "";

    public string SourceText => sourceText;

#if UNITY_EDITOR
    public void SetSourceTextForImport(string text)
    {
        sourceText = text ?? "";
    }
#endif
  }
}
