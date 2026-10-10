using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class StandardLibraryGeneratorTests
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        private string _testRoot;
        private string _repositoryRoot;
        private string _packageRoot;
        private string _additions;
        private string _externalRoot;

        [SetUp]
        public void SetUp()
        {
            _testRoot = Path.Combine(
                Path.GetTempPath(),
                $"SobakasuStandardLibraryGeneratorTests_{Guid.NewGuid():N}");
            _repositoryRoot = Path.Combine(_testRoot, "repository");
            _packageRoot = Path.Combine(
                _repositoryRoot,
                "Packages",
                StandardLibraryGenerator.PackageName);
            _additions = Path.Combine(
                _packageRoot,
                StandardLibraryGenerator.AdditionsDirectoryName);
            _externalRoot = Path.Combine(_testRoot, "external");
            Directory.CreateDirectory(_additions);
            Directory.CreateDirectory(_externalRoot);
            WriteText(
                Path.Combine(_packageRoot, "package.json"),
                $"{{\"name\":\"{StandardLibraryGenerator.PackageName}\"}}\n");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, true);
        }

        [Test]
        public void GeneratedOnly_IsWrittenToTheOutput()
        {
            var output = ExternalPath("generated-only");
            CreateGenerator(new Dictionary<string, string>
            {
                ["foo.library.sobakasu"] = "generated\n"
            }).GenerateToDirectory(output, _additions);

            Assert.That(ReadText(Path.Combine(output, "foo.library.sobakasu")),
                Is.EqualTo("generated\n"));
        }

        [Test]
        public void AdditionsOnly_IsWrittenToTheOutput()
        {
            WriteText(Path.Combine(_additions, "foo.library.sobakasu"), "addition\r\n\r\n");
            var output = ExternalPath("additions-only");
            CreateGenerator().GenerateToDirectory(output, _additions);

            Assert.That(ReadText(Path.Combine(output, "foo.library.sobakasu")),
                Is.EqualTo("addition\n"));
        }

        [Test]
        public void GeneratedAndAdditions_AreComposedInDeterministicOrder()
        {
            WriteText(
                Path.Combine(_additions, "prelude.library.sobakasu"),
                "\r\npublic use maybe::Maybe;\r\n\r\n");
            var output = ExternalPath("composed");
            CreateGenerator(new Dictionary<string, string>
            {
                ["prelude.library.sobakasu"] = "public use unity::Vector3;\r\n"
            }).GenerateToDirectory(output, _additions);

            var path = Path.Combine(output, "prelude.library.sobakasu");
            Assert.That(ReadText(path), Is.EqualTo(
                "public use unity::Vector3;\n\n" +
                "public use maybe::Maybe;\n"));
            AssertUtf8WithoutBomAndLf(path);
        }

        [Test]
        public void CatalogFixture_GeneratesLibraryReadableByCompiler()
        {
            var output = ExternalPath("bundled-v2");
            var bindings = UdonBindingGeneratorTestSupport.CreateGenerator().Generate();
            new StandardLibraryGenerator(() => bindings, _packageRoot).GenerateToDirectory(
                output,
                _additions);

            var compilation = SobakasuTestEnvironment.CompileToUasm(
                "behavior { on interact {} }",
                output);

            Assert.That(compilation.Success, Is.True, compilation.ErrorText);
        }

        [Test]
        public void RecursivePaths_ArePreserved()
        {
            WriteText(
                Path.Combine(_additions, "vrc", "network.library.sobakasu"),
                "addition\n");
            var output = ExternalPath("recursive");
            CreateGenerator(new Dictionary<string, string>
            {
                ["unity/foo.library.sobakasu"] = "generated\n"
            }).GenerateToDirectory(output, _additions);

            Assert.That(File.Exists(Path.Combine(output, "unity", "foo.library.sobakasu")),
                Is.True);
            Assert.That(File.Exists(Path.Combine(output, "vrc", "network.library.sobakasu")),
                Is.True);
        }

        [Test]
        public void CleanRebuild_RemovesStaleFiles()
        {
            IReadOnlyDictionary<string, string> generated =
                new Dictionary<string, string> { ["old.library.sobakasu"] = "old\n" };
            var generator = CreateGenerator(() => generated);
            var output = ExternalPath("clean-rebuild");
            generator.GenerateToDirectory(output, _additions);
            Assert.That(File.Exists(Path.Combine(output, "old.library.sobakasu")), Is.True);

            generated = new Dictionary<string, string>
            {
                ["current.library.sobakasu"] = "current\n"
            };
            generator.GenerateToDirectory(output, _additions);

            Assert.That(File.Exists(Path.Combine(output, "old.library.sobakasu")), Is.False);
            Assert.That(File.Exists(Path.Combine(output, "current.library.sobakasu")), Is.True);
        }

        [Test]
        public void GenerationFailure_PreservesExistingOutput()
        {
            var output = ExternalPath("preserved-output");
            WriteText(Path.Combine(output, "valid.library.sobakasu"), "valid\n");
            var generator = new StandardLibraryGenerator(
                () => throw new InvalidOperationException("intentional failure"),
                _packageRoot);

            Assert.Throws<InvalidOperationException>(() =>
                generator.GenerateToDirectory(output, _additions));
            Assert.That(ReadText(Path.Combine(output, "valid.library.sobakasu")),
                Is.EqualTo("valid\n"));
            Assert.That(Directory.GetFiles(output, "*", SearchOption.AllDirectories),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void SameInput_ProducesByteForByteIdenticalDirectories()
        {
            WriteText(Path.Combine(_additions, "b.library.sobakasu"), "addition\r\n");
            var generator = CreateGenerator(new Dictionary<string, string>
            {
                ["z/foo.library.sobakasu"] = "nested\r\n",
                ["a.library.sobakasu"] = "generated\r\n\r\n"
            });
            var first = ExternalPath("deterministic-first");
            var second = ExternalPath("deterministic-second");

            generator.GenerateToDirectory(first, _additions);
            generator.GenerateToDirectory(second, _additions);

            Assert.That(Snapshot(second), Is.EqualTo(Snapshot(first)));
        }

        [Test]
        public void DefaultPaths_DoNotDependOnCurrentWorkingDirectory()
        {
            var original = Environment.CurrentDirectory;
            var firstOutput = StandardLibraryGenerator.DefaultOutputDirectory;
            var firstAdditions = StandardLibraryGenerator.DefaultAdditionsDirectory;
            var otherDirectory = ExternalPath("other-cwd");
            Directory.CreateDirectory(otherDirectory);
            try
            {
                Environment.CurrentDirectory = otherDirectory;
                Assert.That(StandardLibraryGenerator.DefaultOutputDirectory,
                    Is.EqualTo(firstOutput));
                Assert.That(StandardLibraryGenerator.DefaultAdditionsDirectory,
                    Is.EqualTo(firstAdditions));
            }
            finally
            {
                Environment.CurrentDirectory = original;
            }
        }

        [Test]
        public void ExplicitPaths_AreUsedAndDiagnosticsStayOutsideTheLibrary()
        {
            var additions = ExternalPath("explicit-additions");
            var output = ExternalPath("explicit-output");
            var diagnostics = ExternalPath("explicit-diagnostics");
            WriteText(Path.Combine(additions, "manual.library.sobakasu"), "manual\n");

            var result = CreateGenerator(new Dictionary<string, string>
            {
                ["generated.library.sobakasu"] = "generated\n"
            }).GenerateToDirectory(output, additions, diagnostics);

            Assert.That(result.OutputDirectory, Is.EqualTo(Path.GetFullPath(output)));
            Assert.That(result.AdditionsDirectory, Is.EqualTo(Path.GetFullPath(additions)));
            Assert.That(result.DiagnosticsDirectory,
                Is.EqualTo(Path.GetFullPath(diagnostics)));
            Assert.That(File.Exists(Path.Combine(output, "manual.library.sobakasu")), Is.True);
            Assert.That(File.Exists(Path.Combine(output, "generated.library.sobakasu")), Is.True);
            Assert.That(File.Exists(Path.Combine(
                diagnostics,
                UdonBindingGenerator.ReportFileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(
                output,
                UdonBindingGenerator.ReportFileName)), Is.False);
        }

        [Test]
        public void NonSobakasuCollision_IsAnErrorAndDoesNotReplaceOutput()
        {
            WriteText(Path.Combine(_additions, "README.md"), "manual\n");
            var output = ExternalPath("non-source-collision");
            WriteText(Path.Combine(output, "valid.library.sobakasu"), "valid\n");

            var generator = CreateGenerator(new Dictionary<string, string>
            {
                ["README.md"] = "generated\n"
            });
            Assert.Throws<InvalidOperationException>(() =>
                generator.GenerateToDirectory(output, _additions));
            Assert.That(ReadText(Path.Combine(output, "valid.library.sobakasu")),
                Is.EqualTo("valid\n"));
        }

        [Test]
        public void DangerousOutputPaths_AreRejected()
        {
            var externalAdditions = ExternalPath("safety-additions");
            Directory.CreateDirectory(externalAdditions);
            var generatorSource = Path.Combine(
                _packageRoot,
                "Editor",
                "Tools",
                "StandardLibraryGenerator");

            Assert.Throws<InvalidOperationException>(() =>
                StandardLibraryPathSafety.Validate(
                    _packageRoot,
                    externalAdditions,
                    externalAdditions,
                    null));
            Assert.Throws<InvalidOperationException>(() =>
                StandardLibraryPathSafety.Validate(
                    _packageRoot,
                    _packageRoot,
                    externalAdditions,
                    null));
            Assert.Throws<InvalidOperationException>(() =>
                StandardLibraryPathSafety.Validate(
                    _packageRoot,
                    _repositoryRoot,
                    externalAdditions,
                    null));
            Assert.Throws<InvalidOperationException>(() =>
                StandardLibraryPathSafety.Validate(
                    _packageRoot,
                    Path.GetPathRoot(_packageRoot),
                    externalAdditions,
                    null));
            Assert.Throws<InvalidOperationException>(() =>
                StandardLibraryPathSafety.Validate(
                    _packageRoot,
                    generatorSource,
                    externalAdditions,
                    null));
        }

        private StandardLibraryGenerator CreateGenerator(
            IReadOnlyDictionary<string, string> generated = null)
        {
            generated ??= new Dictionary<string, string>();
            return CreateGenerator(() => generated);
        }

        private StandardLibraryGenerator CreateGenerator(
            Func<IReadOnlyDictionary<string, string>> generated)
        {
            return new StandardLibraryGenerator(
                () => new UdonBindingGenerationResult(
                    generated(),
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        [UdonBindingGenerator.ReportFileName] = "{}\n",
                        [UdonBindingGenerator.SkippedMembersFileName] = string.Empty
                    },
                    new UdonApiGenerationReport()),
                _packageRoot);
        }

        private string ExternalPath(string name)
        {
            return Path.Combine(_externalRoot, name);
        }

        private static void WriteText(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, Utf8WithoutBom);
        }

        private static string ReadText(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static void AssertUtf8WithoutBomAndLf(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.GreaterThan(0));
            Assert.That(
                bytes.Length >= 3 &&
                bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf,
                Is.False);
            Assert.That(bytes.Contains((byte)'\r'), Is.False);
            Assert.That(bytes[^1], Is.EqualTo((byte)'\n'));
            Assert.That(bytes.Length == 1 || bytes[^2] != (byte)'\n', Is.True);
        }

        private static string Snapshot(string directory)
        {
            var records = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => new
                {
                    Path = path[(directory.Length + 1)..].Replace('\\', '/'),
                    Bytes = File.ReadAllBytes(path)
                })
                .OrderBy(record => record.Path, StringComparer.Ordinal)
                .Select(record =>
                    record.Path + "\0" + Convert.ToBase64String(record.Bytes));
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(
                Encoding.UTF8.GetBytes(string.Join("\n", records))));
        }
    }
}
