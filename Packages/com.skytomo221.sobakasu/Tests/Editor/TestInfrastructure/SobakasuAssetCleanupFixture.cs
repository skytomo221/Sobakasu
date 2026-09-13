using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu;
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public abstract class SobakasuAssetCleanupFixture
    {
        private readonly List<string> _cleanupAssetPaths = new();

        [TearDown]
        public void TearDown()
        {
            if (_cleanupAssetPaths.Count == 0)
            {
                return;
            }

            _cleanupAssetPaths.Sort((left, right) => right.Length.CompareTo(left.Length));
            foreach (var assetPath in _cleanupAssetPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null ||
                    AssetDatabase.IsValidFolder(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }

            _cleanupAssetPaths.Clear();
            AssetDatabase.Refresh();
        }

        protected SobakasuProgramAsset CreateImportedProgramAsset(string directoryName)
        {
            return SobakasuTestAssetFactory.CreateImportedProgramAsset(
                directoryName,
                RegisterForCleanup);
        }

        protected void RegisterForCleanup(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                _cleanupAssetPaths.Add(assetPath);
            }
        }
    }
}
