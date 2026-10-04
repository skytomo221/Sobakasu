using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class LocalVariableProgramAssetIntegrationTests : SobakasuAssetCleanupFixture
    {
        [Test]
        public void SetUasmAndAssemble_SucceedsForLocalDeclarationAssignmentAndRead()
        {
            Assemble(@"on interact() {
  let mut x = 1;
  x = 2;
  extern UnityEngine.Debug.Log(x);
}");
        }

        [Test]
        public void SetUasmAndAssemble_SucceedsForExternCallInitializerAndRead()
        {
            Assemble(@"
on interact() {
  let x = extern UnityEngine.Mathf.Sqrt(2.0f32);
  extern UnityEngine.Debug.Log(x);
}");
        }

        [Test]
        public void SetUasmAndAssemble_SucceedsForCompoundAssignmentAndShortCircuitOperators()
        {
            Assemble(@"
on interact() {
  let mut x = 1;
  x += 1;
  x <<= 1;
  let a = false;
  let b = a || ((extern UnityEngine.Mathf.Sqrt(1.0f32)) > 0.0f32);
}");
        }

        private void Assemble(string source)
        {
            var result = SobakasuCompiler.CompileToUasm(
                source,
                SobakasuUnityCompilationEnvironmentProvider.GetEnvironment());
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateImportedProgramAsset("SobakasuLocalVariableTests");
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
        }
    }
}
