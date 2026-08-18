using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
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

        public void OutReference(out UdonApiStubGeneratorFixture value)
        {
            value = new UdonApiStubGeneratorFixture();
        }

        public void OutNumber(out int value)
        {
            value = 1;
        }

        public T Generic<T>(T value)
        {
            return value;
        }
    }

    public sealed class UdonApiNormalConstructorFixture
    {
        public UdonApiNormalConstructorFixture(int value)
        {
        }
    }

    public sealed class UdonApiRefConstructorFixture
    {
        public UdonApiRefConstructorFixture(ref int value)
        {
            value++;
        }
    }

    public sealed class UdonApiOutConstructorFixture
    {
        public UdonApiOutConstructorFixture(out string name)
        {
            name = "fixture";
        }
    }

    public sealed class UdonApiMixedConstructorFixture
    {
        public UdonApiMixedConstructorFixture(
            ref int value,
            out string name,
            ref float weight)
        {
            value++;
            name = value.ToString();
            weight += 1.0f;
        }
    }

    public static class UdonApiStaticFixture
    {
        public static bool IsVisible { get; set; }

        public static int Abs(int value) => Math.Abs(value);
        public static float Abs(float value) => Math.Abs(value);
        public static bool IsReady() => true;
        public static bool isActiveAndEnabled() => true;
        public static int IsCount() => 1;
    }

    public static class UdonApiStaticFixture2
    {
        public static double Abs(double value) => Math.Abs(value);
    }

    public static class UdonApiStaticCollisionFixture
    {
        public static int Abs(int value) => Math.Abs(value);
    }

    namespace PolicyFixtures
    {
        public static class NamespaceFixture
        {
            public static int Value() => 1;
        }

        namespace Deep
        {
            public static class DeepNamespaceFixture
            {
                public static int DeepValue() => 2;
            }
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
        public void GeneratedSystemMathSource_BindsAgainstInstalledCatalog()
        {
            var result = UdonApiStubGenerator.CreateDefault()
                .Generate(new[] { typeof(Math) });
            var source = GetSource(result, "external.sobakasu");
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser));

            var binder = new SobakasuBinder();
            binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Generator_RendersNormalRefOutAndMixedConstructors()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiNormalConstructorFixture),
                typeof(UdonApiRefConstructorFixture),
                typeof(UdonApiOutConstructorFixture),
                typeof(UdonApiMixedConstructorFixture)
            });

            var normal = GetSource(result, "external.sobakasu");
            var byRef = normal;
            var byOut = normal;
            var mixed = normal;

            Assert.That(normal,
                Does.Contain("pub static fn new(value: i32) -> Self"));
            Assert.That(normal, Does.Contain("= extern new Self(value)"));
            Assert.That(byRef,
                Does.Contain("pub static fn new(value: i32) -> (Self, i32)"));
            Assert.That(byRef,
                Does.Contain("= extern new Self(ref i32 value)"));
            Assert.That(byOut,
                Does.Contain("pub static fn new() -> (Self, string)"));
            Assert.That(byOut,
                Does.Contain("= extern new Self(out string name)"));
            Assert.That(mixed,
                Does.Contain(
                    "pub static fn new(value: i32, weight: f32) -> (Self, i32, string, f32)"));
            Assert.That(mixed,
                Does.Contain(
                    "= extern new Self(ref i32 value, out string name, ref f32 weight)"));
            Assert.That(byOut, Does.Not.Contain("maybe out"));
            Assert.That(mixed, Does.Not.Contain("maybe out"));

            foreach (var source in new[] { normal, byRef, byOut, mixed })
            {
                var parser = new SobakasuParser(SourceText.From(source));
                parser.ParseCompilationUnit();
                Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                    FormatDiagnostics(parser));
            }
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
                var stubPath = Path.Combine(output, "external.sobakasu");
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

        private static UdonApiStubGenerator CreateGenerator(
            UdonApiStubGenerationConfig configuration = null)
        {
            var formatter = new UdonApiStubTypeFormatter();
            var exposure = new FixtureExposure();
            return new UdonApiStubGenerator(
                new UdonApiDiscovery(exposure, formatter),
                new SobakasuStubRenderer(formatter),
                configuration);
        }

        private static UdonApiStubMemberRule MemberRule(
            Type declaringType,
            string memberKind,
            string member,
            IReadOnlyList<Type> parameterTypes,
            string returnProjection = null,
            string outParameter = null,
            string outProjection = null,
            string name = null,
            bool exclude = false)
        {
            var clrParameterTypes = new string[parameterTypes.Count];
            for (var index = 0; index < parameterTypes.Count; index++)
            {
                clrParameterTypes[index] =
                    (parameterTypes[index].FullName ?? parameterTypes[index].Name)
                    .Replace('+', '.');
            }

            return new UdonApiStubMemberRule
            {
                declaring_type = (declaringType.FullName ?? declaringType.Name)
                    .Replace('+', '.'),
                member_kind = memberKind,
                member = member,
                parameter_types = clrParameterTypes,
                @return = returnProjection,
                @out = string.IsNullOrWhiteSpace(outParameter)
                    ? Array.Empty<UdonApiStubOutRule>()
                    : new[]
                    {
                        new UdonApiStubOutRule
                        {
                            parameter = outParameter,
                            projection = outProjection
                        }
                    },
                name = name,
                exclude = exclude
            };
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
            return GetSource(
                result,
                "external.sobakasu");
        }

        private static string GetSource(
            UdonApiGenerationResult result,
            string fileName)
        {
            foreach (var pair in result.Files)
            {
                if (pair.Key.EndsWith(
                    fileName,
                    StringComparison.Ordinal))
                {
                    return pair.Value;
                }
            }

            Assert.Fail($"The fixture stub '{fileName}' was not generated.");
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

        private static UdonApiGeneratedTypeRecord FindGeneratedType(
            UdonApiGenerationReport report,
            Type type)
        {
            var name = (type.FullName ?? type.Name).Replace('+', '.');
            foreach (var record in report.generated_types)
            {
                if (string.Equals(
                    record.clr_declaring_type,
                    name,
                    StringComparison.Ordinal))
                {
                    return record;
                }
            }

            Assert.Fail($"No generated type record was found for '{name}'.");
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
            return FormatDiagnostics(parser.Diagnostics.Diagnostics);
        }

        private static string FormatDiagnostics(
            IReadOnlyList<Diagnostic> diagnostics)
        {
            var messages = new List<string>();
            foreach (var diagnostic in diagnostics)
                messages.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", messages);
        }

        private static string NewTemporaryPath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                $"SobakasuUdonApiStubGeneratorTests_{Guid.NewGuid():N}");
        }

        private static Type FindLoadedType(string qualifiedName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(qualifiedName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        private sealed class FixtureExposure : IUdonApiExposure
        {
            private readonly string[] _fixturePrefixes =
            {
                GetPrefix(typeof(UdonApiStubGeneratorFixture)),
                GetPrefix(typeof(UdonApiNormalConstructorFixture)),
                GetPrefix(typeof(UdonApiRefConstructorFixture)),
                GetPrefix(typeof(UdonApiOutConstructorFixture)),
                GetPrefix(typeof(UdonApiMixedConstructorFixture)),
                GetPrefix(typeof(UdonApiStaticFixture)),
                GetPrefix(typeof(UdonApiStaticFixture2)),
                GetPrefix(typeof(UdonApiStaticCollisionFixture)),
                GetPrefix(typeof(PolicyFixtures.NamespaceFixture)),
                GetPrefix(typeof(PolicyFixtures.Deep.DeepNamespaceFixture))
            };

            public bool IsTypeExposed(Type type)
            {
                return true;
            }

            public bool IsMemberExposed(string externSignature)
            {
                foreach (var prefix in _fixturePrefixes)
                {
                    if (externSignature.StartsWith(prefix, StringComparison.Ordinal) &&
                        externSignature.IndexOf(
                            "__Hidden",
                            StringComparison.Ordinal) < 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static string GetPrefix(Type type)
            {
                return UdonExternSignatureFormatter.GetUdonTypeName(type) + ".";
            }
        }

        [Test]
        public void Generator_AggregatesStaticClassesAsTopLevelOverloadsAndPredicates()
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStaticFixture).FullName,
                    @namespace = "math",
                    placement = "top_level"
                },
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStaticFixture2).FullName,
                    @namespace = "math",
                    placement = "top_level"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiStaticFixture2),
                typeof(UdonApiStaticFixture)
            });
            var source = GetSource(result, "math.sobakasu");

            Assert.That(source, Does.Contain("pub fn abs(value: i32) -> i32"));
            Assert.That(source, Does.Contain("pub fn abs(value: f32) -> f32"));
            Assert.That(source, Does.Contain("pub fn abs(value: f64) -> f64"));
            Assert.That(source, Does.Contain("pub fn ready?() -> bool"));
            Assert.That(source, Does.Contain("pub fn active_and_enabled?() -> bool"));
            Assert.That(source, Does.Contain("pub fn is_count() -> i32"));
            Assert.That(source, Does.Contain("pub fn visible? -> bool"));
            Assert.That(source, Does.Contain("pub fn set_visible(value: bool)"));
            Assert.That(source, Does.Not.Contain("pub impl UdonApiStaticFixture"));
            Assert.That(result.Report.top_level_static_type_count, Is.EqualTo(2));
            Assert.That(result.Report.namespaces_generated, Is.EqualTo(1));

            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser));
        }

        [Test]
        public void Generator_AppliesMaybeReturnMaybeOutAndConstructorProjection()
        {
            var fixtureType = typeof(UdonApiStubGeneratorFixture);
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.members = new[]
            {
                MemberRule(
                    fixtureType,
                    "static_method",
                    "Find",
                    new[] { typeof(string) },
                    returnProjection: "maybe"),
                MemberRule(
                    fixtureType,
                    "instance_method",
                    "RefOut",
                    new[]
                    {
                        typeof(int).MakeByRefType(),
                        typeof(string).MakeByRefType()
                    },
                    outParameter: "text",
                    outProjection: "maybe"),
                MemberRule(
                    fixtureType,
                    "instance_method",
                    "OutReference",
                    new[] { fixtureType.MakeByRefType() },
                    outParameter: "value",
                    outProjection: "maybe")
            };

            var result = CreateGenerator(config).Generate(new[] { fixtureType });
            var source = GetFixtureSource(result);

            Assert.That(source,
                Does.Contain("pub static fn find(name: string) -> Maybe<Self>"));
            Assert.That(source, Does.Contain("= maybe extern " + fixtureType.FullName + ".Find(name)"));
            Assert.That(source,
                Does.Contain("pub fn ref_out(value: i32) -> (bool, i32, Maybe<string>)"));
            Assert.That(source,
                Does.Contain("ref i32 value, maybe out string text"));
            Assert.That(source,
                Does.Contain("pub fn out_reference() -> Maybe<Self>"));
            Assert.That(source,
                Does.Contain("maybe out Self value"));
            Assert.That(result.Report.maybe_return_count, Is.EqualTo(1));
            Assert.That(result.Report.maybe_out_count, Is.EqualTo(2));
            Assert.That(result.Report.rules_matched, Is.EqualTo(3));
            Assert.That(result.Files[UdonApiStubGenerator.ReportFileName],
                Does.Contain("\"maybe_return_count\": 1"));
            Assert.That(result.Files[UdonApiStubGenerator.ReportFileName],
                Does.Contain("\"sobakasu_namespace\": \"external\""));

            var constructorConfig = UdonApiStubGenerationConfig.CreateDefault();
            constructorConfig.members = new[]
            {
                MemberRule(
                    typeof(UdonApiOutConstructorFixture),
                    "constructor",
                    ".ctor",
                    new[] { typeof(string).MakeByRefType() },
                    outParameter: "name",
                    outProjection: "maybe")
            };
            var constructorResult = CreateGenerator(constructorConfig).Generate(new[]
            {
                typeof(UdonApiOutConstructorFixture)
            });
            var constructorSource = GetSource(constructorResult, "external.sobakasu");
            Assert.That(constructorSource,
                Does.Contain("pub static fn new() -> (Self, Maybe<string>)"));
            Assert.That(constructorSource,
                Does.Contain("= extern new Self(maybe out string name)"));
        }

        [Test]
        public void Generator_AllowsStaticClassImplPlacementOverride()
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStaticFixture2).FullName,
                    @namespace = "utility",
                    placement = "impl"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiStaticFixture2)
            });
            var source = GetSource(result, "utility.sobakasu");

            Assert.That(source,
                Does.Contain("pub impl UdonApiStaticFixture2 = extern"));
            Assert.That(source,
                Does.Contain("pub static fn abs(value: f64) -> f64"));
            Assert.That(result.Report.impl_type_count, Is.EqualTo(1));
            Assert.That(result.Report.top_level_static_type_count, Is.Zero);
        }

        [Test]
        public void Generator_UsesExplicitRenameAndExclusionBeforeAutomaticNaming()
        {
            var fixtureType = typeof(UdonApiStaticFixture);
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.members = new[]
            {
                MemberRule(
                    fixtureType,
                    "static_method",
                    "IsReady",
                    Array.Empty<Type>(),
                    name: "available?"),
                MemberRule(
                    fixtureType,
                    "static_method",
                    "IsCount",
                    Array.Empty<Type>(),
                    exclude: true)
            };

            var result = CreateGenerator(config).Generate(new[] { fixtureType });
            var source = GetSource(result, "external.sobakasu");

            Assert.That(source, Does.Contain("pub fn available?() -> bool"));
            Assert.That(source, Does.Not.Contain("fn ready?"));
            Assert.That(source, Does.Not.Contain("fn is_count"));
            Assert.That(result.Report.explicit_exclusions, Is.EqualTo(1));
        }

        [Test]
        public void Generator_ResolvesNamespaceRulesByTypeAndLongestPrefix()
        {
            var rootNamespace = typeof(UdonApiStaticFixture).Namespace;
            var policyNamespace = typeof(PolicyFixtures.NamespaceFixture).Namespace;
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.namespaces = new[]
            {
                new UdonApiStubNamespaceRule
                {
                    clr_namespace = rootNamespace,
                    @namespace = "root_api",
                    preserve_subnamespaces = false
                },
                new UdonApiStubNamespaceRule
                {
                    clr_namespace = policyNamespace,
                    @namespace = "fixtures",
                    preserve_subnamespaces = true
                }
            };
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStaticFixture).FullName,
                    @namespace = "exact"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture),
                typeof(UdonApiStaticFixture),
                typeof(PolicyFixtures.NamespaceFixture)
            });

            Assert.That(result.Files.Keys, Does.Contain("exact.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("fixtures.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("fixtures/deep.sobakasu"));
            Assert.That(result.Files["fixtures.sobakasu"],
                Does.StartWith("pub mod deep;\n"));
            Assert.That(result.Report.namespace_rules_matched, Is.EqualTo(2));
            Assert.That(result.Report.unmatched_namespace_rules, Is.Empty);
            Assert.That(FindGeneratedType(result.Report, typeof(UdonApiStaticFixture))
                .sobakasu_namespace, Is.EqualTo("exact"));
            Assert.That(FindGeneratedType(
                result.Report,
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture))
                .sobakasu_namespace, Is.EqualTo("fixtures.deep"));
        }

        [Test]
        public void Generator_FlattensNamespaceAndAggregatesTypes()
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.namespaces = new[]
            {
                new UdonApiStubNamespaceRule
                {
                    clr_namespace = typeof(PolicyFixtures.NamespaceFixture).Namespace,
                    @namespace = "flat",
                    preserve_subnamespaces = false
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(PolicyFixtures.NamespaceFixture),
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)
            });

            Assert.That(result.Files.Keys, Does.Contain("flat.sobakasu"));
            Assert.That(result.Files.Keys, Does.Not.Contain("flat/deep.sobakasu"));
            Assert.That(result.Files["flat.sobakasu"],
                Does.Contain("pub fn value() -> i32"));
            Assert.That(result.Files["flat.sobakasu"],
                Does.Contain("pub fn deep_value() -> i32"));
        }

        [Test]
        public void Generator_ReportsPostPolicyTopLevelCollisionWithoutRenaming()
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStaticFixture).FullName,
                    @namespace = "collision"
                },
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStaticCollisionFixture).FullName,
                    @namespace = "collision"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiStaticFixture),
                typeof(UdonApiStaticCollisionFixture)
            });
            var source = GetSource(result, "collision.sobakasu");

            Assert.That(source, Does.Not.Contain("pub fn abs(value: i32)"));
            Assert.That(source, Does.Not.Contain("abs_2"));
            Assert.That(result.Report.declaration_collisions, Is.EqualTo(2));
            Assert.That(result.Report.skipped_members.Exists(record =>
                record.reason.Contains("same Sobakasu declaration")), Is.True);
        }

        [Test]
        public void Generator_RejectsInvalidAndStalePolicyRules()
        {
            var fixtureType = typeof(UdonApiStubGeneratorFixture);

            var stale = UdonApiStubGenerationConfig.CreateDefault();
            stale.members = new[]
            {
                MemberRule(
                    fixtureType,
                    "static_method",
                    "Missing",
                    Array.Empty<Type>())
            };
            Assert.That(
                () => CreateGenerator(stale).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonApiStubConfigurationException>()
                    .With.Message.Contains("did not match"));

            var valueReturn = UdonApiStubGenerationConfig.CreateDefault();
            valueReturn.members = new[]
            {
                MemberRule(
                    fixtureType,
                    "instance_method",
                    "Mix",
                    new[] { typeof(int) },
                    returnProjection: "maybe")
            };
            Assert.That(
                () => CreateGenerator(valueReturn).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonApiStubConfigurationException>()
                    .With.Message.Contains("non-reference return"));

            var refProjection = UdonApiStubGenerationConfig.CreateDefault();
            refProjection.members = new[]
            {
                MemberRule(
                    fixtureType,
                    "instance_method",
                    "RefValue",
                    new[] { typeof(int).MakeByRefType() },
                    outParameter: "value",
                    outProjection: "maybe")
            };
            Assert.That(
                () => CreateGenerator(refProjection).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonApiStubConfigurationException>()
                    .With.Message.Contains("it is ref"));

            var invalidEnum = UdonApiStubGenerationConfig.CreateDefault();
            invalidEnum.defaults.reference_return = "nullable";
            Assert.That(
                () => CreateGenerator(invalidEnum).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonApiStubConfigurationException>()
                    .With.Message.Contains("invalid projection"));

            var valueOut = UdonApiStubGenerationConfig.CreateDefault();
            valueOut.members = new[]
            {
                MemberRule(
                    fixtureType,
                    "instance_method",
                    "OutNumber",
                    new[] { typeof(int).MakeByRefType() },
                    outParameter: "value",
                    outProjection: "maybe")
            };
            Assert.That(
                () => CreateGenerator(valueOut).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonApiStubConfigurationException>()
                    .With.Message.Contains("non-reference parameter"));

            var missingOut = UdonApiStubGenerationConfig.CreateDefault();
            missingOut.members = new[]
            {
                MemberRule(
                    fixtureType,
                    "instance_method",
                    "RefOut",
                    new[]
                    {
                        typeof(int).MakeByRefType(),
                        typeof(string).MakeByRefType()
                    },
                    outParameter: "missing",
                    outProjection: "maybe")
            };
            Assert.That(
                () => CreateGenerator(missingOut).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonApiStubConfigurationException>()
                    .With.Message.Contains("does not exist"));

            var duplicate = UdonApiStubGenerationConfig.CreateDefault();
            var duplicateRule = MemberRule(
                fixtureType,
                "static_method",
                "Find",
                new[] { typeof(string) });
            duplicate.members = new[] { duplicateRule, duplicateRule };
            Assert.That(
                () => CreateGenerator(duplicate).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonApiStubConfigurationException>()
                    .With.Message.Contains("Conflicting member rules"));
        }

        [Test]
        public void ConfigurationLoader_RejectsUnknownProperties()
        {
            var path = NewTemporaryPath() + ".json";
            File.WriteAllText(path,
                "{\"version\":\"1\",\"defaults\":{\"reference_retrn\":\"maybe\"}}");
            try
            {
                Assert.That(
                    () => UdonApiStubGenerationConfig.Load(path),
                    Throws.TypeOf<UdonApiStubConfigurationException>()
                        .With.Message.Contains("Unknown property 'reference_retrn'"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ConfigurationLoader_LoadsVersionedSampleSchema()
        {
            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "docs/samples/udon-api-stub-generation-config.json");
            var config = UdonApiStubGenerationConfig.Load(path);

            Assert.That(config.version, Is.EqualTo("1"));
            Assert.That(config.defaults.@namespace, Is.EqualTo("external"));
            Assert.That(config.namespaces, Has.Length.EqualTo(2));
            Assert.That(config.types, Has.Length.EqualTo(3));
            Assert.That(config.members, Has.Length.EqualTo(1));
            Assert.That(config.members[0].@return, Is.EqualTo("maybe"));

            var utilitiesType = FindLoadedType("VRC.SDKBase.Utilities");
            Assert.That(utilitiesType, Is.Not.Null);
            var result = UdonApiStubGenerator.CreateDefault(path).Generate(new[]
            {
                typeof(Math),
                typeof(UnityEngine.Debug),
                typeof(UnityEngine.GameObject),
                utilitiesType
            });
            Assert.That(result.Files.Keys, Does.Contain("math.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("debug.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("unity.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("vrc.sobakasu"));
            Assert.That(result.Report.rules_configured, Is.EqualTo(6));
            Assert.That(result.Report.rules_matched, Is.EqualTo(6));
        }
    }
}
