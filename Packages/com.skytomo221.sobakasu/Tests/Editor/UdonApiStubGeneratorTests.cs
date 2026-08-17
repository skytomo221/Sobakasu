using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Tools.UdonApiStubGenerator;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class UdonApiStubGeneratorFixture
    {
        public int Number;
        public readonly int ReadOnlyNumber;
        public int Count { get; set; }
        public static string Label => "fixture";
        public int this[int index] => index;

        public event Action Changed;

        public UdonApiStubGeneratorFixture()
        {
        }

        public UdonApiStubGeneratorFixture(int value)
        {
            Number = value;
        }

        public static UdonApiStubGeneratorFixture Find(string name)
        {
            return name == null ? null : new UdonApiStubGeneratorFixture();
        }

        public void SetActive(bool active)
        {
            if (active)
                Changed?.Invoke();
        }

        public int Mix(int value)
        {
            return value;
        }

        public float Mix(float value)
        {
            return value;
        }

        public void Hidden()
        {
        }

        public void RefValue(ref int value)
        {
            value++;
        }

        public bool RefOut(ref int value, out string text)
        {
            value++;
            text = value.ToString();
            return true;
        }

        public T Generic<T>(T value)
        {
            return value;
        }
    }

    public class UdonApiStubGeneratorTests
    {
        [TestCase("GameObject", "game_object")]
        [TestCase("VRCPlayerApi", "vrc_player_api")]
        [TestCase("URLLoader", "url_loader")]
        [TestCase("HTTPRequest", "http_request")]
        [TestCase("activeSelf", "active_self")]
        [TestCase("isActiveAndEnabled", "is_active_and_enabled")]
        public void NameUtility_ConvertsStableSnakeCase(
            string source,
            string expected)
        {
            Assert.That(SobakasuNameUtility.ToSnakeCase(source), Is.EqualTo(expected));
        }

        [Test]
        public void TypeFormatter_ReusesBuiltInsAndRejectsUnsupportedShapes()
        {
            var formatter = new UdonApiStubTypeFormatter();

            AssertFormats(formatter, typeof(int), "i32");
            AssertFormats(formatter, typeof(long), "i64");
            AssertFormats(formatter, typeof(float), "f32");
            AssertFormats(formatter, typeof(double), "f64");
            AssertFormats(formatter, typeof(bool), "bool");
            AssertFormats(formatter, typeof(string), "string");
            AssertFormats(formatter, typeof(int[]), "[i32]");
            AssertFormats(formatter, typeof(int).MakeByRefType(), "i32");

            Assert.That(formatter.TryFormat(
                typeof(UdonApiStubGeneratorFixture),
                typeof(UdonApiStubGeneratorFixture),
                out var selfType,
                out _), Is.True);
            Assert.That(selfType, Is.EqualTo("Self"));

            Assert.That(formatter.TryFormat(
                typeof(int[,]),
                typeof(UdonApiStubGeneratorFixture),
                out _,
                out var arrayReason), Is.False);
            Assert.That(arrayReason, Does.Contain("Array shape"));

            Assert.That(formatter.TryFormat(
                typeof(List<int>),
                typeof(UdonApiStubGeneratorFixture),
                out _,
                out var genericReason), Is.False);
            Assert.That(genericReason, Does.Contain("Generic type"));

            Assert.That(formatter.TryFormat(
                typeof(int).MakePointerType(),
                typeof(UdonApiStubGeneratorFixture),
                out _,
                out var pointerReason), Is.False);
            Assert.That(pointerReason, Does.Contain("Pointer type"));
        }

        [Test]
        public void Generator_RendersSupportedMemberKindsAndSnakeCaseNames()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiStubGeneratorFixture)
            });
            var source = GetFixtureSource(result);

            Assert.That(source, Does.Contain("pub impl UdonApiStubGeneratorFixture = extern"));
            Assert.That(CountOccurrences(source, "pub static fn new("), Is.EqualTo(2));
            Assert.That(source, Does.Contain("pub static fn find(name: string) -> Self"));
            Assert.That(source, Does.Contain("pub fn set_active(active: bool)"));
            Assert.That(source, Does.Contain("pub fn count -> i32"));
            Assert.That(source, Does.Contain("pub fn set_count(value: i32)"));
            Assert.That(source, Does.Contain("pub fn number -> i32"));
            Assert.That(source, Does.Contain("pub fn set_number(value: i32)"));
            Assert.That(source, Does.Contain("pub static fn label -> string"));
            Assert.That(source, Does.Contain("= extern new Self("));
            Assert.That(source, Does.Contain("= extern self.SetActive(active)"));
            Assert.That(source, Does.Contain("= extern self.Count = value"));
            Assert.That(source, Does.Contain("pub fn ref_value(value: i32) -> i32"));
            Assert.That(source,
                Does.Contain("= extern self.RefValue(ref i32 value)"));
            Assert.That(source,
                Does.Contain("pub fn ref_out(value: i32) -> (bool, i32, string)"));
            Assert.That(source,
                Does.Contain("= extern self.RefOut(ref i32 value, out string text)"));
            Assert.That(source, Does.Not.Contain(" -> Self {"));

            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser));
        }

        [Test]
        public void Generator_PreservesDistinctOverloads()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiStubGeneratorFixture)
            });
            var source = GetFixtureSource(result);

            Assert.That(source, Does.Contain("pub fn mix(value: i32) -> i32"));
            Assert.That(source, Does.Contain("pub fn mix(value: f32) -> f32"));
            Assert.That(CountOccurrences(source, "pub fn mix("), Is.EqualTo(2));
        }

        [Test]
        public void Generator_ReportsHiddenAndUnsupportedMembers()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiStubGeneratorFixture)
            });
            var source = GetFixtureSource(result);

            Assert.That(source, Does.Not.Contain("fn hidden"));
            Assert.That(source, Does.Not.Contain("fn generic"));
            Assert.That(FindSkip(result.Report, "Hidden").reason,
                Does.Contain("not exposed to Udon"));
            Assert.That(FindSkip(result.Report, "Generic").reason,
                Does.Contain("Generic methods"));
            Assert.That(FindSkip(result.Report, "Item").reason,
                Does.Contain("Indexed properties"));
            Assert.That(FindSkip(result.Report, "Changed").reason,
                Does.Contain("CLR events"));
        }

        [Test]
        public void Generator_MaintainsCompletenessInvariants()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiStubGeneratorFixture),
                typeof(int)
            });
            var report = result.Report;

            Assert.That(report.types_discovered,
                Is.EqualTo(report.types_generated + report.types_skipped));
            Assert.That(report.members_discovered,
                Is.EqualTo(report.members_generated + report.members_skipped));
            Assert.That(report.types_discovered, Is.EqualTo(2));
            Assert.That(report.types_skipped, Is.EqualTo(1));
        }

        [Test]
        public void Generator_ProducesDeterministicFilesAndOrdering()
        {
            var generator = CreateGenerator();
            var first = generator.Generate(new[]
            {
                typeof(int),
                typeof(UdonApiStubGeneratorFixture)
            });
            var second = generator.Generate(new[]
            {
                typeof(UdonApiStubGeneratorFixture),
                typeof(int)
            });

            Assert.That(second.Files.Keys, Is.EqualTo(first.Files.Keys));
            foreach (var pair in first.Files)
            {
                Assert.That(second.Files[pair.Key], Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void OutputWriter_UsesSnakeCaseArtifactsAndUtf8WithoutBom()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiStubGeneratorFixture)
            });
            var output = NewTemporaryPath();
            try
            {
                new UdonApiStubOutputWriter().Write(output, result);

                Assert.That(File.Exists(Path.Combine(
                    output,
                    UdonApiStubGenerator.ReportFileName)), Is.True);
                Assert.That(File.Exists(Path.Combine(
                    output,
                    UdonApiStubGenerator.SkippedMembersFileName)), Is.True);
                var stubPath = Path.Combine(
                    output,
                    typeof(UdonApiStubGeneratorFixture).Namespace.Replace('.',
                        Path.DirectorySeparatorChar),
                    "udon_api_stub_generator_fixture.sobakasu");
                Assert.That(File.Exists(stubPath), Is.True);

                var bytes = File.ReadAllBytes(stubPath);
                Assert.That(bytes.Length, Is.GreaterThan(3));
                Assert.That(
                    bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf,
                    Is.False);
            }
            finally
            {
                if (Directory.Exists(output))
                    Directory.Delete(output, true);
            }
        }

        [Test]
        public void OutputWriter_DoesNotOverwriteExistingFiles()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiStubGeneratorFixture)
            });
            var output = NewTemporaryPath();
            Directory.CreateDirectory(output);
            var manualPath = Path.Combine(output, "manual.sobakasu");
            File.WriteAllText(manualPath, "human edit");
            try
            {
                Assert.Throws<IOException>(() =>
                    new UdonApiStubOutputWriter().Write(output, result));
                Assert.That(File.ReadAllText(manualPath), Is.EqualTo("human edit"));
                Assert.That(Directory.GetFiles(output), Has.Length.EqualTo(1));
            }
            finally
            {
                Directory.Delete(output, true);
            }
        }

        private static UdonApiStubGenerator CreateGenerator()
        {
            var formatter = new UdonApiStubTypeFormatter();
            var exposure = new FixtureExposure();
            return new UdonApiStubGenerator(
                new UdonApiDiscovery(exposure, formatter),
                new SobakasuStubRenderer(formatter));
        }

        private static void AssertFormats(
            UdonApiStubTypeFormatter formatter,
            Type type,
            string expected)
        {
            Assert.That(formatter.TryFormat(
                type,
                typeof(UdonApiStubGeneratorFixture),
                out var actual,
                out var reason), Is.True, reason);
            Assert.That(actual, Is.EqualTo(expected));
        }

        private static string GetFixtureSource(UdonApiGenerationResult result)
        {
            foreach (var pair in result.Files)
            {
                if (pair.Key.EndsWith(
                    "udon_api_stub_generator_fixture.sobakasu",
                    StringComparison.Ordinal))
                {
                    return pair.Value;
                }
            }

            Assert.Fail("The fixture stub was not generated.");
            return null;
        }

        private static UdonApiSkipRecord FindSkip(
            UdonApiGenerationReport report,
            string memberName)
        {
            foreach (var record in report.skipped_members)
            {
                if (record.full_name.EndsWith(
                    "." + memberName,
                    StringComparison.Ordinal))
                {
                    return record;
                }
            }

            Assert.Fail($"No skip record was found for '{memberName}'.");
            return null;
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        private static string FormatDiagnostics(SobakasuParser parser)
        {
            var messages = new List<string>();
            foreach (var diagnostic in parser.Diagnostics.Diagnostics)
                messages.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", messages);
        }

        private static string NewTemporaryPath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                $"SobakasuUdonApiStubGeneratorTests_{Guid.NewGuid():N}");
        }

        private sealed class FixtureExposure : IUdonApiExposure
        {
            private readonly string _fixturePrefix =
                UdonExternSignatureFormatter.GetUdonTypeName(
                    typeof(UdonApiStubGeneratorFixture)) + ".";

            public bool IsTypeExposed(Type type)
            {
                return true;
            }

            public bool IsMemberExposed(string externSignature)
            {
                return externSignature.StartsWith(
                           _fixturePrefix,
                           StringComparison.Ordinal) &&
                       externSignature.IndexOf(
                           "__Hidden",
                           StringComparison.Ordinal) < 0;
            }
        }
    }
}
