using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class ImplExternProgramAssetIntegrationTests : SobakasuAssetCleanupFixture
    {
        [Test]
        public void UdonAssembler_AcceptsResolvedImplAndExternProgram()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"public implementation GameObject = extern UnityEngine.GameObject {
  public function set_name(self, value: string) { extern self.name = value; }
}

public implementation Vector3 = extern UnityEngine.Vector3 {
  public function new(x: f32, y: f32, z: f32) -> Self { extern new Self(x, y, z) }
  public function +(self, rhs: Self) -> Self { extern self + rhs }
  public function x(self) -> f32 { extern self.x }
  public function set_x(self, value: f32) { extern self.x = value; }
}

behavior {
on interact {
  let target = extern UnityEngine.GameObject.Find(""Sobakasu"");
  target.set_name(""Sobakasu"");
  let mutable value = Vector3::new(1.0f32, 2.0f32, 3.0f32);
  value.set_x(4.0f32);
  let sum = value + value;
  extern UnityEngine.Debug.Log(sum.x);
}
}",
                SobakasuUnityCompilationEnvironmentProvider.GetEnvironment());
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateImportedProgramAsset("SobakasuImplExternTests");
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
        }

    }
}
