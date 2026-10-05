using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using UnityEditor;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class ArrayProgramAssetIntegrationTests : SobakasuAssetCleanupFixture
    {
        [Test]
        public void UasmAssembler_AcceptsPublicAndNoneSynchronizedArrayStates()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"public state values: [i32];
sync state scores: [i32] = [];
on start {}",
                SobakasuUnityCompilationEnvironmentProvider.GetEnvironment());
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateImportedProgramAsset("SobakasuArrayTests");
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
        }

        [Test]
        public void RefreshProgram_ReappliesArrayStateHeapPatchManifest()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "state values: [i32] = [1, 2, 3]; on start {}",
                SobakasuUnityCompilationEnvironmentProvider.GetEnvironment());
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateImportedProgramAsset("SobakasuArrayTests");
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError),
                Is.True, commitError);
            RegisterForCleanup(AssetDatabase.GetAssetPath(asset.SerializedProgramAsset));

            asset.RefreshProgram();

            var patch = result.HeapPatches[0];
            var program = asset.GetRealProgram();
            var address = program.SymbolTable.GetAddressFromSymbol(patch.SymbolName);
            Assert.That(program.Heap.GetHeapVariable(address), Is.EqualTo(new[] { 1, 2, 3 }));
        }
    }
}
