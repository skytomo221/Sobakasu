using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuUseDirectiveTests
    {
        [Test]
        public void Parser_ParsesDottedSobakasuModulePathAndAlias()
        {
            var parser = new SobakasuParser(
                SourceText.From("use example.math.twice as double_value;"));
            var syntax = parser.ParseCompilationUnit();
            var use = syntax.Members[0] as UseDirectiveSyntax;

            Assert.That(use, Is.Not.Null);
            Assert.That(use.Path.GetText(), Is.EqualTo("example.math.twice"));
            Assert.That(use.Alias.Text, Is.EqualTo("double_value"));
            Assert.That(parser.Diagnostics.HasErrors, Is.False);
        }

        [Test]
        public void Parser_RejectsDoubleColonModulePath()
        {
            var parser = new SobakasuParser(
                SourceText.From("use example::math::twice;"));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics, "SBK1024"), Is.True);
        }

        [Test]
        public void Compiler_RejectsExternalApiUseWithoutExternFallback()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "use UnityEngine.Debug; on Interact {} ");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result, "SBK4011"), Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_DoesNotResolveBareExternalApiNames()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "on Interact { Debug.Log(\"no fallback\"); }");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result, "SBK2002"), Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_ResolvesExplicitExternStaticMethod()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "on Interact { extern UnityEngine.Debug.Log(\"hello\"); }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(
                result.Uasm,
                Does.Contain("UnityEngineDebug.__Log__SystemObject__SystemVoid"));
        }

        [Test]
        public void Compiler_CombinesDefaultStandardLibraryModuleIntoOneProgram()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use example.math.twice;
on Interact {
  extern UnityEngine.Debug.Log(twice(21));
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("op_Multip"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineDebug.__Log"));
            Assert.That(result.Uasm, Does.Not.Contain(".export twice"));
        }

        [Test]
        public void Compiler_ResolvesModuleAlias()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use example.math.twice as double_value;
on Interact {
  extern UnityEngine.Debug.Log(double_value(21));
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
        }

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

        [TestCase("state count = 0; pub fn value -> i32 { count }", "SBK4012")]
        [TestCase("pub state status = 1; pub fn value -> i32 { 0 }", "SBK4012")]
        [TestCase("sync state status = 0; pub fn value -> i32 { 0 }", "SBK4012")]
        [TestCase("on Interact {} pub fn value -> i32 { 0 }", "SBK4013")]
        public void Compiler_RejectsRuntimeStateAndEventsInLibrary(
            string moduleSource,
            string diagnosticCode)
        {
            WithTemporaryLibrary(root =>
            {
                WriteModule(root, "check.module", moduleSource);
                var result = SobakasuCompiler.CompileToUasm(
                    "use check.module.value; on Interact { value; }",
                    root);
                Assert.That(result.Success, Is.False);
                Assert.That(ContainsCode(result, diagnosticCode), Is.True, result.ErrorText);
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
        public void Compiler_ImportsPublicExternalBindingTypeAndPublicMethod()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(
                        root,
                        "sample.unity",
                        @"pub impl GameObject = extern UnityEngine.GameObject {
  pub fn set_active(active: bool) {
    extern self.SetActive(active);
  }
}");
                    var result = SobakasuCompiler.CompileToUasm(
                        @"use sample.unity.GameObject;
state target: GameObject = null;
on Interact {
  target.set_active(true);
}",
                        root);

                    Assert.That(result.Success, Is.True, result.ErrorText);
                    Assert.That(result.Uasm,
                        Does.Contain("UnityEngineGameObject.__SetActive"));
                });
        }

        [Test]
        public void Compiler_AllowsNormalImplInStandardLibraryModule()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(
                        root,
                        "sample.numbers",
                        @"impl i32 {
  pub fn triple -> i32 { self * 3 }
}

pub fn apply(value: i32) -> i32 {
  value.triple
}");
                    var result = SobakasuCompiler.CompileToUasm(
                        @"use sample.numbers.apply;
on Interact {
  extern UnityEngine.Debug.Log(apply(14));
}",
                        root);

                    Assert.That(result.Success, Is.True, result.ErrorText);
                    Assert.That(result.Uasm, Does.Contain("op_Multiplication"));
                });
        }

        [Test]
        public void Compiler_RejectsPrivateImportedFunction()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "private.module", "fn hidden -> i32 { 1 }");
                    var result = SobakasuCompiler.CompileToUasm(
                        "use private.module.hidden; on Interact {}",
                        root);

                    Assert.That(result.Success, Is.False);
                    Assert.That(ContainsCode(result, "SBK4007"), Is.True,
                        result.ErrorText);
                });
        }

        [Test]
        public void Compiler_PreservesStandardLibraryDiagnosticSourcePath()
        {
            WithTemporaryLibrary(
                root =>
                {
                    var sourcePath = GetModulePath(root, "broken.module");
                    Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
                    File.WriteAllText(sourcePath, "pub fn broken -> i32 {}");
                    var result = SobakasuCompiler.CompileToUasm(
                        "use broken.module.broken; on Interact {}",
                        root);

                    Assert.That(result.Success, Is.False);
                    Assert.That(result.ErrorText, Does.Contain(sourcePath));
                });
        }

        [Test]
        public void Compiler_PreservesStandardLibraryLexerDiagnosticSourcePath()
        {
            WithTemporaryLibrary(
                root =>
                {
                    var sourcePath = GetModulePath(root, "broken.lexer");
                    Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
                    File.WriteAllText(sourcePath, "pub fn broken -> i32 { ` }");
                    var result = SobakasuCompiler.CompileToUasm(
                        "use broken.lexer.broken; on Interact {}",
                        root);

                    Assert.That(result.Success, Is.False);
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        if (diagnostic.Code == "SBK0001")
                        {
                            Assert.That(diagnostic.SourcePath, Is.EqualTo(sourcePath));
                            return;
                        }
                    }

                    Assert.Fail("Expected SBK0001 lexer diagnostic.");
                });
        }

        [Test]
        public void Compiler_RejectsPrivateMethodOnImportedType()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(
                        root,
                        "private.unity",
                        @"pub impl GameObject = extern UnityEngine.GameObject {
  fn hidden { extern self.SetActive(false); }
}");
                    var result = SobakasuCompiler.CompileToUasm(
                        @"use private.unity.GameObject;
state target: GameObject = null;
on Interact { target.hidden; }
",
                        root);

                    Assert.That(result.Success, Is.False);
                    Assert.That(ContainsCode(result, "SBK4007"), Is.True,
                        result.ErrorText);
                });
        }

        [Test]
        public void Compiler_RejectsPrivateImportedType()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(
                        root,
                        "private.type",
                        "impl GameObject = extern UnityEngine.GameObject {}");
                    var result = SobakasuCompiler.CompileToUasm(
                        "use private.type.GameObject; on Interact {}",
                        root);

                    Assert.That(result.Success, Is.False);
                    Assert.That(ContainsCode(result, "SBK4007"), Is.True,
                        result.ErrorText);
                });
        }

        [Test]
        public void Compiler_DetectsDuplicateAliasAndAmbiguousImportedName()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "first.module", "pub fn value -> i32 { 1 }");
                    WriteModule(root, "second.module", "pub fn value -> i32 { 2 }");

                    var duplicateAlias = SobakasuCompiler.CompileToUasm(
                        @"use first.module.value as selected;
use second.module.value as selected;
on Interact {}",
                        root);
                    Assert.That(ContainsCode(duplicateAlias, "SBK4008"), Is.True,
                        duplicateAlias.ErrorText);

                    var ambiguousName = SobakasuCompiler.CompileToUasm(
                        @"use first.module.value;
use second.module.value;
on Interact {}",
                        root);
                    Assert.That(ContainsCode(ambiguousName, "SBK4009"), Is.True,
                        ambiguousName.ErrorText);

                    var aliasWins = SobakasuCompiler.CompileToUasm(
                        @"use first.module.value;
use second.module.value as value;
on Interact { value(); }",
                        root);
                    Assert.That(aliasWins.Success, Is.True, aliasWins.ErrorText);

                    var aliasWinsRegardlessOfOrder = SobakasuCompiler.CompileToUasm(
                        @"use second.module.value as value;
use first.module.value;
on Interact { value(); }",
                        root);
                    Assert.That(
                        aliasWinsRegardlessOfOrder.Success,
                        Is.True,
                        aliasWinsRegardlessOfOrder.ErrorText);
                });
        }

        [Test]
        public void Compiler_ImportsQualifiesReExportsAndPreludesPublicConstants()
        {
            WithTemporaryLibrary(root =>
            {
                WriteModule(root, "values", @"pub const BASE = 20;
pub const DOUBLE = BASE * 2;
const PRIVATE = 1;");
                WriteModule(root, "api", "pub use values.DOUBLE;");
                WriteModule(root, "prelude", "pub use values.BASE;");

                var imported = SobakasuCompiler.CompileToUasm(
                    @"use values.DOUBLE;
state result = DOUBLE;
on Interact { extern UnityEngine.Debug.Log(DOUBLE); }",
                    root);
                Assert.That(imported.Success, Is.True, imported.ErrorText);

                var qualified = SobakasuCompiler.CompileToUasm(
                    @"use values;
on Interact { extern UnityEngine.Debug.Log(values.DOUBLE); }",
                    root);
                Assert.That(qualified.Success, Is.True, qualified.ErrorText);

                var reExported = SobakasuCompiler.CompileToUasm(
                    @"use api.DOUBLE;
on Interact { extern UnityEngine.Debug.Log(DOUBLE); }",
                    root);
                Assert.That(reExported.Success, Is.True, reExported.ErrorText);

                var prelude = SobakasuCompiler.CompileToUasm(
                    "on Interact { extern UnityEngine.Debug.Log(BASE); }",
                    root);
                Assert.That(prelude.Success, Is.True, prelude.ErrorText);

                var privateConstant = SobakasuCompiler.CompileToUasm(
                    "use values.PRIVATE; on Interact {}",
                    root);
                Assert.That(privateConstant.Success, Is.False);
                Assert.That(ContainsCode(privateConstant, "SBK4007"), Is.True,
                    privateConstant.ErrorText);
            });
        }

        [Test]
        public void Compiler_UsesPublicConstantsFromStandardLibraryMath()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use math.TAU;
on Interact { extern UnityEngine.Debug.Log(TAU); }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain(".export PI"));
            Assert.That(result.Uasm, Does.Not.Contain(".export TAU"));
        }

        [Test]
        public void Compiler_PreservesLocalShadowingOfImportedFunction()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "shadow.module", "pub fn value -> i32 { 1 }");
                    var result = SobakasuCompiler.CompileToUasm(
                        @"use shadow.module.value;
on Interact {
  let value = 42;
  extern UnityEngine.Debug.Log(value);
}",
                        root);

                    Assert.That(result.Success, Is.True, result.ErrorText);
                });
        }

        [Test]
        public void Compiler_PrefersCurrentChildModuleOverExplicitAlias()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "api", @"mod child;
use other.child as child;
pub fn run { child.call(); }");
                    WriteModule(root, "api.child", "pub fn call -> i32 { 1 }");
                    WriteModule(root, "other", "pub fn child -> i32 { 2 }");

                    var result = SobakasuCompiler.CompileToUasm(
                        "use api.run; on Interact { run(); }",
                        root);
                    Assert.That(result.Success, Is.True, result.ErrorText);
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
        public void Parser_ParsesModPubModAndPubUse()
        {
            var parser = new SobakasuParser(SourceText.From(
                "mod private_child; pub mod public_child; pub use private_child.value;"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(syntax.Members[0], Is.TypeOf<ModDeclarationSyntax>());
            Assert.That(((ModDeclarationSyntax)syntax.Members[0]).IsPublic, Is.False);
            Assert.That(((ModDeclarationSyntax)syntax.Members[1]).IsPublic, Is.True);
            Assert.That(((UseDirectiveSyntax)syntax.Members[2]).IsReExport, Is.True);
            Assert.That(parser.Diagnostics.HasErrors, Is.False);
        }

        [Test]
        public void Parser_ReportsMalformedAndNestedModAndRecovers()
        {
            var malformed = new SobakasuParser(SourceText.From(
                "mod missing pub fn after -> i32 { 1 }"));
            var malformedSyntax = malformed.ParseCompilationUnit();
            Assert.That(ContainsCode(malformed.Diagnostics, "SBK1025"), Is.True);
            Assert.That(malformedSyntax.Members.Count, Is.GreaterThan(1));

            var nested = new SobakasuParser(SourceText.From(
                "fn run { mod child; pub mod public_child; } pub fn after -> i32 { 1 }"));
            nested.ParseCompilationUnit();
            Assert.That(
                nested.Diagnostics.Diagnostics.Count(
                    diagnostic => diagnostic.Code == "SBK1026"),
                Is.EqualTo(2));
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
                    Assert.That(
                        result.Graph.FindModule("api.child").SourcePath,
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
        public void Compiler_UsesPreludeModuleAndParentReExportWithoutUse()
        {
            WithTemporaryLibrary(root =>
            {
                WriteHierarchy(root, includePrelude: true);
                var result = SobakasuCompiler.CompileToUasm(
                    @"state target: api.GameObject = null;
on Interact {
  extern UnityEngine.Debug.Log(api.twice(21));
}",
                    root);

                Assert.That(result.Success, Is.True, result.ErrorText);
                Assert.That(result.Uasm, Does.Contain("op_Multiplication"));
            });
        }

        [Test]
        public void Compiler_SeparatesPrivateAndPublicChildPaths()
        {
            WithTemporaryLibrary(root =>
            {
                WriteHierarchy(root, includePrelude: false);

                var privatePath = SobakasuCompiler.CompileToUasm(
                    "use api.private_child.twice; on Interact {}",
                    root);
                Assert.That(privatePath.Success, Is.False);
                Assert.That(ContainsCode(privatePath, "SBK4021"), Is.True,
                    privatePath.ErrorText);

                var publicPath = SobakasuCompiler.CompileToUasm(
                    @"use api;
on Interact { extern UnityEngine.Debug.Log(api.public_child.identity(7)); }",
                    root);
                Assert.That(publicPath.Success, Is.True, publicPath.ErrorText);

                var canonicalPath = SobakasuCompiler.CompileToUasm(
                    @"use api;
on Interact { extern UnityEngine.Debug.Log(api.twice(7)); }",
                    root);
                Assert.That(canonicalPath.Success, Is.True, canonicalPath.ErrorText);
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
                        "use api.child.value; on Interact {}",
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
        public void Binder_PreservesDeclarationIdentityAndCanonicalPublicPath()
        {
            WithTemporaryLibrary(root =>
            {
                WriteHierarchy(root, includePrelude: false);
                var resolution = new StandardLibraryResolver().Resolve("use api.twice;", root);
                var binder = new Skytomo221.Sobakasu.Compiler.Binder.SobakasuBinder();
                binder.BindProgram(resolution.Graph);

                Assert.That(resolution.Diagnostics.HasErrors, Is.False);
                Assert.That(binder.Diagnostics.HasErrors, Is.False);
                var api = resolution.Graph.FindModule("api");
                var child = resolution.Graph.FindModule("api.private_child");
                var fromParent = binder.ModuleSymbols[api].LookupExport("twice");
                var fromChild = binder.ModuleSymbols[child].LookupExport("twice");
                Assert.That(fromParent, Is.SameAs(fromChild));
                var function = (Skytomo221.Sobakasu.Compiler.Binder.FunctionSymbol)fromParent;
                Assert.That(function.DeclarationIdentity,
                    Is.EqualTo("api.private_child.twice"));
                Assert.That(function.CanonicalPublicPath, Is.EqualTo("api.twice"));
            });
        }

        [Test]
        public void Compiler_DiagnosesInvalidAndAmbiguousReExports()
        {
            WithTemporaryLibrary(
                root =>
                {
                    WriteModule(root, "api.first", "pub fn value -> i32 { 1 } fn hidden -> i32 { 0 }");
                    WriteModule(root, "api.second", "pub fn value -> i32 { 2 }");

                    WriteModule(root, "api", @"mod first; mod second;
pub use first.hidden;
pub use first.missing;
pub use first.value as selected;
pub use second.value as selected;");
                    var result = SobakasuCompiler.CompileToUasm("use api; on Interact {}", root);
                    Assert.That(ContainsCode(result, "SBK4007"), Is.True, result.ErrorText);
                    Assert.That(ContainsCode(result, "SBK4010"), Is.True, result.ErrorText);
                    Assert.That(ContainsCode(result, "SBK4024"), Is.True, result.ErrorText);
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
                        "on Interact { value(); }",
                        root);
                    Assert.That(
                        implicitDeclaration.Success,
                        Is.True,
                        implicitDeclaration.ErrorText);

                    var shadow = SobakasuCompiler.CompileToUasm(
                        "fn value -> i32 { 2 } on Interact { value(); }",
                        root);
                    Assert.That(shadow.Success, Is.True, shadow.ErrorText);

                    var explicitImport = SobakasuCompiler.CompileToUasm(
                        "use explicit_values.value; on Interact { value(); }",
                        root);
                    Assert.That(
                        explicitImport.Success,
                        Is.True,
                        explicitImport.ErrorText);

                    var standardLibrary = SobakasuCompiler.CompileToUasm(
                        "use consumer.run; on Interact { run(); }",
                        root);
                    Assert.That(standardLibrary.Success, Is.False);
                    Assert.That(ContainsCode(standardLibrary, "SBK2002"), Is.True,
                        standardLibrary.ErrorText);
                });
        }

        [Test]
        public void Compiler_DistinguishesModuleMembersFromValueMembers()
        {
            WithTemporaryLibrary(root =>
            {
                WriteHierarchy(root, includePrelude: false);
                WriteModule(root, "api.public_child", @"pub fn identity(value: i32) -> i32 { value }
fn hidden -> i32 { 0 }");

                var privateFunction = SobakasuCompiler.CompileToUasm(
                    "use api; on Interact { api.public_child.hidden(); }",
                    root);
                Assert.That(ContainsCode(privateFunction, "SBK4025"), Is.True,
                    privateFunction.ErrorText);

                var missingMember = SobakasuCompiler.CompileToUasm(
                    "use api; on Interact { api.public_child.missing(); }",
                    root);
                Assert.That(missingMember.Success, Is.False);
                Assert.That(ContainsCode(missingMember, "SBK2003"), Is.True,
                    missingMember.ErrorText);

                var bothMemberKinds = SobakasuCompiler.CompileToUasm(
                    @"use api;
impl i32 { fn choose(rhs: i64) -> i64 { rhs } }
on Interact {
  api.public_child.identity(7);
  let receiver: i32 = 1;
  receiver.choose(2);
}",
                    root);
                Assert.That(bothMemberKinds.Success, Is.True, bothMemberKinds.ErrorText);
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
                    Assert.That(ContainsCode(result.Diagnostics, "SBK4006"), Is.True);
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
                    var result = new StandardLibraryResolver().Resolve(string.Empty, root);
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

        private static void WriteHierarchy(string root, bool includePrelude)
        {
            if (includePrelude)
                WriteModule(root, "prelude", "pub use api;");
            WriteModule(root, "api", @"mod private_child;
pub mod public_child;
pub use private_child.twice;
pub use private_child.GameObject;");
            WriteModule(root, "api.private_child", @"pub fn twice(value: i32) -> i32 { value * 2 }
pub impl GameObject = extern UnityEngine.GameObject {}");
            WriteModule(root, "api.public_child",
                "pub fn identity(value: i32) -> i32 { value }");
        }

        private static string GetModulePath(string root, string logicalName)
        {
            return Path.Combine(
                root,
                logicalName.Replace('.', Path.DirectorySeparatorChar) + ".sobakasu");
        }

        private static void WriteModule(string root, string logicalName, string source)
        {
            var path = GetModulePath(root, logicalName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, source);
        }

        private static void WithTemporaryLibrary(Action<string> action)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-standard-library-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                action(root);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        private static bool ContainsCode(
            Skytomo221.Sobakasu.Compiler.Diagnostic.DiagnosticBag diagnostics,
            string code)
        {
            foreach (var diagnostic in diagnostics.Diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }

        private static bool ContainsCode(
            SobakasuCompiler.CompileResult result,
            string code)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }
    }
}
