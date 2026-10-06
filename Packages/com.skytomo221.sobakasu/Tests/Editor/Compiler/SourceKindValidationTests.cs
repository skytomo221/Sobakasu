using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Diagnostic;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class SourceKindValidationTests
    {
        [TestCase("Foo.sobakasu", SobakasuSourceKind.Script)]
        [TestCase("Foo.library.sobakasu", SobakasuSourceKind.Library)]
        [TestCase("Foo.editor.sobakasu", SobakasuSourceKind.Editor)]
        [TestCase("Foo.LIBRARY.SOBAKASU", SobakasuSourceKind.Library)]
        [TestCase("<entry>", SobakasuSourceKind.Script)]
        public void FromPath_ClassifiesMostSpecificSuffix(
            string path,
            SobakasuSourceKind expected)
        {
            Assert.That(SobakasuSourceKinds.FromPath(path), Is.EqualTo(expected));
        }

        [Test]
        public void LibrarySource_RejectsStateBlock()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"state {
  count: i32 = 0;
}",
                "Assets/Counter.library.sobakasu");

            Assert.That(result.Success, Is.False);
            AssertDiagnostic(result, "SBK2307", "Assets/Counter.library.sobakasu");
        }

        [Test]
        public void LibrarySource_RejectsBehavior()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"behavior {
  on interact(state) {
  }
}",
                "Assets/Counter.library.sobakasu");

            Assert.That(result.Success, Is.False);
            AssertDiagnostic(result, "SBK2308", "Assets/Counter.library.sobakasu");
        }

        [Test]
        public void ScriptSource_DoesNotApplyLibraryRestrictions()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"state {
  count: i32 = 0;
}

behavior {
  on interact(state) {
  }
}",
                "Assets/Counter.sobakasu");

            Assert.That(result.Diagnostics, Has.None.Matches<Diagnostic>(
                diagnostic => diagnostic.Code == "SBK2307" || diagnostic.Code == "SBK2308"));
        }

        [Test]
        public void LibrarySource_AllowsReusableDeclarations()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"struct Counter {
  value: i32,
}

function identity(value: i32) -> i32 {
  value
}",
                "Assets/Counter.library.sobakasu");

            Assert.That(result.Diagnostics, Has.None.Matches<Diagnostic>(
                diagnostic => diagnostic.Code == "SBK2307" || diagnostic.Code == "SBK2308"));
        }

        [Test]
        public void EditorSource_IsReservedAndUnsupported()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                string.Empty,
                "Assets/Counter.editor.sobakasu");

            Assert.That(result.Success, Is.False);
            AssertDiagnostic(result, "SBK5003", "Assets/Counter.editor.sobakasu");
        }

        private static void AssertDiagnostic(
            SobakasuCompiler.CompileResult result,
            string code,
            string sourcePath)
        {
            Assert.That(result.Diagnostics, Has.Some.Matches<Diagnostic>(diagnostic =>
                diagnostic.Code == code && diagnostic.SourcePath == sourcePath));
        }
    }
}
