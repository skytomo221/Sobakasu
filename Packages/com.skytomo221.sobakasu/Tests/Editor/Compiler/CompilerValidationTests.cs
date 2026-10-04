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
                @"pub impl i32 = extern System.Int32 {
  pub fn +(self, rhs: Self) -> Self
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
                @"pub impl f32 = extern System.Single {
  pub fn invalid(self) -> f32
    = extern self.DoesNotExist()
}",
                SobakasuTestEnvironment.Default);

            Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(
                diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }
    }
}
