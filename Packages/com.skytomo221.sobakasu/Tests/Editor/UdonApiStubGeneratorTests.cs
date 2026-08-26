using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
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

    public class UdonApiInheritedParentFixture
    {
        public event Action Changed;

        public void Foo()
        {
        }

        protected void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }

    public sealed class UdonApiInheritedChildAFixture : UdonApiInheritedParentFixture
    {
    }

    public sealed class UdonApiInheritedChildBFixture : UdonApiInheritedParentFixture
    {
    }

    public sealed class UdonApiGenericCoverageFixture
    {
        public T ExposedGeneric<T>(T value)
        {
            return value;
        }

        public T UnexposedGeneric<T>(T value)
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

    public struct UdonApiStructFixture
    {
        public int Value;
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
        public void Generator_SplitsGeneratedTypesAndReExportsThemFromFacade()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiStaticFixture),
                typeof(UdonApiStructFixture),
                typeof(UdonApiStubGeneratorFixture)
            });

            Assert.That(result.Files.Keys, Does.Contain("external.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("external/udon_api_static_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("external/udon_api_struct_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("external/udon_api_stub_generator_fixture.sobakasu"));

            var facade = result.Files["external.sobakasu"];
            Assert.That(facade, Is.EqualTo(
                "mod udon_api_static_fixture;\n" +
                "mod udon_api_struct_fixture;\n" +
                "mod udon_api_stub_generator_fixture;\n" +
                "\n" +
                "pub use udon_api_static_fixture.*;\n" +
                "pub use udon_api_struct_fixture.UdonApiStructFixture;\n" +
                "pub use udon_api_stub_generator_fixture.UdonApiStubGeneratorFixture;\n"));
            Assert.That(GetTypeSource(result, typeof(UdonApiStructFixture)),
                Does.StartWith("pub impl UdonApiStructFixture = extern"));
            Assert.That(GetTypeSource(result, typeof(UdonApiStubGeneratorFixture)),
                Does.Not.Contain("UdonApiStructFixture"));
            Assert.That(
                FindGeneratedType(result.Report, typeof(UdonApiStructFixture))
                    .generated_file,
                Is.EqualTo("external/udon_api_struct_fixture.sobakasu"));
            AssertAllStubSourcesParse(result);
        }

        [Test]
        public void Generator_UsesFinalWrapperNameForTypeModulePath()
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStubGeneratorFixture).FullName,
                    @namespace = "renamed",
                    name = "URLLoader"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiStubGeneratorFixture)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("renamed/url_loader.sobakasu"));
            Assert.That(result.Files["renamed.sobakasu"],
                Does.Contain("pub use url_loader.URLLoader;"));
            Assert.That(result.Files["renamed/url_loader.sobakasu"],
                Does.StartWith("pub impl URLLoader = extern"));
            Assert.That(
                FindGeneratedType(result.Report, typeof(UdonApiStubGeneratorFixture))
                    .generated_file,
                Is.EqualTo("renamed/url_loader.sobakasu"));
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
            var source = GetTypeSource(result, typeof(Math));
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
        public void GeneratedImplFacade_PreservesLogicalTypeApiThroughCompiler()
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(UnityEngine.GameObject).FullName,
                    @namespace = "unity",
                    placement = "impl"
                }
            };
            config.members = new[]
            {
                MemberRule(
                    typeof(UnityEngine.GameObject),
                    "static_method",
                    "op_Equality",
                    new[] { typeof(UnityEngine.Object), typeof(UnityEngine.Object) },
                    exclude: true),
                MemberRule(
                    typeof(UnityEngine.GameObject),
                    "static_method",
                    "op_Implicit",
                    new[] { typeof(UnityEngine.Object) },
                    exclude: true),
                MemberRule(
                    typeof(UnityEngine.GameObject),
                    "static_method",
                    "op_Inequality",
                    new[] { typeof(UnityEngine.Object), typeof(UnityEngine.Object) },
                    exclude: true)
            };
            var result = CreateInstalledGenerator(config).Generate(new[]
            {
                typeof(UnityEngine.GameObject)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("unity/game_object.sobakasu"));
            Assert.That(result.Files["unity.sobakasu"],
                Does.Contain("mod game_object;"));
            Assert.That(result.Files["unity.sobakasu"],
                Does.Contain("pub use game_object.GameObject;"));

            WithGeneratedLibrary(result, root =>
            {
                var compilation = SobakasuCompiler.CompileToUasm(
                    @"use unity.GameObject;
on interact { GameObject.find(""Sobakasu""); }",
                    root);
                Assert.That(compilation.Success, Is.True, compilation.ErrorText);
            });
        }

        [Test]
        public void GeneratedTopLevelFacades_MergeOverloadsFromSplitTypeModules()
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(Math).FullName,
                    @namespace = "math",
                    placement = "top_level"
                },
                new UdonApiStubTypeRule
                {
                    type = typeof(UnityEngine.Mathf).FullName,
                    @namespace = "math",
                    placement = "top_level"
                }
            };
            var result = CreateInstalledGenerator(config).Generate(new[]
            {
                typeof(UnityEngine.Mathf),
                typeof(Math)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("math/math.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("math/mathf.sobakasu"));
            Assert.That(result.Files["math.sobakasu"],
                Does.Contain("pub use math.math.*;"));
            Assert.That(result.Files["math.sobakasu"],
                Does.Contain("pub use mathf.*;"));

            WithGeneratedLibrary(result, root =>
            {
                var compilation = SobakasuCompiler.CompileToUasm(
                    @"use math;
on interact {
  math.round(1.25f32);
  math.round(1.25f64);
}",
                    root);
                Assert.That(compilation.Success, Is.True, compilation.ErrorText);
            });
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

            var normal = GetTypeSource(
                result,
                typeof(UdonApiNormalConstructorFixture));
            var byRef = GetTypeSource(
                result,
                typeof(UdonApiRefConstructorFixture));
            var byOut = GetTypeSource(
                result,
                typeof(UdonApiOutConstructorFixture));
            var mixed = GetTypeSource(
                result,
                typeof(UdonApiMixedConstructorFixture));

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
        public void Generator_PreservesInheritedSurfacesAndDeduplicatesPhysicalApi()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiInheritedChildBFixture),
                typeof(UdonApiInheritedChildAFixture)
            });
            var childA = GetTypeSource(
                result,
                typeof(UdonApiInheritedChildAFixture));
            var childB = GetTypeSource(
                result,
                typeof(UdonApiInheritedChildBFixture));
            var foo = typeof(UdonApiInheritedParentFixture).GetMethod("Foo");
            var signature = UdonExternSignatureFormatter.GetUdonMethodName(foo);
            var physical = FindPhysical(result.Report, signature);

            Assert.That(childA, Does.Contain("pub fn foo()"));
            Assert.That(childB, Does.Contain("pub fn foo()"));
            Assert.That(physical.clr_declaring_type,
                Is.EqualTo(typeof(UdonApiInheritedParentFixture).FullName));
            Assert.That(physical.surface_types, Is.EqualTo(new[]
            {
                typeof(UdonApiInheritedChildAFixture).FullName,
                typeof(UdonApiInheritedChildBFixture).FullName
            }));
            Assert.That(physical.generated_surface_types,
                Is.EqualTo(physical.surface_types));
            Assert.That(result.Report.udon_api.FindAll(record =>
                record.extern_signature == signature), Has.Count.EqualTo(1));
            Assert.That(result.Report.udon_signatures_exposed,
                Is.EqualTo(result.Report.udon_api.FindAll(record =>
                    record.is_udon_exposed).Count));

            var eventFailures = result.Report.skipped_members.FindAll(record =>
                string.IsNullOrEmpty(record.extern_signature) &&
                record.full_name.EndsWith(".Changed", StringComparison.Ordinal));
            Assert.That(eventFailures, Has.Count.EqualTo(2));
            Assert.That(result.Report.udon_api.Exists(record =>
                string.IsNullOrEmpty(record.extern_signature)), Is.False);

            var physicalSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in result.Report.udon_api)
            {
                Assert.That(record.extern_signature, Is.Not.Empty);
                Assert.That(physicalSignatures.Add(record.extern_signature), Is.True,
                    record.extern_signature);
            }
            var skippedPhysicalSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in result.Report.skipped_members)
            {
                if (string.IsNullOrEmpty(record.extern_signature))
                    continue;
                Assert.That(skippedPhysicalSignatures.Add(record.extern_signature),
                    Is.True,
                    record.extern_signature);
            }
        }

        [Test]
        public void Generator_CoversPhysicalApiWhenAnyInheritedSurfaceGenerates()
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiInheritedChildBFixture).FullName,
                    placement = "top_level"
                }
            };
            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiInheritedChildAFixture),
                typeof(UdonApiInheritedChildBFixture)
            });
            var signature = UdonExternSignatureFormatter.GetUdonMethodName(
                typeof(UdonApiInheritedParentFixture).GetMethod("Foo"));
            var physical = FindPhysical(result.Report, signature);
            var skipped = result.Report.skipped_members.Find(record =>
                record.extern_signature == signature);

            Assert.That(physical.is_covered, Is.True);
            Assert.That(physical.generated_surface_types, Is.EqualTo(new[]
            {
                typeof(UdonApiInheritedChildAFixture).FullName
            }));
            Assert.That(skipped, Is.Not.Null);
            Assert.That(skipped.surface_failures.Exists(failure =>
                failure.surface_type ==
                    typeof(UdonApiInheritedChildBFixture).FullName), Is.True);
        }

        [Test]
        public void Generator_SeparatesUdonExposureFromGenericSupportAndCalculatesCoverage()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiGenericCoverageFixture)
            });
            var exposedSignature = UdonExternSignatureFormatter.GetUdonMethodName(
                typeof(UdonApiGenericCoverageFixture).GetMethod("ExposedGeneric"));
            var unexposedSignature = UdonExternSignatureFormatter.GetUdonMethodName(
                typeof(UdonApiGenericCoverageFixture).GetMethod("UnexposedGeneric"));
            var exposed = FindPhysical(result.Report, exposedSignature);
            var unexposed = FindPhysical(result.Report, unexposedSignature);

            Assert.That(exposed.is_udon_exposed, Is.True);
            Assert.That(exposed.is_covered, Is.False);
            Assert.That(exposed.reasons, Has.Some.Contains("Generic methods"));
            Assert.That(unexposed.is_udon_exposed, Is.False);
            Assert.That(unexposed.is_covered, Is.False);
            Assert.That(result.Report.udon_signatures_exposed, Is.EqualTo(2));
            Assert.That(result.Report.udon_signatures_covered, Is.EqualTo(1));
            Assert.That(result.Report.udon_signatures_unsupported, Is.EqualTo(1));
            Assert.That(result.Report.udon_signatures_exposed, Is.EqualTo(
                result.Report.udon_signatures_covered +
                result.Report.udon_signatures_unsupported));
            Assert.That(result.Report.udon_api_coverage_percent, Is.EqualTo(50.0));
            Assert.That(result.Report.skipped_members.FindAll(record =>
                record.extern_signature == exposedSignature), Has.Count.EqualTo(1));
            Assert.That(result.Report.udon_unsupported_reasons.Exists(reason =>
                reason.reason.Contains("Generic methods") && reason.count == 1),
                Is.True);
            Assert.That(result.Files[UdonApiStubGenerator.ReportFileName],
                Does.Contain("\"udon_signatures_exposed\": 2"));
            Assert.That(result.Files[UdonApiStubGenerator.ReportFileName],
                Does.Contain("\"is_udon_exposed\": true"));
        }

        [Test]
        public void Generator_UsesZeroCoverageWhenNoPhysicalSignatureIsExposed()
        {
            var result = CreateGenerator(exposure: new NoMemberExposure()).Generate(
                new[] { typeof(UdonApiGenericCoverageFixture) });

            Assert.That(result.Report.udon_signatures_exposed, Is.Zero);
            Assert.That(result.Report.udon_signatures_covered, Is.Zero);
            Assert.That(result.Report.udon_signatures_unsupported, Is.Zero);
            Assert.That(result.Report.udon_api_coverage_percent, Is.EqualTo(0.0));
            Assert.That(double.IsNaN(result.Report.udon_api_coverage_percent),
                Is.False);
            Assert.That(double.IsInfinity(result.Report.udon_api_coverage_percent),
                Is.False);
        }

        [Test]
        public void Generator_ReportsUnmatchedExposedNodesOutsideCoverageDenominator()
        {
            const string unmatched = "Unmatched.__Node__SystemVoid";
            var exposure = new FixtureExposure(new[] { unmatched });
            var result = CreateGenerator(exposure: exposure).Generate(new[]
            {
                typeof(UdonApiGenericCoverageFixture)
            });

            Assert.That(result.Report.udon_exposed_unmatched_signatures,
                Is.EqualTo(new[] { unmatched }));
            Assert.That(result.Report.udon_exposed_unmatched_signatures_count,
                Is.EqualTo(1));
            Assert.That(result.Report.udon_signatures_exposed, Is.EqualTo(2));
            Assert.That(result.Report.udon_api.Exists(record =>
                record.extern_signature == unmatched), Is.False);
        }

        [Test]
        public void Generator_ProducesDeterministicFilesAndOrdering()
        {
            var generator = CreateGenerator();
            var first = generator.Generate(new[]
            {
                typeof(UdonApiStaticFixture),
                typeof(UdonApiStructFixture),
                typeof(UdonApiStubGeneratorFixture),
                typeof(int)
            });
            var second = generator.Generate(new[]
            {
                typeof(UdonApiStubGeneratorFixture),
                typeof(UdonApiStructFixture),
                typeof(UdonApiStaticFixture),
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
                    "external",
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

        private static UdonApiStubGenerator CreateGenerator(
            UdonApiStubGenerationConfig configuration = null,
            IUdonApiExposure exposure = null)
        {
            var formatter = new UdonApiStubTypeFormatter();
            exposure ??= new FixtureExposure();
            return new UdonApiStubGenerator(
                new UdonApiDiscovery(exposure, formatter),
                new SobakasuStubRenderer(formatter),
                configuration);
        }

        private static UdonApiStubGenerator CreateInstalledGenerator(
            UdonApiStubGenerationConfig configuration)
        {
            var formatter = new UdonApiStubTypeFormatter(
                SobakasuBuiltInEnvironment.Default.ExternCatalog);
            return new UdonApiStubGenerator(
                new UdonApiDiscovery(
                    new InstalledUdonApiExposure(UdonExposedNodeCache.Default),
                    formatter),
                new SobakasuStubRenderer(formatter),
                configuration);
        }

        private static UdonApiStubGenerationConfig CreateTypeNamespaceCollisionConfig(
            string parentNamespace,
            string childNamespace)
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStaticFixture).FullName,
                    @namespace = parentNamespace,
                    placement = "top_level",
                    name = "Deep"
                },
                new UdonApiStubTypeRule
                {
                    type = typeof(PolicyFixtures.Deep.DeepNamespaceFixture).FullName,
                    @namespace = childNamespace,
                    placement = "top_level"
                }
            };
            return config;
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

        private static void AssertParses(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser));
        }

        private static void AssertAllStubSourcesParse(
            UdonApiGenerationResult result)
        {
            foreach (var pair in result.Files)
            {
                if (!pair.Key.EndsWith(".sobakasu", StringComparison.Ordinal))
                    continue;
                AssertParses(pair.Value);
            }
        }

        private static void WithGeneratedLibrary(
            UdonApiGenerationResult result,
            Action<string> action)
        {
            var root = NewTemporaryPath();
            try
            {
                new UdonApiStubOutputWriter().Write(root, result);
                action(root);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static string GetFixtureSource(UdonApiGenerationResult result)
        {
            return GetTypeSource(result, typeof(UdonApiStubGeneratorFixture));
        }

        private static string GetTypeSource(
            UdonApiGenerationResult result,
            Type type)
        {
            var record = FindGeneratedType(result.Report, type);
            Assert.That(record.generated_file, Is.Not.Empty,
                $"The generated file for '{type.FullName}' is empty.");
            return GetSource(result, record.generated_file);
        }

        private static string GetSource(
            UdonApiGenerationResult result,
            string fileName)
        {
            if (result.Files.TryGetValue(fileName, out var source))
                return source;

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

        private static UdonApiPhysicalRecord FindPhysical(
            UdonApiGenerationReport report,
            string externSignature)
        {
            var record = report.udon_api.Find(candidate => string.Equals(
                candidate.extern_signature,
                externSignature,
                StringComparison.Ordinal));
            if (record != null)
                return record;

            Assert.Fail($"No physical Udon API record was found for '{externSignature}'.");
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
            private readonly HashSet<string> _exposedSignatures =
                new(StringComparer.Ordinal);
            private readonly string[] _fixturePrefixes =
            {
                GetPrefix(typeof(UdonApiStubGeneratorFixture)),
                GetPrefix(typeof(UdonApiInheritedParentFixture)),
                GetPrefix(typeof(UdonApiInheritedChildAFixture)),
                GetPrefix(typeof(UdonApiInheritedChildBFixture)),
                GetPrefix(typeof(UdonApiGenericCoverageFixture)),
                GetPrefix(typeof(UdonApiNormalConstructorFixture)),
                GetPrefix(typeof(UdonApiRefConstructorFixture)),
                GetPrefix(typeof(UdonApiOutConstructorFixture)),
                GetPrefix(typeof(UdonApiMixedConstructorFixture)),
                GetPrefix(typeof(UdonApiStructFixture)),
                GetPrefix(typeof(UdonApiStaticFixture)),
                GetPrefix(typeof(UdonApiStaticFixture2)),
                GetPrefix(typeof(UdonApiStaticCollisionFixture)),
                GetPrefix(typeof(PolicyFixtures.NamespaceFixture)),
                GetPrefix(typeof(PolicyFixtures.Deep.DeepNamespaceFixture))
            };

            public FixtureExposure(IEnumerable<string> exposedSignatures = null)
            {
                if (exposedSignatures == null)
                    return;
                foreach (var signature in exposedSignatures)
                    _exposedSignatures.Add(signature);
            }

            public IReadOnlyCollection<string> ExposedSignatures =>
                _exposedSignatures;

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
                            StringComparison.Ordinal) < 0 &&
                        externSignature.IndexOf(
                            "__UnexposedGeneric",
                            StringComparison.Ordinal) < 0)
                    {
                        _exposedSignatures.Add(externSignature);
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

        private sealed class NoMemberExposure : IUdonApiExposure
        {
            public IReadOnlyCollection<string> ExposedSignatures =>
                Array.Empty<string>();

            public bool IsTypeExposed(Type type)
            {
                return true;
            }

            public bool IsMemberExposed(string externSignature)
            {
                return false;
            }
        }

        [Test]
        public void Generator_ReExportsSplitStaticClassesAsTopLevelOverloadsAndPredicates()
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
            var firstSource = GetTypeSource(
                result,
                typeof(UdonApiStaticFixture));
            var secondSource = GetTypeSource(
                result,
                typeof(UdonApiStaticFixture2));
            var facade = GetSource(result, "math.sobakasu");

            Assert.That(firstSource, Does.Contain("pub fn abs(value: i32) -> i32"));
            Assert.That(firstSource, Does.Contain("pub fn abs(value: f32) -> f32"));
            Assert.That(secondSource, Does.Contain("pub fn abs(value: f64) -> f64"));
            Assert.That(firstSource, Does.Contain("pub fn ready?() -> bool"));
            Assert.That(firstSource, Does.Contain("pub fn active_and_enabled?() -> bool"));
            Assert.That(firstSource, Does.Contain("pub fn is_count() -> i32"));
            Assert.That(firstSource, Does.Contain("pub fn visible? -> bool"));
            Assert.That(firstSource, Does.Contain("pub fn set_visible(value: bool)"));
            Assert.That(firstSource, Does.Not.Contain("pub impl UdonApiStaticFixture"));
            Assert.That(facade, Does.Contain("mod udon_api_static_fixture;"));
            Assert.That(facade, Does.Contain("mod udon_api_static_fixture2;"));
            Assert.That(facade,
                Does.Contain("pub use udon_api_static_fixture.*;"));
            Assert.That(facade,
                Does.Contain("pub use udon_api_static_fixture2.*;"));
            Assert.That(result.Report.top_level_static_type_count, Is.EqualTo(2));
            Assert.That(result.Report.namespaces_generated, Is.EqualTo(1));

            AssertParses(firstSource);
            AssertParses(secondSource);
            AssertParses(facade);
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
            var constructorSource = GetTypeSource(
                constructorResult,
                typeof(UdonApiOutConstructorFixture));
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
            var source = GetTypeSource(result, typeof(UdonApiStaticFixture2));

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
            var source = GetTypeSource(result, fixtureType);

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
            Assert.That(result.Files.Keys,
                Does.Contain("exact/udon_api_static_fixture.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("fixtures.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("fixtures/deep.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("fixtures/namespace_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("fixtures/deep/deep_namespace_fixture.sobakasu"));
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
            AssertAllStubSourcesParse(result);
        }

        [Test]
        public void Generator_FlattensNamespaceAndSplitsTypeFiles()
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
            Assert.That(result.Files.Keys,
                Does.Contain("flat/namespace_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("flat/deep_namespace_fixture.sobakasu"));
            Assert.That(result.Files["flat/namespace_fixture.sobakasu"],
                Does.Contain("pub fn value() -> i32"));
            Assert.That(result.Files["flat/deep_namespace_fixture.sobakasu"],
                Does.Contain("pub fn deep_value() -> i32"));
            Assert.That(result.Files["flat.sobakasu"],
                Does.Contain("pub use namespace_fixture.*;"));
            Assert.That(result.Files["flat.sobakasu"],
                Does.Contain("pub use deep_namespace_fixture.*;"));
            AssertAllStubSourcesParse(result);
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
            var source = GetTypeSource(
                result,
                typeof(UdonApiStaticFixture)) +
                GetTypeSource(
                    result,
                    typeof(UdonApiStaticCollisionFixture));

            Assert.That(source, Does.Not.Contain("pub fn abs(value: i32)"));
            Assert.That(source, Does.Not.Contain("abs_2"));
            Assert.That(result.Report.declaration_collisions, Is.EqualTo(2));
            Assert.That(result.Report.skipped_members.Exists(record =>
                record.reason.Contains("same Sobakasu declaration")), Is.True);
        }

        [Test]
        public void Generator_RejectsSnakeCaseTypeModulePathCollisions()
        {
            var config = UdonApiStubGenerationConfig.CreateDefault();
            config.types = new[]
            {
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStaticFixture).FullName,
                    @namespace = "module_collision",
                    placement = "top_level",
                    name = "URLLoader"
                },
                new UdonApiStubTypeRule
                {
                    type = typeof(UdonApiStaticFixture2).FullName,
                    @namespace = "module_collision",
                    placement = "top_level",
                    name = "UrlLoader"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiStaticFixture),
                typeof(UdonApiStaticFixture2)
            });

            Assert.That(result.Report.types_generated, Is.Zero);
            Assert.That(result.Report.types_skipped, Is.EqualTo(2));
            Assert.That(result.Files.Keys,
                Does.Not.Contain("module_collision.sobakasu"));
            Assert.That(
                FindGeneratedType(result.Report, typeof(UdonApiStaticFixture))
                    .generated_file,
                Is.Empty);
            Assert.That(result.Report.skipped_types.TrueForAll(record =>
                record.reason.Contains("same generated type module path")), Is.True);
        }

        [Test]
        public void Generator_PrioritizesChildNamespaceFacadeOverTypeModule()
        {
            var config = CreateTypeNamespaceCollisionConfig(
                "path_collision",
                "path_collision.deep");

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiStaticFixture),
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)
            });

            Assert.That(result.Files["path_collision.sobakasu"],
                Is.EqualTo("pub mod deep;\n"));
            Assert.That(result.Files.Keys,
                Does.Contain("path_collision/deep.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain(
                    "path_collision/deep/deep_namespace_fixture.sobakasu"));
            Assert.That(
                FindGeneratedType(result.Report, typeof(UdonApiStaticFixture))
                    .generated_file,
                Is.Empty);
            Assert.That(
                FindGeneratedType(
                    result.Report,
                    typeof(PolicyFixtures.Deep.DeepNamespaceFixture))
                    .generated_file,
                Is.EqualTo(
                    "path_collision/deep/deep_namespace_fixture.sobakasu"));
            Assert.That(result.Report.skipped_types.Exists(record =>
                record.reason.Contains("namespace facade path")), Is.True);
        }

        [Test]
        public void Generator_RejectsCaseInsensitiveTypeAndNamespacePathCollision()
        {
            var config = CreateTypeNamespaceCollisionConfig(
                "case_collision",
                "case_collision.Deep");

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture),
                typeof(UdonApiStaticFixture)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("case_collision/Deep.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Not.Contain("case_collision/deep.sobakasu"));
            Assert.That(
                FindGeneratedType(result.Report, typeof(UdonApiStaticFixture))
                    .generated_file,
                Is.Empty);
            Assert.That(result.Report.skipped_types.Exists(record =>
                record.reason.Contains("namespace facade path")), Is.True);
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
            Assert.That(result.Files.Keys,
                Does.Contain("math/math.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("debug/debug.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("unity/game_object.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("vrc/utilities.sobakasu"));
            Assert.That(result.Report.rules_configured, Is.EqualTo(6));
            Assert.That(result.Report.rules_matched, Is.EqualTo(6));
        }
    }
}
