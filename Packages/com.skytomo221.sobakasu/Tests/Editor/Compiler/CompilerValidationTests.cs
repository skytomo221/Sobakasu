using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Diagnostic;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class CompilerValidationTests
    {
        [Test]
        public void ValidateDeclarations_BindsPrimitiveOperatorWithoutLoadingStandardLibrary()
        {
            var diagnostics = SobakasuCompiler.ValidateDeclarations(
                @"public implementation i32 = extern System.Int32 {
  public function +(self, rhs: Self) -> Self
    = extern self + rhs
}",
                SobakasuTestEnvironment.Default);

            Assert.That(diagnostics, Has.None.Matches<Diagnostic>(
                diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Test]
        public void ValidateDeclarations_ReportsBindingErrors()
        {
            var diagnostics = SobakasuCompiler.ValidateDeclarations(
                @"public implementation f32 = extern System.Single {
  public function invalid(self) -> f32
    = extern self.DoesNotExist()
}",
                SobakasuTestEnvironment.Default);

            Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(
                diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }
    }
}
