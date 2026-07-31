#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu
{
    [CustomEditor(typeof(SobakasuSourceAsset))]
    public sealed class SobakasuSourceAssetEditor : Editor
    {
        private const string IconPath =
            "Packages/com.skytomo221.sobakasu/Editor/Icons/SobakasuSource.png";

        public override Texture2D RenderStaticPreview(
            string assetPath,
            Object[] subAssets,
            int width,
            int height)
        {
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (source == null)
            {
                Debug.LogWarning($"Sobakasu preview image was not found: {IconPath}");
                return null;
            }

            return ResizeTexture(source, width, height);
        }

        private static Texture2D ResizeTexture(
            Texture2D source,
            int width,
            int height)
        {
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32);

            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                var preview = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false);

                preview.ReadPixels(
                    new Rect(0, 0, width, height),
                    0,
                    0);

                preview.Apply();
                return preview;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }
    }
}
#endif