using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class AggregateProgramAssetIntegrationTests : SobakasuAssetCleanupFixture
    {
        [Test]
        public void RefreshProgram_RestoresFlattenedAggregateInitialValues()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point {
  x: i32,
  y: i32,
}
state {
  sync point = Point { x: 10, y: 20, };
}
behavior {
  on start {}
}",
                SobakasuUnityCompilationEnvironmentProvider.GetEnvironment());
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateImportedProgramAsset("SobakasuAggregateStateTests");
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError),
                Is.True, commitError);
            asset.RefreshProgram();

            var program = asset.GetRealProgram();
            Assert.That(program.Heap.GetHeapVariable(
                program.SymbolTable.GetAddressFromSymbol("__state_0")), Is.EqualTo(10));
            Assert.That(program.Heap.GetHeapVariable(
                program.SymbolTable.GetAddressFromSymbol("__state_1")), Is.EqualTo(20));
        }
    }
}
