using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.Udon;
using VRC.Udon.ProgramSources;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuImporterTests
    {
        private string _folderPath;
        private readonly List<GameObject> _gameObjects = new();

        [SetUp]
        public void SetUp()
        {
            var folderGuid = AssetDatabase.CreateFolder(
                "Assets",
                $"SobakasuImporterTests_{Guid.NewGuid():N}");
            _folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _gameObjects)
            {
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }
            _gameObjects.Clear();

            if (!string.IsNullOrWhiteSpace(_folderPath) &&
                AssetDatabase.IsValidFolder(_folderPath))
            {
                AssetDatabase.DeleteAsset(_folderPath);
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void Import_CreatesProgramSourceAsMainObject()
        {
            var assetPath = ImportSource(
                "MainObject.sobakasu",
                "state value: i32 = 1; on start {}");

            var programAsset =
                AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var importedObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            Assert.That(programAsset, Is.Not.Null);
            Assert.That(mainAsset, Is.SameAs(programAsset));
            Assert.That(programAsset, Is.InstanceOf<AbstractUdonProgramSource>());
            Assert.That(importedObjects, Has.Length.EqualTo(2));
            Assert.That(programAsset.SerializedProgramAsset,
                Is.TypeOf<SerializedUdonProgramAsset>());
            Assert.That(programAsset.CompileError, Is.Null.Or.Empty, programAsset.CompileError);
            Assert.That(programAsset.PatchError, Is.Null.Or.Empty, programAsset.PatchError);
            Assert.That(
                programAsset.SerializedProgramAsset.GetSerializedProgramSize(),
                Is.GreaterThan(0UL));
            Assert.That(
                programAsset.SerializedProgramAsset.RetrieveProgram(),
                Is.Not.Null);
            Assert.That(Attribute.IsDefined(
                typeof(SobakasuProgramAsset),
                typeof(CreateAssetMenuAttribute),
                false),
                Is.False);
        }

        [Test]
        public void ImportedProgram_CanBeAssignedDirectlyToUdonBehaviour()
        {
            var assetPath = ImportSource(
                "ProgramSource.sobakasu",
                "on interact {}");
            var programAsset =
                AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
            var behaviour = CreateUdonBehaviour();

            behaviour.programSource = programAsset;

            Assert.That(behaviour.programSource, Is.SameAs(programAsset));
            Assert.That(behaviour.programSource,
                Is.InstanceOf<AbstractUdonProgramSource>());
        }

        [Test]
        public void Reimport_PreservesProgramSourceReferenceAndUpdatesProgram()
        {
            var assetPath = ImportSource(
                "Reimport.sobakasu",
                "pub state value: i32 = 1; on start {}");
            var originalAsset =
                AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
            var originalSerializedAsset = originalAsset.SerializedProgramAsset;
            var behaviour = CreateUdonBehaviour();
            behaviour.programSource = originalAsset;

            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalAsset,
                out var originalGuid,
                out long originalLocalId),
                Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                originalSerializedAsset,
                out var originalSerializedGuid,
                out long originalSerializedLocalId),
                Is.True);
            Assert.That(originalAsset.CompileError,
                Is.Null.Or.Empty,
                originalAsset.CompileError);
            Assert.That(originalAsset.PatchError,
                Is.Null.Or.Empty,
                originalAsset.PatchError);
            AssertHeapValue(originalAsset.SerializedProgramAsset.RetrieveProgram(), "value", 1);

            SobakasuTestAssetFactory.WriteSource(
                assetPath,
                "pub state value: i32 = 2; on start {}");
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var reimportedAsset =
                AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
            var reimportedSerializedAsset = reimportedAsset.SerializedProgramAsset;

            Assert.That(behaviour.programSource, Is.SameAs(reimportedAsset));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                reimportedAsset,
                out var reimportedGuid,
                out long reimportedLocalId),
                Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                reimportedSerializedAsset,
                out var reimportedSerializedGuid,
                out long reimportedSerializedLocalId),
                Is.True);
            Assert.That(reimportedGuid, Is.EqualTo(originalGuid));
            Assert.That(reimportedLocalId, Is.EqualTo(originalLocalId));
            Assert.That(reimportedSerializedGuid, Is.EqualTo(originalSerializedGuid));
            Assert.That(reimportedSerializedLocalId,
                Is.EqualTo(originalSerializedLocalId));
            Assert.That(reimportedAsset.CompileError, Is.Null.Or.Empty);
            Assert.That(reimportedAsset.PatchError, Is.Null.Or.Empty);
            AssertHeapValue(
                reimportedAsset.SerializedProgramAsset.RetrieveProgram(),
                "value",
                2);
        }

        [Test]
        public void CompileError_PreservesAssetAndReferenceButInvalidatesProgram()
        {
            const string invalidSource = "on start { let value = ; }";
            var assetPath = ImportSource(
                "CompileError.sobakasu",
                "state value: i32 = 1; on start {}");
            var validAsset =
                AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
            var behaviour = CreateUdonBehaviour();
            behaviour.programSource = validAsset;

            var compileResult = SobakasuCompiler.CompileToUasm(invalidSource);
            foreach (var diagnostic in compileResult.Diagnostics)
            {
                LogAssert.Expect(
                    ToLogType(diagnostic.Severity),
                    SobakasuUnityDiagnosticReporter.FormatMessage(
                        null,
                        assetPath,
                        invalidSource,
                        diagnostic));
            }

            SobakasuTestAssetFactory.WriteSource(assetPath, invalidSource);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var failedAsset =
                AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
            Assert.That(failedAsset, Is.Not.Null);

            var compileError = failedAsset.CompileError;

            failedAsset.RefreshProgram();

            Assert.That(behaviour.programSource, Is.SameAs(failedAsset));
            Assert.That(failedAsset.CompileError, Is.Not.Null.And.Not.Empty);
            Assert.That(failedAsset.CompileError, Is.EqualTo(compileError));
            Assert.That(failedAsset.GetRealProgram(), Is.Null);
            Assert.That(failedAsset.SerializedProgramAsset, Is.Not.Null);
            Assert.That(failedAsset.SerializedProgramAsset.RetrieveProgram(), Is.Null);
        }

        private string ImportSource(string fileName, string sourceText)
        {
            var assetPath = $"{_folderPath}/{fileName}";
            SobakasuTestAssetFactory.WriteSource(assetPath, sourceText);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            return assetPath;
        }

        private UdonBehaviour CreateUdonBehaviour()
        {
            var gameObject = new GameObject("Sobakasu Importer Test");
            _gameObjects.Add(gameObject);
            return gameObject.AddComponent<UdonBehaviour>();
        }

        private static LogType ToLogType(DiagnosticSeverity severity)
        {
            return severity switch
            {
                DiagnosticSeverity.Error => LogType.Error,
                DiagnosticSeverity.Warning => LogType.Warning,
                _ => LogType.Log
            };
        }

        private static void AssertHeapValue(
            VRC.Udon.Common.Interfaces.IUdonProgram program,
            string symbolName,
            object expected)
        {
            Assert.That(program, Is.Not.Null);
            Assert.That(program.SymbolTable.TryGetAddressFromSymbol(
                symbolName,
                out var address),
                Is.True);
            Assert.That(program.Heap.GetHeapVariable(address), Is.EqualTo(expected));
        }
    }
}
