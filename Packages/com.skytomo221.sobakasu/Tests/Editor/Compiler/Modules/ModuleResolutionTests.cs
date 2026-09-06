using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

using static Skytomo221.Sobakasu.Tests.Editor.ModuleTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class ModuleResolutionTests
    {

        [Test]
        public void Resolver_LoadsTheSameModuleOnlyOnce()
        {
            var resolver = new StandardLibraryResolver();
            var resolution = resolver.Resolve(
                @"use example.math.twice;
use example.math.twice as twice_again;",
                StandardLibraryResolver.DefaultRoot);

            Assert.That(resolution.Diagnostics.HasErrors, Is.False);
            var mathCount = 0;
            foreach (var module in resolution.Graph.Modules)
            {
                if (module.LogicalName == "example.math")
                    mathCount++;
            }
            Assert.That(mathCount, Is.EqualTo(1));
        }

        [Test]
        public void Resolver_DoesNotExpandAllChildrenOfImportedModuleAncestors()
        {
            var resolution = new StandardLibraryResolver().Resolve(
                "use math; on start { math.sin(0.0); }",
                StandardLibraryResolver.DefaultRoot);

            Assert.That(resolution.Diagnostics.HasErrors, Is.False);
            Assert.That(resolution.Graph.FindModule("system"), Is.Not.Null);
            Assert.That(resolution.Graph.FindModule("system.math"), Is.Not.Null);
            Assert.That(resolution.Graph.FindModule("unity"), Is.Not.Null);
            Assert.That(resolution.Graph.FindModule("unity.mathf"), Is.Not.Null);
            Assert.That(resolution.Graph.FindModule("unity.audio_source"), Is.Null);
            Assert.That(resolution.Graph.FindModule("unity.camera_binding"), Is.Null);
            Assert.That(resolution.Graph.FindModule("unity.game_object"), Is.Null);
            Assert.That(resolution.Graph.FindModule("unity.vector3_binding"), Is.Null);
            Assert.That(
                resolution.Graph.Modules.Count,
                Is.LessThan(50),
                string.Join(", ", resolution.Graph.Modules
                    .GroupBy(module => module.LogicalName.Split('.')[0])
                    .Select(group => $"{group.Key}:{group.Count()}")));
        }

        [Test]
        public void Resolver_UsesConventionAndReportsMissingModules()
        {
            WithTemporaryLibrary(root =>
            {
                WriteModule(root, "sample.value", "pub fn get -> i32 { 1 }");
                var resolver = new StandardLibraryResolver();
                var found = resolver.Resolve("use sample.value.get;", root);
                Assert.That(found.Diagnostics.HasErrors, Is.False);
                Assert.That(
                    found.Graph.FindModule("sample.value").SourcePath,
                    Is.EqualTo(GetModulePath(root, "sample.value")));

                var missing = resolver.Resolve("use missing.module.value;", root);
                Assert.That(ContainsCode(missing.Diagnostics, "SBK4004"), Is.True);
            });
        }

        [Test]
        public void Resolver_DetectsCyclicDependencies()
        {
            WithTemporaryLibrary(root =>
            {
                WriteModule(
                    root,
                    "cycle.a",
                    "use cycle.b.value; pub fn value -> i32 { 1 }");
                WriteModule(
                    root,
                    "cycle.b",
                    "use cycle.a.value; pub fn value -> i32 { 2 }");
                var result = new StandardLibraryResolver().Resolve(
                    "use cycle.a.value;",
                    root);

                Assert.That(ContainsCode(result.Diagnostics, "SBK4006"), Is.True);
            });
        }

        [Test]
        public void Resolver_DoesNotUseFilesOutsideTheConventionPath()
        {
            WithTemporaryLibrary(root =>
            {
                var misplacedPath = Path.Combine(root, "other", "location.sobakasu");
                Directory.CreateDirectory(Path.GetDirectoryName(misplacedPath));
                File.WriteAllText(misplacedPath, "pub fn get -> i32 { 1 }");

                var result = new StandardLibraryResolver().Resolve(
                    "use escape.value.get;",
                    root);
                Assert.That(ContainsCode(result.Diagnostics, "SBK4004"), Is.True);
                Assert.That(result.Graph.FindModule("escape.value"), Is.Null);
            });
        }

        [Test]
        public void Resolver_InvalidatesParsedSourceCacheWhenSourceChanges()
        {
            WithTemporaryLibrary(root =>
            {
                var sourcePath = GetModulePath(root, "cache.module");
                Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
                File.WriteAllText(sourcePath, "pub fn value -> i32 { 1 }");
                var resolver = new StandardLibraryResolver();
                var first = resolver.Resolve("use cache.module.value;", root);
                Assert.That(first.Diagnostics.HasErrors, Is.False);

                File.WriteAllText(sourcePath, "pub fn value -> i32 { }");
                var second = resolver.Resolve("use cache.module.value;", root);
                var binder = new Skytomo221.Sobakasu.Compiler.Binder.SobakasuBinder();
                binder.BindProgram(second.Graph);
                Assert.That(binder.Diagnostics.HasErrors, Is.True);
            });
        }

        [Test]
        public void Resolver_RecomputesConventionPathsBetweenResolutions()
        {
            WithTemporaryLibrary(root =>
            {
                WriteModule(root, "old.module", "pub fn value -> i32 { 1 }");
                var resolver = new StandardLibraryResolver();
                var first = resolver.Resolve("use old.module.value;", root);
                Assert.That(first.Diagnostics.HasErrors, Is.False);

                var oldPath = GetModulePath(root, "old.module");
                var updatedPath = GetModulePath(root, "updated.module");
                Directory.CreateDirectory(Path.GetDirectoryName(updatedPath));
                File.Move(oldPath, updatedPath);

                var stale = resolver.Resolve("use old.module.value;", root);
                Assert.That(ContainsCode(stale.Diagnostics, "SBK4004"), Is.True);
                var updated = resolver.Resolve("use updated.module.value;", root);
                Assert.That(updated.Diagnostics.HasErrors, Is.False);
            });
        }

        [Test]
        public void Resolver_ReportsMissingRootAndUsesConventionFilesWithoutConfiguration()
        {
            var missingRoot = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-standard-library-tests",
                Guid.NewGuid().ToString("N"));
            var rootResult = new StandardLibraryResolver().Resolve(
                "use missing.module.value;",
                missingRoot);
            Assert.That(ContainsCode(rootResult.Diagnostics, "SBK4001"), Is.True);

            WithTemporaryLibrary(root =>
            {
                var empty = new StandardLibraryResolver().Resolve(string.Empty, root);
                Assert.That(empty.Diagnostics.HasErrors, Is.False);
                Assert.That(empty.Graph.PreludeModule, Is.Null);

                WriteModule(root, "unregistered", "pub fn value -> i32 { 1 }");
                var discovered = new StandardLibraryResolver().Resolve(
                    "use unregistered.value;",
                    root);
                Assert.That(discovered.Diagnostics.HasErrors, Is.False);
                Assert.That(discovered.Graph.FindModule("unregistered"), Is.Not.Null);

                var missing = new StandardLibraryResolver().Resolve(
                    "use unregistered.module.value;",
                    root);
                Assert.That(ContainsCode(missing.Diagnostics, "SBK4004"), Is.True);
            });
        }

        [Test]
        public void Resolver_IgnoresLegacyManifestFile()
        {
            WithTemporaryLibrary(root =>
            {
                File.WriteAllText(Path.Combine(root, "manifest.json"), "not valid json");
                WriteModule(root, "legacy", "pub fn value -> i32 { 1 }");

                var result = new StandardLibraryResolver().Resolve(
                    "use legacy.value;",
                    root);
                Assert.That(result.Diagnostics.HasErrors, Is.False);
                Assert.That(result.Graph.FindModule("legacy"), Is.Not.Null);
            });
        }

        [Test]
        public void Resolver_UsesBuiltInPreludePathWhenPresent()
        {
            WithTemporaryLibrary(root =>
            {
                WriteModule(root, "prelude", "pub fn value -> i32 { 1 }");
                var result = new StandardLibraryResolver().Resolve(string.Empty, root);
                Assert.That(result.Diagnostics.HasErrors, Is.False);
                Assert.That(result.Graph.PreludeModule.LogicalName, Is.EqualTo("prelude"));
                Assert.That(
                    result.Graph.PreludeModule.SourcePath,
                    Is.EqualTo(Path.Combine(root, "prelude.sobakasu")));
            });

            WithTemporaryLibrary(root =>
            {
                WriteModule(root, "not_prelude", "pub fn value -> i32 { 1 }");
                var result = new StandardLibraryResolver().Resolve(string.Empty, root);
                Assert.That(result.Diagnostics.HasErrors, Is.False);
                Assert.That(result.Graph.PreludeModule, Is.Null);
            });
        }

        [Test]
        public void Resolver_MapsLogicalNamesOnlyToConventionPaths()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "api", "pub mod child;");
                    WriteModule(root, "api.child", "pub fn value -> i32 { 1 }");
                    var result = new StandardLibraryResolver().Resolve("use api;", root);
                    Assert.That(result.Diagnostics.HasErrors, Is.False);
                    Assert.That(result.Graph.FindModule("api.child"), Is.Null);

                    var referenced = new StandardLibraryResolver().Resolve(
                        "use api; on interact { api.child.value(); }",
                        root);
                    Assert.That(referenced.Diagnostics.HasErrors, Is.False);
                    Assert.That(
                        referenced.Graph.FindModule("api.child").SourcePath,
                        Is.EqualTo(GetModulePath(root, "api.child")));
                });

            WithTemporaryLibrary(
                root =>
                {
                    var wrongPath = Path.Combine(root, "other", "location.sobakasu");
                    Directory.CreateDirectory(Path.GetDirectoryName(wrongPath));
                    File.WriteAllText(wrongPath, "pub fn value -> i32 { 1 }");
                    var result = new StandardLibraryResolver().Resolve(
                        "use api.child.value;",
                        root);
                    Assert.That(ContainsCode(result.Diagnostics, "SBK4004"), Is.True);
                    Assert.That(result.Graph.FindModule("api.child"), Is.Null);
                });
        }

        [Test]
        public void Resolver_MaterializesOnlyTheReferencedReExportTarget()
        {
            WithTemporaryLibrary(root =>
            {
                WriteModule(root, "api", @"mod used;
mod unused;
pub use used.value;
pub use unused.other;");
                WriteModule(root, "api.used", "pub fn value -> i32 { 1 }");
                WriteModule(root, "api.unused", "pub fn other -> i32 { 2 }");

                var broad = new StandardLibraryResolver().Resolve(
                    "use api; on interact {}",
                    root);
                Assert.That(broad.Diagnostics.HasErrors, Is.False);
                Assert.That(broad.Graph.FindModule("api"), Is.Not.Null);
                Assert.That(broad.Graph.FindModule("api.used"), Is.Null);
                Assert.That(broad.Graph.FindModule("api.unused"), Is.Null);

                var referenced = new StandardLibraryResolver().Resolve(
                    "use api; on interact { api.value(); }",
                    root);
                Assert.That(referenced.Diagnostics.HasErrors, Is.False);
                Assert.That(referenced.Graph.FindModule("api.used"), Is.Not.Null);
                Assert.That(referenced.Graph.FindModule("api.unused"), Is.Null);
            });
        }

        [Test]
        public void Resolver_RequiresParentModAndDiagnosesDuplicateAndMissingChildren()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "api", "pub fn root -> i32 { 1 }");
                    WriteModule(root, "api.child", "pub fn value -> i32 { 2 }");
                    var unconnected = SobakasuCompiler.CompileToUasm(
                        "use api.child.value; on interact {}",
                        root);
                    Assert.That(ContainsCode(unconnected, "SBK4022"), Is.True,
                        unconnected.ErrorText);

                    WriteModule(root, "api", "mod child; pub mod child; mod missing;");
                    var invalid = new StandardLibraryResolver().Resolve("use api;", root);
                    Assert.That(ContainsCode(invalid.Diagnostics, "SBK4018"), Is.True);
                    Assert.That(ContainsCode(invalid.Diagnostics, "SBK4017"), Is.True);
                });
        }

        [Test]
        public void Prelude_IsWeakAndIsNotInjectedIntoStandardLibraryModules()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "prelude", "pub use helpers.value;");
                    WriteModule(root, "helpers", "pub fn value -> i32 { 1 }");
                    WriteModule(root, "explicit_values", "pub fn value -> i32 { 2 }");
                    WriteModule(root, "consumer", "pub fn run -> i32 { value() }");

                    var implicitDeclaration = SobakasuCompiler.CompileToUasm(
                        "on interact { value(); }",
                        root);
                    Assert.That(
                        implicitDeclaration.Success,
                        Is.True,
                        implicitDeclaration.ErrorText);

                    var shadow = SobakasuCompiler.CompileToUasm(
                        "fn value -> i32 { 2 } on interact { value(); }",
                        root);
                    Assert.That(shadow.Success, Is.True, shadow.ErrorText);

                    var explicitImport = SobakasuCompiler.CompileToUasm(
                        "use explicit_values.value; on interact { value(); }",
                        root);
                    Assert.That(
                        explicitImport.Success,
                        Is.True,
                        explicitImport.ErrorText);

                    var standardLibrary = SobakasuCompiler.CompileToUasm(
                        "use consumer.run; on interact { run(); }",
                        root);
                    Assert.That(standardLibrary.Success, Is.False);
                    Assert.That(ContainsCode(standardLibrary, "SBK2002"), Is.True,
                        standardLibrary.ErrorText);
                });
        }

        [Test]
        public void Resolver_DoesNotTurnMissingPreludeReExportIntoSelfCycle()
        {
            WithTemporaryLibrary(root =>
            {
                WriteModule(root, "prelude", "pub use missing;");

                var result = new StandardLibraryResolver().Resolve(string.Empty, root);

                Assert.That(ContainsCode(result.Diagnostics, "SBK4004"), Is.True);
                Assert.That(ContainsCode(result.Diagnostics, "SBK4006"), Is.False);
            });
        }

        [Test]
        public void Resolver_DiagnosesModuleReExportPreludeCyclesAndAllowsDiamond()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "api", "mod child;");
                    WriteModule(root, "api.child", "use api;");
                    var result = new StandardLibraryResolver().Resolve("use api;", root);
                    Assert.That(ContainsCode(result.Diagnostics, "SBK4006"), Is.False);
                    Assert.That(result.Graph.FindModule("api.child"), Is.Null);
                });

            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "first", "pub use second.value; pub fn value -> i32 { 1 }");
                    WriteModule(root, "second", "pub use first.value; pub fn value -> i32 { 2 }");
                    var result = new StandardLibraryResolver().Resolve("use first.value;", root);
                    Assert.That(ContainsCode(result.Diagnostics, "SBK4006"), Is.True);
                });

            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "prelude", "pub use api.value;");
                    WriteModule(root, "api", "use prelude.value; pub fn value -> i32 { 1 }");
                    var result = new StandardLibraryResolver().Resolve(
                        "on interact { value(); }",
                        root);
                    Assert.That(ContainsCode(result.Diagnostics, "SBK4006"), Is.True);
                });

            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "root", "use left; use right;");
                    WriteModule(root, "left", "use leaf.value;");
                    WriteModule(root, "right", "use leaf.value;");
                    WriteModule(root, "leaf", "pub fn value -> i32 { 1 }");
                    var result = new StandardLibraryResolver().Resolve("use root;", root);
                    Assert.That(result.Diagnostics.HasErrors, Is.False);
                    Assert.That(
                        result.Graph.Modules.Count(module => module.LogicalName == "leaf"),
                        Is.EqualTo(1));
                });
        }
    }
}
