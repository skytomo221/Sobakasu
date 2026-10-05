using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class StateUnityIntegrationTests
    {
        private readonly List<string> _cleanupAssetPaths = new();

        [TearDown]
        public void TearDown()
        {
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

        [Test]
        public void CompileToUasm_KeepsPrivateSynchronizedStateOutOfSourcePublicApi()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"sync state private_status = 0;
public state public_status: i32;
on interact() { private_status = public_status; }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            var privatePatch = FindStatePatch(result.HeapPatches, "__state_");
            Assert.That(privatePatch, Is.Not.Null);
            Assert.That(result.Uasm, Does.Contain($".sync {privatePatch.SymbolName}, none"));
            Assert.That(result.Uasm, Does.Not.Contain($".export {privatePatch.SymbolName}"));
            Assert.That(result.Uasm, Does.Contain(".export public_status"));

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
        }

        [Test]
        public void CompileToUasm_PreservesRepresentableQuotedAndUnicodePublicNames()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"public state 日本語テストの変数: string;
public state `if`: string;
public state `void`: string;
on start {}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export 日本語テストの変数"));
            Assert.That(result.Uasm, Does.Contain(".export if"));
            Assert.That(result.Uasm, Does.Contain(".export void"));

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
        }

        [Test]
        public void AssemblePatchCommitAndRefresh_PreservesPrivateStateInitialValueAndSyncMetadata()
        {
            const string source = @"sync(linear) state value: f32 = -2.5;
on update() { extern UnityEngine.Debug.Log(value); }";
            var result = SobakasuTestEnvironment.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);
            var statePatch = FindStatePatch(result.HeapPatches, "__state_");
            Assert.That(statePatch, Is.Not.Null, FormatHeapPatches(result.HeapPatches));

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError),
                Is.True, commitError);

            AssertProgramState(asset, statePatch.SymbolName, -2.5f, "Linear");
            asset.RefreshProgram();
            AssertProgramState(asset, statePatch.SymbolName, -2.5f, "Linear");
        }

        private SobakasuProgramAsset CreateProgramAsset()
        {
            return SobakasuTestAssetFactory.CreateImportedProgramAsset(
                "SobakasuStateVariableTests",
                _cleanupAssetPaths.Add);
        }

        private static HeapPatchEntry FindStatePatch(
            IReadOnlyList<HeapPatchEntry> patches,
            string symbolFragment)
        {
            foreach (var patch in patches)
            {
                if (patch.Kind == HeapPatchKind.GlobalInitializer &&
                    patch.SymbolName.IndexOf(symbolFragment, StringComparison.Ordinal) >= 0)
                {
                    return patch;
                }
            }

            return null;
        }

        private static string FormatHeapPatches(IReadOnlyList<HeapPatchEntry> patches)
        {
            var entries = new List<string>();
            foreach (var patch in patches)
                entries.Add($"{patch.Kind}:{patch.SymbolName}");
            return string.Join(", ", entries);
        }

        private static void AssertProgramState(
            SobakasuProgramAsset asset,
            string symbol,
            object expectedValue,
            string expectedInterpolation)
        {
            var program = asset.GetRealProgram();
            Assert.That(program, Is.Not.Null);
            Assert.That(program.SymbolTable.HasExportedSymbol(symbol), Is.False);
            var address = program.SymbolTable.GetAddressFromSymbol(symbol);
            Assert.That(program.Heap.GetHeapVariable(address), Is.EqualTo(expectedValue));

            var sync = program.SyncMetadataTable.GetSyncMetadataFromSymbol(symbol);
            Assert.That(sync, Is.Not.Null);
            Assert.That(sync.Properties.Count, Is.GreaterThan(0));
            Assert.That(sync.Properties[0].InterpolationAlgorithm.ToString(),
                Is.EqualTo(expectedInterpolation));
        }
    }
}
