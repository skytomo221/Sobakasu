using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using UnityEditor;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.UdonNetworkCalling;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class NetworkProgramAssetIntegrationTests : SobakasuAssetCleanupFixture
    {
        [Test]
        public void ProgramAsset_PreservesNetworkMetadataAcrossRefresh()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"behavior {
  receive notify(value: i32) {}

  on interact {
    send behavior::notify(1) to self;
  }
}",
                SobakasuUnityCompilationEnvironmentProvider.GetEnvironment());
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateImportedProgramAsset("SobakasuNetworkEventTests");
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, result.NetworkReceivers,
                out var assemblyError), Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError),
                Is.True, commitError);
            RegisterForCleanup(AssetDatabase.GetAssetPath(asset.SerializedProgramAsset));

            AssertNetworkMetadata(asset.SerializedProgramAsset.GetNetworkCallingMetadata());
            asset.RefreshProgram();
            Assert.That(asset.GetRealProgram(), Is.Not.Null);
            AssertNetworkMetadata(asset.SerializedProgramAsset.GetNetworkCallingMetadata());
        }

        private static void AssertNetworkMetadata(NetworkCallingEntrypointMetadata[] metadata)
        {
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata, Has.Length.EqualTo(1));
            Assert.That(metadata[0].Name, Is.EqualTo("notify"));
            Assert.That(metadata[0].MaxEventsPerSecond, Is.EqualTo(5));
            Assert.That(metadata[0].Parameters, Has.Length.EqualTo(1));
            Assert.That(metadata[0].Parameters[0].Name,
                Does.StartWith("__receive_param_"));
            Assert.That(metadata[0].Parameters[0].Type.ToString(),
                Is.EqualTo("UdonInt"));
        }
    }
}
