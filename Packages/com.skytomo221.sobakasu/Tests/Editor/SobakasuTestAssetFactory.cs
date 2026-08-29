using System;
using System.IO;
using System.Text;
using Skytomo221.Sobakasu.Compiler;
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class SobakasuTestAssetFactory
    {
        public static SobakasuProgramAsset CreateImportedProgramAsset(
            string folderNamePrefix,
            Action<string> registerForCleanup)
        {
            var folderGuid = AssetDatabase.CreateFolder(
                "Assets",
                $"{folderNamePrefix}_{Guid.NewGuid():N}");
            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            registerForCleanup?.Invoke(folderPath);

            var assetPath = $"{folderPath}/Program.sobakasu";
            WriteSource(assetPath, "on start {}");
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var asset = AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
            if (asset == null)
                throw new InvalidOperationException(
                    $"Failed to import Sobakasu program at '{assetPath}'.");

            return asset;
        }

        public static void WriteSource(string assetPath, string sourceText)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException("Unity project root was not found.");
            var fullPath = Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(
                fullPath,
                sourceText ?? string.Empty,
                new UTF8Encoding(false));
        }
    }

    internal static class SobakasuTestCompiler
    {
        public static SobakasuCompiler.CompileResult CompileWithoutStandardLibrary(
            string sourceText)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-empty-standard-library-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                return SobakasuCompiler.CompileToUasm(sourceText, root);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
