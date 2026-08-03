using System;
using System.IO;
using System.Text;
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
            Assert.That(resolution.Graph.Modules.Count, Is.EqualTo(2));
        }

        [Test]
        public void Resolver_RejectsMissingAndDuplicateLogicalModules()
        {
            WithTemporaryLibrary(
                @"{
  ""modules"": [
    { ""name"": ""sample.value"", ""path"": ""value.sobakasu"" },
    { ""name"": ""sample.value"", ""path"": ""other.sobakasu"" }
  ]
}",
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "value.sobakasu"),
                        "pub fn get -> i32 { 1 }");
                    var resolver = new StandardLibraryResolver();
                    var result = resolver.Resolve(
                        "use missing.module.value;",
                        root);

                    Assert.That(ContainsCode(result.Diagnostics, "SBK4005"), Is.True);
                });
        }

        [Test]
        public void Resolver_DetectsCyclicDependencies()
        {
            WithTemporaryLibrary(
                @"{
  ""modules"": [
    { ""name"": ""cycle.a"", ""path"": ""a.sobakasu"" },
    { ""name"": ""cycle.b"", ""path"": ""b.sobakasu"" }
  ]
}",
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "a.sobakasu"),
                        "use cycle.b.value; pub fn value -> i32 { 1 }");
                    File.WriteAllText(
                        Path.Combine(root, "b.sobakasu"),
                        "use cycle.a.value; pub fn value -> i32 { 2 }");
                    var result = new StandardLibraryResolver().Resolve(
                        "use cycle.a.value;",
                        root);

                    Assert.That(ContainsCode(result.Diagnostics, "SBK4006"), Is.True);
                });
        }

        [Test]
        public void Resolver_RejectsManifestPathEscape()
        {
            WithTemporaryLibrary(
                @"{
  ""modules"": [
    { ""name"": ""escape.value"", ""path"": ""../outside.sobakasu"" }
  ]
}",
                root =>
                {
                    var result = new StandardLibraryResolver().Resolve(
                        "use escape.value.get;",
                        root);
                    Assert.That(ContainsCode(result.Diagnostics, "SBK4015"), Is.True);
                });
        }

        [TestCase("let mut count = 0; pub fn value -> i32 { count }", "SBK4012")]
        [TestCase("pub let state = 1; pub fn value -> i32 { 0 }", "SBK4012")]
        [TestCase("sync let mut state = 0; pub fn value -> i32 { 0 }", "SBK4012")]
        [TestCase("on Interact {} pub fn value -> i32 { 0 }", "SBK4013")]
        public void Compiler_RejectsRuntimeStateAndEventsInLibrary(
            string moduleSource,
            string diagnosticCode)
        {
            WithTemporaryLibrary(
                SingleModuleManifest("check.module", "module.sobakasu"),
                root =>
                {
                    File.WriteAllText(Path.Combine(root, "module.sobakasu"), moduleSource);
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
            WithTemporaryLibrary(
                SingleModuleManifest("cache.module", "module.sobakasu"),
                root =>
                {
                    var sourcePath = Path.Combine(root, "module.sobakasu");
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
        public void Resolver_DoesNotKeepStaleManifestMappings()
        {
            WithTemporaryLibrary(
                SingleModuleManifest("old.module", "module.sobakasu"),
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "module.sobakasu"),
                        "pub fn value -> i32 { 1 }");
                    var resolver = new StandardLibraryResolver();
                    var first = resolver.Resolve("use old.module.value;", root);
                    Assert.That(first.Diagnostics.HasErrors, Is.False);

                    File.WriteAllText(
                        Path.Combine(root, StandardLibraryResolver.ManifestFileName),
                        SingleModuleManifest("updated.module", "module.sobakasu"),
                        new UTF8Encoding(false));
                    var second = resolver.Resolve("use updated.module.value;", root);
                    Assert.That(second.Diagnostics.HasErrors, Is.False);
                });
        }

        [Test]
        public void Compiler_ImportsPublicExternalBindingTypeAndPublicMethod()
        {
            WithTemporaryLibrary(
                SingleModuleManifest("sample.unity", "unity.sobakasu"),
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "unity.sobakasu"),
                        @"pub impl GameObject = extern UnityEngine.GameObject {
  pub fn set_active(active: bool) {
    extern self.SetActive(active);
  }
}");
                    var result = SobakasuCompiler.CompileToUasm(
                        @"use sample.unity.GameObject;
let target: GameObject = null;
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
                SingleModuleManifest("sample.numbers", "numbers.sobakasu"),
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "numbers.sobakasu"),
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
                SingleModuleManifest("private.module", "private.sobakasu"),
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "private.sobakasu"),
                        "fn hidden -> i32 { 1 }");
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
                SingleModuleManifest("broken.module", "broken.sobakasu"),
                root =>
                {
                    var sourcePath = Path.Combine(root, "broken.sobakasu");
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
                SingleModuleManifest("broken.lexer", "broken.sobakasu"),
                root =>
                {
                    var sourcePath = Path.Combine(root, "broken.sobakasu");
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
                SingleModuleManifest("private.unity", "unity.sobakasu"),
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "unity.sobakasu"),
                        @"pub impl GameObject = extern UnityEngine.GameObject {
  fn hidden { extern self.SetActive(false); }
}");
                    var result = SobakasuCompiler.CompileToUasm(
                        @"use private.unity.GameObject;
let target: GameObject = null;
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
                SingleModuleManifest("private.type", "type.sobakasu"),
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "type.sobakasu"),
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
                @"{
  ""modules"": [
    { ""name"": ""first.module"", ""path"": ""first.sobakasu"" },
    { ""name"": ""second.module"", ""path"": ""second.sobakasu"" }
  ]
}",
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "first.sobakasu"),
                        "pub fn value -> i32 { 1 }");
                    File.WriteAllText(
                        Path.Combine(root, "second.sobakasu"),
                        "pub fn value -> i32 { 2 }");

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
                });
        }

        [Test]
        public void Compiler_PreservesLocalShadowingOfImportedFunction()
        {
            WithTemporaryLibrary(
                SingleModuleManifest("shadow.module", "shadow.sobakasu"),
                root =>
                {
                    File.WriteAllText(
                        Path.Combine(root, "shadow.sobakasu"),
                        "pub fn value -> i32 { 1 }");
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
        public void Resolver_ReportsRootManifestAndUnregisteredModuleFailures()
        {
            var missingRoot = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-standard-library-tests",
                Guid.NewGuid().ToString("N"));
            var rootResult = new StandardLibraryResolver().Resolve(
                "use missing.module.value;",
                missingRoot);
            Assert.That(ContainsCode(rootResult.Diagnostics, "SBK4001"), Is.True);

            var missingManifestRoot = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-standard-library-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(missingManifestRoot);
            try
            {
                var missingManifest = new StandardLibraryResolver().Resolve(
                    "use missing.module.value;",
                    missingManifestRoot);
                Assert.That(ContainsCode(missingManifest.Diagnostics, "SBK4002"),
                    Is.True);
            }
            finally
            {
                Directory.Delete(missingManifestRoot, recursive: true);
            }

            WithTemporaryLibrary("not valid json", root =>
            {
                var invalidManifest = new StandardLibraryResolver().Resolve(
                    "use missing.module.value;",
                    root);
                Assert.That(ContainsCode(invalidManifest.Diagnostics, "SBK4003"),
                    Is.True);
            });

            WithTemporaryLibrary(@"{ ""modules"": [] }", root =>
            {
                File.WriteAllText(
                    Path.Combine(root, "unregistered.sobakasu"),
                    "pub fn value -> i32 { 1 }");
                var unregistered = new StandardLibraryResolver().Resolve(
                    "use unregistered.module.value;",
                    root);
                Assert.That(ContainsCode(unregistered.Diagnostics, "SBK4004"), Is.True);
                Assert.That(unregistered.Graph.Modules.Count, Is.EqualTo(1));
            });
        }

        private static string SingleModuleManifest(string name, string path)
        {
            return $@"{{
  ""modules"": [
    {{ ""name"": ""{name}"", ""path"": ""{path}"" }}
  ]
}}";
        }

        private static void WithTemporaryLibrary(string manifest, Action<string> action)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-standard-library-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(
                    Path.Combine(root, StandardLibraryResolver.ManifestFileName),
                    manifest,
                    new UTF8Encoding(false));
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
