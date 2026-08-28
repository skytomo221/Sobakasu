#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu
{
    [CustomEditor(typeof(SobakasuProgramAsset))]
    internal class SobakasuProgramAssetEditor : Editor
    {
        private const string PreviewImagePath =
            "Packages/com.skytomo221.sobakasu/Editor/Icons/SobakasuAsset.png";

        public override void OnInspectorGUI()
        {
            var programAsset = (SobakasuProgramAsset)target;
            bool dirty = false;

            programAsset.RunEditorUpdate(null, ref dirty);

            if (dirty)
                EditorUtility.SetDirty(programAsset);
        }

        public override Texture2D RenderStaticPreview(
            string assetPath,
            Object[] subAssets,
            int width,
            int height)
        {
            var sourceTexture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(PreviewImagePath);

            if (sourceTexture == null)
            {
                Debug.LogWarning(
                    $"Sobakasu preview image was not found: {PreviewImagePath}");

                return null;
            }

            return CreatePreviewTexture(sourceTexture, width, height);
        }

        private static Texture2D CreatePreviewTexture(
            Texture2D sourceTexture,
            int width,
            int height)
        {
            var previousRenderTexture = RenderTexture.active;
            var temporaryRenderTexture = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32);

            try
            {
                Graphics.Blit(sourceTexture, temporaryRenderTexture);
                RenderTexture.active = temporaryRenderTexture;

                var previewTexture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false);

                previewTexture.ReadPixels(
                    new Rect(0, 0, width, height),
                    0,
                    0);

                previewTexture.Apply();

                return previewTexture;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(temporaryRenderTexture);
            }
        }

    }
}
#endif
