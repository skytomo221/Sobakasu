using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;

using static Skytomo221.Sobakasu.Tests.Editor.UdonBindingGeneratorTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class UdonBindingGeneratorRenderingTests
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
        public void ClrMemberId_FormatsReflectionAndDiscoveredMembersCanonically()
        {
            var type = typeof(UdonBindingGeneratorFixture);
            var prefix = type.FullName.Replace('+', '.');
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.FlattenHierarchy;

            Assert.That(ClrMemberId.Format(type.GetConstructor(Type.EmptyTypes)),
                Is.EqualTo(prefix + "()"));
            Assert.That(ClrMemberId.Format(type.GetConstructor(new[] { typeof(int) })),
                Is.EqualTo(prefix + "(System.Int32)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "Find", flags, null, new[] { typeof(string) }, null)),
                Is.EqualTo(prefix + ".Find(System.String)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "Mix", flags, null, new[] { typeof(int) }, null)),
                Is.EqualTo(prefix + ".Mix(System.Int32)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "Mix", flags, null, new[] { typeof(float) }, null)),
                Is.EqualTo(prefix + ".Mix(System.Single)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "ArrayValue", flags, null, new[] { typeof(string[]) }, null)),
                Is.EqualTo(prefix + ".ArrayValue(System.String[])"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "RefValue", flags, null,
                new[] { typeof(int).MakeByRefType() }, null)),
                Is.EqualTo(prefix + ".RefValue(System.Int32&)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "Nested", flags, null,
                new[] { typeof(UdonBindingGeneratorFixture.NestedValue) }, null)),
                Is.EqualTo(prefix + ".Nested(" + prefix + ".NestedValue)"));
            Assert.That(ClrMemberId.Format(type.GetProperty("Count")),
                Is.EqualTo(prefix + ".Count"));
            Assert.That(ClrMemberId.Format(type.GetField("Number")),
                Is.EqualTo(prefix + ".Number"));

            var formatter = new UdonBindingTypeFormatter();
            var discovered = new UdonApiDiscovery(
                new FixtureExposure(),
                formatter).Discover(new[] { type });
            UdonApiMemberModel discoveredMix = null;
            foreach (var member in discovered.Types[0].Members)
            {
                if (member.MemberName == "Mix" &&
                    member.Callable.GetParameters()[0].ParameterType == typeof(int))
                {
                    discoveredMix = member;
                    break;
                }
            }
            Assert.That(discoveredMix, Is.Not.Null);
            Assert.That(ClrMemberId.Format(discoveredMix),
                Is.EqualTo(prefix + ".Mix(System.Int32)"));
        }

        [Test]
        public void TypeFormatter_ReusesBuiltInsAndRejectsUnsupportedShapes()
        {
            var formatter = new UdonBindingTypeFormatter();

            AssertFormats(formatter, typeof(int), "i32");
            AssertFormats(formatter, typeof(long), "i64");
            AssertFormats(formatter, typeof(float), "f32");
            AssertFormats(formatter, typeof(double), "f64");
            AssertFormats(formatter, typeof(bool), "bool");
            AssertFormats(formatter, typeof(string), "string");
            AssertFormats(formatter, typeof(int[]), "[i32]");
            AssertFormats(formatter, typeof(int).MakeByRefType(), "i32");

            Assert.That(formatter.TryFormat(
                typeof(UdonBindingGeneratorFixture),
                typeof(UdonBindingGeneratorFixture),
                out var selfType,
                out _), Is.True);
            Assert.That(selfType, Is.EqualTo("Self"));

            Assert.That(formatter.TryFormat(
                typeof(int[,]),
                typeof(UdonBindingGeneratorFixture),
                out _,
                out var arrayReason), Is.False);
            Assert.That(arrayReason, Does.Contain("Array shape"));

            AssertFormats(formatter, typeof(List<int>),
                "System.Collections.Generic.List<i32>");
            var genericParameter = typeof(UdonBindingGeneratorFixture)
                .GetMethod("Generic").GetGenericArguments()[0];
            Assert.That(formatter.TryFormat(
                genericParameter.MakeArrayType(),
                typeof(UdonBindingGeneratorFixture),
                out var genericArray,
                out _), Is.True);
            Assert.That(genericArray, Is.EqualTo("[T]"));

            Assert.That(formatter.TryFormat(
                typeof(int).MakePointerType(),
                typeof(UdonBindingGeneratorFixture),
                out _,
                out var pointerReason), Is.False);
            Assert.That(pointerReason, Does.Contain("Pointer type"));
        }

        [Test]
        public void Generator_RendersCanonicalPrimitiveAsLanguageItemImpl()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.lang = new[]
            {
                new UdonBindingLangRule
                {
                    from = "System.Int64",
                    item = "i64"
                }
            };

            var result = CreateGenerator(config, new NoMemberExposure()).Generate(new[]
            {
                typeof(long),
                typeof(object)
            });
            var source = GetTypeSource(result, typeof(long));
            var generatedType = FindGeneratedType(result.Report, typeof(long));

            Assert.That(generatedType.placement, Is.EqualTo("impl"));
            Assert.That(source, Does.StartWith(
                "lang \"i64\"\npub impl i64 = extern System.Int64"));
            Assert.That(source, Does.Not.Contain(
                "pub struct i64 = extern System.Int64"));
            Assert.That(result.Files["external.sobakasu"],
                Does.Contain("mod i64_binding;")
                    .And.Not.Contain("pub use i64_binding.i64;"));
            Assert.That(result.Report.skipped_types.Exists(record =>
                record.clr_declaring_type == "System.Object"), Is.True);
            Assert.That(result.Report.rules_configured, Is.EqualTo(1));
            Assert.That(result.Report.rules_matched, Is.EqualTo(1));
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void TypeFormatter_AllowsCanonicalPrimitivesWithInstalledCatalog()
        {
            var formatter = new UdonBindingTypeFormatter(
                SobakasuBuiltInEnvironment.Default.ExternCatalog);

            Assert.That(formatter.CanDeclareType(typeof(long), out var reason),
                Is.True, reason);
            Assert.That(formatter.CanDeclareType(typeof(object), out _), Is.False);
        }

        [Test]
        public void Generator_SplitsGeneratedTypesAndReExportsThemFromFacade()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiStaticFixture),
                typeof(UdonApiStructFixture),
                typeof(UdonBindingGeneratorFixture)
            });

            Assert.That(result.Files.Keys, Does.Contain("external.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("external/udon_api_static_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("external/udon_api_struct_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("external/udon_binding_generator_fixture.sobakasu"));

            var facade = result.Files["external.sobakasu"];
            Assert.That(facade, Is.EqualTo(
                "mod udon_api_static_fixture;\n" +
                "mod udon_api_struct_fixture;\n" +
                "mod udon_binding_generator_fixture;\n" +
                "\n" +
                "pub use udon_api_static_fixture;\n" +
                "pub use udon_api_struct_fixture.UdonApiStructFixture;\n" +
                "pub use udon_binding_generator_fixture.UdonBindingGeneratorFixture;\n"));
            Assert.That(GetTypeSource(result, typeof(UdonApiStructFixture)),
                Does.StartWith("pub struct UdonApiStructFixture = extern")
                    .And.Contain("value: i32 = extern Value,"));
            Assert.That(GetTypeSource(result, typeof(UdonBindingGeneratorFixture)),
                Does.Not.Contain("UdonApiStructFixture"));
            Assert.That(
                FindGeneratedType(result.Report, typeof(UdonApiStructFixture))
                    .generated_file,
                Is.EqualTo("external/udon_api_struct_fixture.sobakasu"));
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void Generator_RendersExternalEnumMembersWithoutCopyingNumbers()
        {
            var result = CreateGenerator().Generate(new[] { typeof(UdonApiEnumFixture) });
            var source = GetTypeSource(result, typeof(UdonApiEnumFixture));

            Assert.That(source, Does.StartWith("pub enum UdonApiEnumFixture = extern"));
            Assert.That(source, Does.Contain("First = extern First,")
                .And.Contain("Alias = extern Alias,")
                .And.Contain("Second = extern Second,"));
            Assert.That(source, Does.Not.Contain("= 10").And.Not.Contain("= 20"));
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void Generator_ProjectsOperatorsToFirstOperandAndPreservesPhysicalIdentity()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiOperatorFixture),
                typeof(float)
            });
            var hostSource = GetTypeSource(result, typeof(float));
            var declaringSource = GetTypeSource(
                result,
                typeof(UdonApiOperatorFixture));
            var multiply = typeof(UdonApiOperatorFixture).GetMethod(
                "op_Multiply",
                BindingFlags.Public | BindingFlags.Static);
            var externSignature =
                UdonExternSignatureFormatter.GetUdonMethodName(multiply);
            var physical = FindPhysical(result.Report, externSignature);

            Assert.That(hostSource, Does.Contain("pub fn *(rhs: ")
                .And.Contain("= extern self * rhs"));
            Assert.That(declaringSource, Does.Contain("pub fn @-")
                .And.Contain("= extern -self")
                .And.Contain("pub fn @~")
                .And.Contain("= extern ~self"));
            Assert.That(declaringSource, Does.Not.Contain("fn op_"));
            Assert.That(physical.clr_declaring_type,
                Is.EqualTo(typeof(UdonApiOperatorFixture).FullName));
            Assert.That(physical.generated_surface_types,
                Does.Contain(typeof(float).FullName));

            foreach (var unsupportedName in new[]
                     {
                         "op_Implicit",
                         "op_Explicit",
                         "op_Increment",
                         "op_Decrement"
                     })
            {
                Assert.That(result.Report.skipped_members.Exists(record =>
                    record.full_name.EndsWith(
                        "." + unsupportedName,
                        StringComparison.Ordinal)), Is.True);
            }
            Assert.That(result.Report.skipped_members.Exists(record =>
                record.reason.IndexOf(
                    "Conversion operators are outside",
                    StringComparison.Ordinal) >= 0), Is.True);
            Assert.That(result.Report.skipped_members.Exists(record =>
                record.reason.IndexOf(
                    "Increment and decrement operators are outside",
                    StringComparison.Ordinal) >= 0), Is.True);

            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void Generator_DiscoversUdonOnlyPrimitiveOperators()
        {
            var signatures = new[]
            {
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(int),
                    "op_Addition",
                    new[] { typeof(int), typeof(int) },
                    typeof(int)),
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(int),
                    "op_UnaryNegation",
                    new[] { typeof(int) },
                    typeof(int)),
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(int),
                    "op_OnesComplement",
                    new[] { typeof(int) },
                    typeof(int)),
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(float),
                    "op_Addition",
                    new[] { typeof(float), typeof(float) },
                    typeof(float)),
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(float),
                    "op_UnaryNegation",
                    new[] { typeof(float) },
                    typeof(float))
            };
            var result = CreateGenerator(
                exposure: new FixtureExposure(signatures)).Generate(new[]
            {
                typeof(int),
                typeof(float)
            });
            var integerSource = GetTypeSource(result, typeof(int));
            var floatSource = GetTypeSource(result, typeof(float));

            Assert.That(integerSource, Does.Contain("pub impl i32 = extern System.Int32")
                .And.Contain("pub fn +(rhs: Self) -> Self")
                .And.Contain("= extern self + rhs")
                .And.Contain("pub fn @- -> Self")
                .And.Contain("pub fn @~ -> Self"));
            Assert.That(floatSource, Does.Contain("pub impl f32 = extern System.Single")
                .And.Contain("pub fn +(rhs: Self) -> Self")
                .And.Contain("pub fn @- -> Self"));
            foreach (var signature in signatures)
            {
                var physical = FindPhysical(result.Report, signature);
                var expectedType = signature.StartsWith(
                    UdonExternSignatureFormatter.GetUdonTypeName(typeof(float)) + ".",
                    StringComparison.Ordinal)
                    ? typeof(float)
                    : typeof(int);
                Assert.That(physical.clr_declaring_type,
                    Is.EqualTo(expectedType.FullName));
                Assert.That(physical.generated_surface_types,
                    Does.Contain(expectedType.FullName));
            }
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void Generator_HoistsNestedStructAndEnumAndPreservesRuntimeIdentity()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiNestedOuterFixture.NestedValue),
                typeof(UdonApiNestedOuterFixture.NestedEnum)
            });
            var valueSource = GetTypeSource(result, typeof(UdonApiNestedOuterFixture.NestedValue));
            var enumSource = GetTypeSource(result, typeof(UdonApiNestedOuterFixture.NestedEnum));

            Assert.That(valueSource, Does.StartWith("pub struct NestedValue = extern ")
                .And.Contain("UdonApiNestedOuterFixture.NestedValue"));
            Assert.That(enumSource, Does.StartWith("pub enum NestedEnum = extern ")
                .And.Contain("UdonApiNestedOuterFixture.NestedEnum"));
            Assert.That(valueSource, Does.Not.Contain("struct UdonApiNestedOuterFixture"));
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void InstalledGenerator_NestedRuntimeTypeBindsAgainstInstalledCatalog()
        {
            var result = UdonBindingGenerator.CreateDefault()
                .Generate(new[] { typeof(UnityEngine.ParticleSystem.Burst) });
            var source = GetTypeSource(result, typeof(UnityEngine.ParticleSystem.Burst));
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
        public void Generator_ReportsNestedHoistCollisionAndAcceptsExplicitRename()
        {
            var types = new[]
            {
                typeof(UdonApiNestedCollisionA.Value),
                typeof(UdonApiNestedCollisionB.Value)
            };
            Assert.That(() => CreateGenerator().Generate(types),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("require generated module path"));

            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.types = new[]
            {
                new UdonBindingTypeRenameRule
                {
                    from = ClrMemberId.GetClrTypeName(typeof(UdonApiNestedCollisionB.Value)),
                    to = "OtherValue"
                }
            };
            var result = CreateGenerator(config).Generate(types);
            Assert.That(result.Files.Keys, Has.Some.Contains("other_value"));
        }

        [Test]
        public void Generator_UsesFinalWrapperNameForTypeModulePath()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = typeof(UdonBindingGeneratorFixture).Namespace,
                    to = "renamed"
                }
            };
            config.renames.types = new[]
            {
                new UdonBindingTypeRenameRule
                {
                    from = typeof(UdonBindingGeneratorFixture).FullName,
                    to = "URLLoader"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("renamed/url_loader.sobakasu"));
            Assert.That(result.Files["renamed.sobakasu"],
                Does.Contain("pub use url_loader.URLLoader;"));
            Assert.That(result.Files["renamed/url_loader.sobakasu"],
                Does.StartWith("pub impl URLLoader = extern"));
            Assert.That(
                FindGeneratedType(result.Report, typeof(UdonBindingGeneratorFixture))
                    .generated_file,
                Is.EqualTo("renamed/url_loader.sobakasu"));
        }

        [Test]
        public void Generator_RendersSupportedMemberKindsAndSnakeCaseNames()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture)
            });
            var source = GetFixtureSource(result);

            Assert.That(source, Does.Contain("pub impl UdonBindingGeneratorFixture = extern"));
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
            var result = UdonBindingGenerator.CreateDefault()
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
        public void InstalledGenerator_SkipsDeclarationsTheCompilerCannotBind()
        {
            var result = UdonBindingGenerator.CreateDefault()
                .Generate(new[] { typeof(UnityEngine.AnimatorStateInfo) });
            var source = GetTypeSource(
                result,
                typeof(UnityEngine.AnimatorStateInfo));

            Assert.That(source, Does.Not.Contain("extern self.loop"));
            Assert.That(FindSkip(result.Report, "loop").reason,
                Does.Contain("member-access syntax"));
        }

        [Test]
        public void Generator_AvoidsCaseInsensitiveImplModuleTypeCollisions()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.types = new[]
            {
                new UdonBindingTypeRenameRule
                {
                    from = typeof(UdonApiStructFixture).FullName,
                    to = "Fixture"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiStructFixture)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("external/fixture_binding.sobakasu"));
            Assert.That(result.Files["external.sobakasu"],
                Does.Contain("mod fixture_binding;")
                    .And.Contain("pub use fixture_binding.Fixture;"));
        }

        [Test]
        public void Generator_ImportsMaybeForProjectedDeclarations()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.maybe.returns = new[]
            {
                MemberRule(
                    typeof(UdonBindingGeneratorFixture),
                    "static_method",
                    "Find",
                    new[] { typeof(string) },
                    returnProjection: "maybe")
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture)
            });
            var source = GetFixtureSource(result);

            Assert.That(source, Does.StartWith("use maybe.Maybe;\n\n"));
            Assert.That(source, Does.Contain("-> Maybe<Self>"));
        }

        [Test]
        public void GeneratedImplFacade_PreservesLogicalTypeApiThroughCompiler()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = "UnityEngine",
                    to = "unity"
                }
            };
            config.excludes.members = new[]
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
        public void GeneratedStaticClasses_UseDistinctPublicModules()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = "System",
                    to = "system"
                },
                new UdonBindingNamespaceRenameRule
                {
                    from = "UnityEngine",
                    to = "unity"
                }
            };
            var result = CreateInstalledGenerator(config).Generate(new[]
            {
                typeof(UnityEngine.Mathf),
                typeof(Math)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("system/math.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("unity/mathf.sobakasu"));
            Assert.That(result.Files["system.sobakasu"],
                Does.Contain("mod math;\n\npub use math;"));
            Assert.That(result.Files["unity.sobakasu"],
                Does.Contain("mod mathf;\n\npub use mathf;"));

            WithGeneratedLibrary(result, root =>
            {
                var compilation = SobakasuCompiler.CompileToUasm(
                    @"use system.math;
use unity.mathf;
on interact {
  math.round(1.25f64);
  mathf.round(1.25f32);
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
                typeof(UdonBindingGeneratorFixture)
            });
            var source = GetFixtureSource(result);

            Assert.That(source, Does.Contain("pub fn mix(value: i32) -> i32"));
            Assert.That(source, Does.Contain("pub fn mix(value: f32) -> f32"));
            Assert.That(CountOccurrences(source, "pub fn mix("), Is.EqualTo(2));
        }

        [Test]
        public void Generator_RenamesExactOverloadPropertyAndField()
        {
            var type = typeof(UdonBindingGeneratorFixture);
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.members = new[]
            {
                new UdonBindingMemberRenameRule
                {
                    from = MemberRule(
                        type,
                        "instance_method",
                        "Mix",
                        new[] { typeof(int) }),
                    to = "mix_integer"
                },
                new UdonBindingMemberRenameRule
                {
                    from = ClrMemberId.Format(type.GetProperty("Count")),
                    to = "amount"
                },
                new UdonBindingMemberRenameRule
                {
                    from = ClrMemberId.Format(type.GetField("Number")),
                    to = "value"
                }
            };

            var source = GetFixtureSource(CreateGenerator(config).Generate(
                new[] { type }));

            Assert.That(source,
                Does.Contain("pub fn mix_integer(value: i32) -> i32"));
            Assert.That(source,
                Does.Contain("pub fn mix(value: f32) -> f32"));
            Assert.That(source, Does.Contain("pub fn amount -> i32"));
            Assert.That(source, Does.Contain("pub fn amount(value: i32)"));
            Assert.That(source, Does.Contain("pub fn value -> i32"));
            Assert.That(source, Does.Contain("pub fn value(value: i32)"));
        }

        [Test]
        public void Generator_ReportsHiddenAndUnsupportedMembers()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture)
            });
            var source = GetFixtureSource(result);

            Assert.That(source, Does.Not.Contain("fn hidden"));
            Assert.That(source,
                Does.Contain("pub fn generic<T>(value: T) -> T"));
            Assert.That(source,
                Does.Contain("= extern self.Generic<T>(value)"));
            Assert.That(source,
                Does.Contain("pub fn generic_array<T>() -> [T]"));
            Assert.That(source,
                Does.Contain("values: System.Collections.Generic.List<T>"));
            Assert.That(FindSkip(result.Report, "Hidden").reason,
                Does.Contain("not exposed to Udon"));
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
                typeof(UdonBindingGeneratorFixture),
                typeof(int)
            });
            var report = result.Report;

            Assert.That(report.types_discovered,
                Is.EqualTo(report.types_generated + report.types_skipped));
            Assert.That(report.members_discovered,
                Is.EqualTo(report.members_generated + report.members_skipped));
            Assert.That(report.types_discovered, Is.EqualTo(2));
            Assert.That(report.types_generated, Is.EqualTo(2));
            Assert.That(report.types_skipped, Is.Zero);
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
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.excludes.types = new[]
            {
                typeof(UdonApiInheritedChildBFixture).FullName
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
            Assert.That(exposed.is_covered, Is.True);
            Assert.That(unexposed.is_udon_exposed, Is.False);
            Assert.That(unexposed.is_covered, Is.False);
            Assert.That(result.Report.udon_signatures_exposed, Is.EqualTo(2));
            Assert.That(result.Report.udon_signatures_covered, Is.EqualTo(2));
            Assert.That(result.Report.udon_signatures_unsupported, Is.Zero);
            Assert.That(result.Report.udon_signatures_exposed, Is.EqualTo(
                result.Report.udon_signatures_covered +
                result.Report.udon_signatures_unsupported));
            Assert.That(result.Report.udon_api_coverage_percent, Is.EqualTo(100.0));
            Assert.That(result.Report.skipped_members.FindAll(record =>
                record.extern_signature == exposedSignature), Is.Empty);
            Assert.That(result.Diagnostics[UdonBindingGenerator.ReportFileName],
                Does.Contain("\"udon_signatures_exposed\": 2"));
            Assert.That(result.Diagnostics[UdonBindingGenerator.ReportFileName],
                Does.Contain("\"is_udon_exposed\": true"));
        }

        [Test]
        public void InstalledSdk_GenericComponentQueriesAreRepresentableWhenUdonExposed()
        {
            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                "GetComponent",
                "GetComponentInChildren",
                "GetComponentInParent",
                "GetComponents",
                "GetComponentsInChildren",
                "GetComponentsInParent"
            };
            var formatter = new UdonBindingTypeFormatter();
            var exposed = UdonExposedNodeCache.Default;
            var represented = 0;
            foreach (var declaringType in new[]
            {
                typeof(UnityEngine.Component),
                typeof(UnityEngine.GameObject)
            })
                foreach (var method in declaringType.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!method.IsGenericMethodDefinition || !names.Contains(method.Name))
                        continue;
                    var signature = UdonExternSignatureFormatter.GetUdonMethodName(method);
                    if (!exposed.IsExposed(signature))
                        continue;

                    Assert.That(ReflectionExternCatalogBuilder.TryGetUnsupportedMethodReason(
                        method, out var reason), Is.False, reason);
                    Assert.That(formatter.TryFormat(method.ReturnType, declaringType,
                        out _, out reason), Is.True, reason);
                    foreach (var parameter in method.GetParameters())
                    {
                        Assert.That(formatter.TryFormat(parameter.ParameterType,
                            declaringType, out _, out reason), Is.True, reason);
                    }
                    represented++;
                }

            Assert.That(represented, Is.GreaterThan(0),
                "The installed SDK exposed no generic Component/GameObject query nodes.");
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
                typeof(UdonBindingGeneratorFixture),
                typeof(int)
            });
            var second = generator.Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture),
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
        public void Generator_SeparatesDiagnosticsFromBindingSources()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("external/udon_binding_generator_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Not.Contain(UdonBindingGenerator.ReportFileName));
            Assert.That(result.Files.Keys,
                Does.Not.Contain(UdonBindingGenerator.SkippedMembersFileName));
            Assert.That(result.Diagnostics.Keys,
                Does.Contain(UdonBindingGenerator.ReportFileName));
            Assert.That(result.Diagnostics.Keys,
                Does.Contain(UdonBindingGenerator.SkippedMembersFileName));
        }

        [Test]
        public void Generator_ReExportsSplitStaticClassesAsTopLevelOverloadsAndPredicates()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = typeof(UdonApiStaticFixture).Namespace,
                    to = "math"
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
            Assert.That(facade,
                Does.Contain("mod udon_api_static_fixture;\n" +
                    "mod udon_api_static_fixture2;\n\n" +
                    "pub use udon_api_static_fixture;\n" +
                    "pub use udon_api_static_fixture2;"));
            Assert.That(result.Report.top_level_static_type_count, Is.EqualTo(2));
            Assert.That(result.Report.namespaces_generated, Is.EqualTo(1));

            AssertParses(firstSource);
            AssertParses(secondSource);
            AssertParses(facade);
        }

        [Test]
        public void Generator_AppliesMaybeReturnMaybeOutAndConstructorProjection()
        {
            var fixtureType = typeof(UdonBindingGeneratorFixture);
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.maybe.returns = new[]
            {
                MemberRule(
                    fixtureType,
                    "static_method",
                    "Find",
                    new[] { typeof(string) })
            };
            config.maybe.outs = new[]
            {
                new UdonBindingMaybeOutRule
                {
                    member = MemberRule(
                    fixtureType,
                    "instance_method",
                    "RefOut",
                    new[]
                    {
                        typeof(int).MakeByRefType(),
                        typeof(string).MakeByRefType()
                    }),
                    parameters = new[] { "text" }
                },
                new UdonBindingMaybeOutRule
                {
                    member = MemberRule(
                    fixtureType,
                    "instance_method",
                    "OutReference",
                    new[] { fixtureType.MakeByRefType() }),
                    parameters = new[] { "value" }
                }
            };

            var result = CreateGenerator(config).Generate(new[] { fixtureType });
            var source = GetFixtureSource(result);

            Assert.That(source,
                Does.Contain("pub static fn find(name: string) -> Maybe<Self>"));
            Assert.That(source,
                Does.Contain("pub static fn find(id: i32) -> Self"));
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
            Assert.That(result.Diagnostics[UdonBindingGenerator.ReportFileName],
                Does.Contain("\"maybe_return_count\": 1"));
            Assert.That(result.Diagnostics[UdonBindingGenerator.ReportFileName],
                Does.Contain("\"sobakasu_namespace\": \"external\""));

            var constructorConfig = UdonBindingGenerationConfig.CreateDefault();
            constructorConfig.maybe.outs = new[]
            {
                new UdonBindingMaybeOutRule
                {
                    member = MemberRule(
                    typeof(UdonApiOutConstructorFixture),
                    "constructor",
                    ".ctor",
                    new[] { typeof(string).MakeByRefType() }),
                    parameters = new[] { "name" }
                }
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
        public void Generator_AlwaysPublishesStaticClassAsModule()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = typeof(UdonApiStaticFixture2).Namespace,
                    to = "utility"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiStaticFixture2)
            });
            var source = GetTypeSource(result, typeof(UdonApiStaticFixture2));

            Assert.That(source,
                Does.Not.Contain("pub impl UdonApiStaticFixture2 = extern"));
            Assert.That(source,
                Does.Contain("pub fn abs(value: f64) -> f64"));
            Assert.That(result.Files["utility.sobakasu"],
                Does.Contain("mod udon_api_static_fixture2;\n\n" +
                    "pub use udon_api_static_fixture2;"));
            Assert.That(result.Report.impl_type_count, Is.Zero);
            Assert.That(result.Report.top_level_static_type_count, Is.EqualTo(1));
        }

        [Test]
        public void Generator_UsesExplicitRenameAndExclusionBeforeAutomaticNaming()
        {
            var fixtureType = typeof(UdonApiStaticFixture);
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.members = new[]
            {
                new UdonBindingMemberRenameRule
                {
                    from = MemberRule(
                        fixtureType,
                        "static_method",
                        "IsReady",
                        Array.Empty<Type>()),
                    to = "available?"
                }
            };
            config.excludes.members = new[]
            {
                MemberRule(
                    fixtureType,
                    "static_method",
                    "IsCount",
                    Array.Empty<Type>())
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
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = rootNamespace,
                    to = "root_api"
                },
                new UdonBindingNamespaceRenameRule
                {
                    from = policyNamespace,
                    to = "fixtures"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture),
                typeof(UdonApiStaticFixture),
                typeof(PolicyFixtures.NamespaceFixture)
            });

            Assert.That(result.Files.Keys, Does.Contain("root_api.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("root_api/udon_api_static_fixture.sobakasu"));
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
                .sobakasu_namespace, Is.EqualTo("root_api"));
            Assert.That(FindGeneratedType(
                result.Report,
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture))
                .sobakasu_namespace, Is.EqualTo("fixtures.deep"));
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void Generator_PromotesRelativeNamespacesAndBaseTypesToTheRoot()
        {
            var rootNamespace = typeof(PolicyFixtures.NamespaceFixture).Namespace;
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = rootNamespace,
                    to = null
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(PolicyFixtures.NamespaceFixture),
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("namespace_fixture.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("deep.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("deep/deep_namespace_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Not.Contain("/namespace_fixture.sobakasu"));
            Assert.That(FindGeneratedType(
                result.Report,
                typeof(PolicyFixtures.NamespaceFixture)).sobakasu_namespace,
                Is.Empty);
            Assert.That(FindGeneratedType(
                result.Report,
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)).sobakasu_namespace,
                Is.EqualTo("deep"));
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void Generator_RemovesEntireMatchedNamespacePrefix()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = typeof(PolicyFixtures.Deep.DeepNamespaceFixture).Namespace,
                    to = null
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("deep_namespace_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Not.Contain("/deep_namespace_fixture.sobakasu"));
            Assert.That(FindGeneratedType(
                result.Report,
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)).sobakasu_namespace,
                Is.Empty);
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void Generator_PreservesNormalizedNamespaceSuffixes()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = typeof(PolicyFixtures.NamespaceFixture).Namespace,
                    to = "flat"
                }
            };

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(PolicyFixtures.NamespaceFixture),
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)
            });

            Assert.That(result.Files.Keys, Does.Contain("flat.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("flat/deep.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("flat/namespace_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("flat/deep/deep_namespace_fixture.sobakasu"));
            Assert.That(result.Files["flat/namespace_fixture.sobakasu"],
                Does.Contain("pub fn value() -> i32"));
            Assert.That(result.Files["flat/deep/deep_namespace_fixture.sobakasu"],
                Does.Contain("pub fn deep_value() -> i32"));
            Assert.That(result.Files["flat.sobakasu"],
                Does.Contain("mod namespace_fixture;"));
            Assert.That(result.Files["flat.sobakasu"],
                Does.Contain("pub mod deep;"));
            Assert.That(result.Files["flat.sobakasu"],
                Does.Contain("pub use namespace_fixture;"));
            Assert.That(result.Files["flat/deep.sobakasu"],
                Does.Contain("mod deep_namespace_fixture;\n\n" +
                    "pub use deep_namespace_fixture;"));
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void Generator_RejectsPostPolicyMemberCollisionWithoutRenaming()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.members = new[]
            {
                new UdonBindingMemberRenameRule
                {
                    from = MemberRule(
                        typeof(UdonApiStaticFixture),
                        "static_method",
                        "IsReady",
                        Array.Empty<Type>()),
                    to = "same"
                },
                new UdonBindingMemberRenameRule
                {
                    from = MemberRule(
                        typeof(UdonApiStaticFixture),
                        "static_method",
                        "IsCount",
                        Array.Empty<Type>()),
                    to = "same"
                }
            };

            Assert.That(
                () => CreateGenerator(config).Generate(new[]
                {
                    typeof(UdonApiStaticFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("same Sobakasu declaration"));
        }

        [Test]
        public void Generator_RejectsSnakeCaseTypeModulePathCollisions()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = typeof(UdonApiStaticFixture).Namespace,
                    to = "module_collision"
                }
            };
            config.renames.types = new[]
            {
                new UdonBindingTypeRenameRule
                {
                    from = typeof(UdonApiStaticFixture).FullName,
                    to = "URLLoader"
                },
                new UdonBindingTypeRenameRule
                {
                    from = typeof(UdonApiStaticFixture2).FullName,
                    to = "UrlLoader"
                }
            };

            Assert.That(
                () => CreateGenerator(config).Generate(new[]
                {
                    typeof(UdonApiStaticFixture),
                    typeof(UdonApiStaticFixture2)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("generated module path"));
        }

        [Test]
        public void Generator_RejectsTypeAndChildNamespacePathCollision()
        {
            var config = CreateTypeNamespaceCollisionConfig(
                "path_collision",
                "path_collision.deep");

            Assert.That(
                () => CreateGenerator(config).Generate(new[]
                {
                    typeof(UdonApiStaticFixture),
                    typeof(PolicyFixtures.Deep.DeepNamespaceFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("namespace facade path"));
        }

        [Test]
        public void Generator_RejectsCaseInsensitiveTypeAndNamespacePathCollision()
        {
            var config = CreateTypeNamespaceCollisionConfig(
                "case_collision",
                "case_collision.Deep");

            Assert.That(
                () => CreateGenerator(config).Generate(new[]
                {
                    typeof(PolicyFixtures.Deep.DeepNamespaceFixture),
                    typeof(UdonApiStaticFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("collides by case"));
        }

        [Test]
        public void Generator_ExcludesNamespaceTypeExactOverloadPropertyAndField()
        {
            var namespaceConfig = UdonBindingGenerationConfig.CreateDefault();
            namespaceConfig.excludes.namespaces = new[]
            {
                typeof(PolicyFixtures.NamespaceFixture).Namespace
            };
            var namespaceResult = CreateGenerator(namespaceConfig).Generate(new[]
            {
                typeof(PolicyFixtures.NamespaceFixture),
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)
            });
            Assert.That(namespaceResult.Report.types_skipped, Is.EqualTo(2));
            Assert.That(namespaceResult.Files, Is.Empty);
            Assert.That(namespaceResult.Report.skipped_types.TrueForAll(record =>
                record.reason.Contains("exclude.namespace")), Is.True);

            var typeConfig = UdonBindingGenerationConfig.CreateDefault();
            typeConfig.excludes.types = new[]
            {
                typeof(UdonApiStructFixture).FullName
            };
            var typeResult = CreateGenerator(typeConfig).Generate(new[]
            {
                typeof(UdonApiStructFixture),
                typeof(UdonApiStaticFixture)
            });
            Assert.That(typeResult.Report.types_skipped, Is.EqualTo(1));
            Assert.That(typeResult.Report.types_generated, Is.EqualTo(1));

            var fixtureType = typeof(UdonBindingGeneratorFixture);
            var memberConfig = UdonBindingGenerationConfig.CreateDefault();
            memberConfig.excludes.members = new[]
            {
                MemberRule(
                    fixtureType,
                    "instance_method",
                    "Mix",
                    new[] { typeof(int) }),
                ClrMemberId.Format(fixtureType.GetProperty("Count")),
                ClrMemberId.Format(fixtureType.GetField("Number"))
            };
            var memberResult = CreateGenerator(memberConfig).Generate(
                new[] { fixtureType });
            var source = GetFixtureSource(memberResult);
            Assert.That(source, Does.Not.Contain("mix(value: i32)"));
            Assert.That(source, Does.Contain("mix(value: f32)"));
            Assert.That(source, Does.Not.Contain("fn count"));
            Assert.That(source, Does.Not.Contain("fn set_count"));
            Assert.That(source, Does.Not.Contain("fn number"));
            Assert.That(source, Does.Not.Contain("fn set_number"));
            Assert.That(memberResult.Report.explicit_exclusions, Is.EqualTo(5));
        }

        [Test]
        public void Generator_RendersTypeMemberAndNonRecursiveNamespacePreludeExports()
        {
            var rootNamespace = typeof(UdonBindingGeneratorFixture).Namespace;

            var typeConfig = UdonBindingGenerationConfig.CreateDefault();
            typeConfig.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = rootNamespace,
                    to = "api"
                }
            };
            typeConfig.prelude.types = new[]
            {
                "api.UdonBindingGeneratorFixture"
            };
            var typeResult = CreateGenerator(typeConfig).Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture)
            });
            Assert.That(typeResult.Files["prelude.sobakasu"], Is.EqualTo(
                "pub use api.UdonBindingGeneratorFixture;\n"));
            Assert.That(typeResult.Files["prelude.sobakasu"],
                Does.Not.Contain("api.udon_binding_generator_fixture"));
            Assert.That(typeResult.Files["api.sobakasu"],
                Does.Contain("mod udon_binding_generator_fixture;"));
            Assert.That(typeResult.Files["api.sobakasu"],
                Does.Not.Contain("pub mod udon_binding_generator_fixture;"));
            Assert.That(typeResult.Report.rules_configured, Is.EqualTo(2));
            Assert.That(typeResult.Report.rules_matched, Is.EqualTo(2));

            var memberConfig = UdonBindingGenerationConfig.CreateDefault();
            memberConfig.renames.namespaces = typeConfig.renames.namespaces;
            memberConfig.prelude.members = new[]
            {
                "api.udon_api_static_fixture.abs"
            };
            var memberResult = CreateGenerator(memberConfig).Generate(new[]
            {
                typeof(UdonApiStaticFixture)
            });
            Assert.That(memberResult.Files["prelude.sobakasu"], Is.EqualTo(
                "pub use api.udon_api_static_fixture.abs;\n"));

            var namespaceConfig = UdonBindingGenerationConfig.CreateDefault();
            namespaceConfig.renames.namespaces = typeConfig.renames.namespaces;
            namespaceConfig.prelude.namespaces = new[] { "api" };
            var namespaceResult = CreateGenerator(namespaceConfig).Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture),
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)
            });
            Assert.That(namespaceResult.Files["prelude.sobakasu"],
                Is.EqualTo("pub use api.*;\n"));
            Assert.That(namespaceResult.Files["prelude.sobakasu"],
                Does.Not.Contain("api.policy_fixtures.*"));
            AssertAllBindingSourcesParse(namespaceResult);
        }

        [Test]
        public void Generator_RejectsStaleAndCollidingPreludeTargets()
        {
            var stale = UdonBindingGenerationConfig.CreateDefault();
            stale.prelude.types = new[] { "missing.Type" };
            Assert.That(
                () => CreateGenerator(stale).Generate(new[]
                {
                    typeof(UdonBindingGeneratorFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("does not exist"));

            var collision = UdonBindingGenerationConfig.CreateDefault();
            collision.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = typeof(UdonBindingGeneratorFixture).Namespace,
                    to = "api"
                }
            };
            collision.prelude.namespaces = new[] { "api" };
            collision.prelude.types = new[]
            {
                "api.UdonBindingGeneratorFixture"
            };
            Assert.That(
                () => CreateGenerator(collision).Generate(new[]
                {
                    typeof(UdonBindingGeneratorFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("Prelude symbol"));
        }

        [Test]
        public void Generator_RejectsInvalidAndStalePolicyRules()
        {
            var fixtureType = typeof(UdonBindingGeneratorFixture);

            var staleType = UdonBindingGenerationConfig.CreateDefault();
            staleType.renames.types = new[]
            {
                new UdonBindingTypeRenameRule
                {
                    from = "Missing.Namespace.Type",
                    to = "Missing"
                }
            };
            Assert.That(
                () => CreateGenerator(staleType).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("rename.type:Missing.Namespace.Type"));

            var duplicateType = UdonBindingGenerationConfig.CreateDefault();
            duplicateType.renames.types = new[]
            {
                new UdonBindingTypeRenameRule
                {
                    from = fixtureType.FullName,
                    to = "FixtureOne"
                },
                new UdonBindingTypeRenameRule
                {
                    from = fixtureType.FullName,
                    to = "FixtureTwo"
                }
            };
            Assert.That(
                () => CreateGenerator(duplicateType).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("Conflicting type renames"));

            var invalidNamespace = UdonBindingGenerationConfig.CreateDefault();
            invalidNamespace.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = fixtureType.Namespace,
                    to = "invalid-path"
                }
            };
            Assert.That(
                () => CreateGenerator(invalidNamespace).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("invalid Sobakasu path"));

            var stale = UdonBindingGenerationConfig.CreateDefault();
            stale.renames.members = new[]
            {
                new UdonBindingMemberRenameRule
                {
                    from = fixtureType.FullName + ".Missing()",
                    to = "missing"
                }
            };
            Assert.That(
                () => CreateGenerator(stale).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("did not match"));

            var valueReturn = UdonBindingGenerationConfig.CreateDefault();
            valueReturn.maybe.returns = new[]
            {
                MemberRule(
                    fixtureType,
                    "instance_method",
                    "Mix",
                    new[] { typeof(int) })
            };
            Assert.That(
                () => CreateGenerator(valueReturn).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("non-reference type"));

            var refProjection = UdonBindingGenerationConfig.CreateDefault();
            refProjection.maybe.outs = new[]
            {
                new UdonBindingMaybeOutRule
                {
                    member = MemberRule(
                        fixtureType,
                        "instance_method",
                        "RefValue",
                        new[] { typeof(int).MakeByRefType() }),
                    parameters = new[] { "value" }
                }
            };
            Assert.That(
                () => CreateGenerator(refProjection).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("is not out"));

            var invalidEnum = UdonBindingGenerationConfig.CreateDefault();
            invalidEnum.version = "1";
            Assert.That(
                () => CreateGenerator(invalidEnum).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("Unsupported configuration version"));

            var valueOut = UdonBindingGenerationConfig.CreateDefault();
            valueOut.maybe.outs = new[]
            {
                new UdonBindingMaybeOutRule
                {
                    member = MemberRule(
                        fixtureType,
                        "instance_method",
                        "OutNumber",
                        new[] { typeof(int).MakeByRefType() }),
                    parameters = new[] { "value" }
                }
            };
            Assert.That(
                () => CreateGenerator(valueOut).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("non-reference type"));

            var missingOut = UdonBindingGenerationConfig.CreateDefault();
            missingOut.maybe.outs = new[]
            {
                new UdonBindingMaybeOutRule
                {
                    member = MemberRule(
                        fixtureType,
                        "instance_method",
                        "RefOut",
                        new[]
                        {
                            typeof(int).MakeByRefType(),
                            typeof(string).MakeByRefType()
                        }),
                    parameters = new[] { "missing" }
                }
            };
            Assert.That(
                () => CreateGenerator(missingOut).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("no parameter"));

            var duplicate = UdonBindingGenerationConfig.CreateDefault();
            var duplicateRule = MemberRule(
                fixtureType,
                "static_method",
                "Find",
                new[] { typeof(string) });
            duplicate.renames.members = new[]
            {
                new UdonBindingMemberRenameRule { from = duplicateRule, to = "one" },
                new UdonBindingMemberRenameRule { from = duplicateRule, to = "two" }
            };
            Assert.That(
                () => CreateGenerator(duplicate).Generate(new[] { fixtureType }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("Conflicting member renames"));
        }

        [Test]
        public void ConfigurationLoader_RejectsUnknownAndDuplicateProperties()
        {
            var path = NewTemporaryPath() + ".json";
            File.WriteAllText(path,
                ConfigurationJson("[]").Replace(
                    "\"renames\":{",
                    "\"renames\":{\"reference_retrn\":\"maybe\","));
            try
            {
                Assert.That(
                    () => UdonBindingGenerationConfig.Load(path),
                    Throws.TypeOf<UdonBindingConfigurationException>()
                        .With.Message.Contains("Unknown property 'reference_retrn'"));
                Assert.That(
                    () => LoadConfig(ConfigurationJson("[]").Replace(
                        "\"version\":\"3\"",
                        "\"version\":\"3\",\"version\":\"3\"")),
                    Throws.TypeOf<UdonBindingConfigurationException>()
                        .With.Message.Contains("declared more than once"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ConfigurationLoader_DistinguishesOmittedAndExplicitNullNamespace()
        {
            var rootNamespace = typeof(PolicyFixtures.NamespaceFixture).Namespace;
            var explicitNull = LoadConfig(
                ConfigurationJson(
                    "[{\"from\":\"" + rootNamespace + "\",\"to\":null}]"));

            Assert.That(explicitNull.renames.namespaces[0].ToSpecified, Is.True);
            Assert.That(explicitNull.renames.namespaces[0].to, Is.Null);
            Assert.That(
                () => LoadConfig(ConfigurationJson(
                    "[{\"from\":\"" + rootNamespace + "\"}]")),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("Required property 'to'"));

            var explicitResult = CreateGenerator(explicitNull).Generate(new[]
            {
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)
            });
            Assert.That(FindGeneratedType(
                explicitResult.Report,
                typeof(PolicyFixtures.Deep.DeepNamespaceFixture)).sobakasu_namespace,
                Is.EqualTo("deep"));
        }

        [Test]
        public void Generator_ResolvesPromotedUdonProductImport()
        {
            const string qualifiedName = "VRC.Economy.UdonProduct";
            var productType = FindLoadedType(qualifiedName);
            if (productType == null)
            {
                var assemblyPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Packages/com.vrchat.worlds/Runtime/VRCSDK/Plugins/" +
                    "VRCEconomy.dll");
                productType = System.Reflection.Assembly.LoadFrom(assemblyPath)
                    .GetType(qualifiedName);
            }
            Assert.That(productType, Is.Not.Null,
                "The installed VRChat SDK does not provide UdonProduct.");
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = "VRC",
                    to = null
                }
            };
            config.excludes.members = new[]
            {
                MemberRule(
                    productType,
                    "static_method",
                    "op_Equality",
                    new[] { typeof(UnityEngine.Object), typeof(UnityEngine.Object) }),
                MemberRule(
                    productType,
                    "static_method",
                    "op_Implicit",
                    new[] { typeof(UnityEngine.Object) }),
                MemberRule(
                    productType,
                    "static_method",
                    "op_Inequality",
                    new[] { typeof(UnityEngine.Object), typeof(UnityEngine.Object) }),
                ClrMemberId.Format(typeof(UnityEngine.Object).GetProperty("name"))
            };

            var result = CreateInstalledGenerator(config).Generate(new[]
            {
                productType
            });

            Assert.That(result.Files.Keys, Does.Contain("economy.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("economy/udon_product.sobakasu"));
            Assert.That(result.Files["economy.sobakasu"],
                Does.Contain("pub use udon_product.UdonProduct;"));
            WithGeneratedLibrary(result, root =>
            {
                var compilation = SobakasuCompiler.CompileToUasm(
                    "use economy.UdonProduct; on start { }",
                    root);
                Assert.That(compilation.Success, Is.True, compilation.ErrorText);
            });
        }

        [Test]
        public void ConfigurationLoader_LoadsVersion3Schema()
        {
            var config = LoadConfig(
                "{\"version\":\"3\"," +
                "\"renames\":{\"namespaces\":[" +
                "{\"from\":\"System\",\"to\":\"system\"}," +
                "{\"from\":\"UnityEngine\",\"to\":\"unity\"}," +
                "{\"from\":\"VRC.SDKBase\",\"to\":null}]," +
                "\"types\":[],\"members\":[]}," +
                "\"lang\":[]," +
                "\"prelude\":{\"namespaces\":[],\"types\":[],\"members\":[]}," +
                "\"maybe\":{\"returns\":[" +
                "\"UnityEngine.GameObject.Find(System.String)\"],\"outs\":[]}," +
                "\"excludes\":{\"namespaces\":[],\"types\":[],\"members\":[]}}");

            Assert.That(config.version, Is.EqualTo("3"));
            Assert.That(config.renames.namespaces, Has.Length.EqualTo(3));
            Assert.That(config.renames.namespaces[2].ToSpecified, Is.True);
            Assert.That(config.renames.namespaces[2].to, Is.Null);
            Assert.That(config.renames.types, Is.Empty);
            Assert.That(config.maybe.returns, Has.Length.EqualTo(1));

            var utilitiesType = FindLoadedType("VRC.SDKBase.Utilities");
            Assert.That(utilitiesType, Is.Not.Null);
            var result = CreateInstalledGenerator(config).Generate(new[]
            {
                typeof(Math),
                typeof(UnityEngine.Debug),
                typeof(UnityEngine.GameObject),
                utilitiesType
            });
            Assert.That(result.Files.Keys, Does.Contain("system.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("unity.sobakasu"));
            Assert.That(result.Files.Keys, Does.Contain("utilities.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("system/math.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("unity/debug.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Contain("unity/game_object.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Not.Contain("/utilities.sobakasu"));
            Assert.That(result.Report.rules_configured, Is.EqualTo(4));
            Assert.That(result.Report.rules_matched, Is.EqualTo(4));
        }

        [Test]
        public void DefaultConfiguration_GeneratesAndExportsNetworkEventTarget()
        {
            var config = UdonBindingGenerationConfig.Load(
                StandardLibraryGenerator.DefaultConfigurationPath);

            Assert.That(config.excludes.types, Does.Not.Contain(
                "VRC.Udon.Common.Interfaces.NetworkEventTarget"));
            Assert.That(config.prelude.types, Does.Contain(
                "vrc.udon.common.interfaces.NetworkEventTarget"));
            Assert.That(config.prelude.types, Does.Not.Contain(
                "vrc.udon.common.interfaces.network_event_target.NetworkEventTarget"));
            Assert.That(config.lang, Has.Length.EqualTo(14));
            Assert.That(Array.Exists(config.lang, rule =>
                rule.from == "VRC.Udon.Common.Interfaces.NetworkEventTarget" &&
                rule.item == "network_event_target"), Is.True);
            Assert.That(Array.Exists(config.lang, rule =>
                rule.from == "System.Int64" && rule.item == "i64"), Is.True);
            Assert.That(Array.Exists(config.lang, rule =>
                rule.from == "System.String" && rule.item == "string"), Is.True);
        }

        [Test]
        public void Generator_LoadsDedicatedLanguageItemConfigAndRendersTypeMetadata()
        {
            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Packages/com.skytomo221.sobakasu/Tests/Editor/TestData/" +
                "StandardLibraryGenerator/lang-version-3.json");
            var config = UdonBindingGenerationConfig.Load(path);

            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture)
            });

            Assert.That(config.version, Is.EqualTo("3"));
            Assert.That(config.lang, Has.Length.EqualTo(1));
            Assert.That(GetFixtureSource(result), Does.StartWith(
                "lang \"network_event_target\"\npub impl "));
            Assert.That(result.Report.rules_configured, Is.EqualTo(1));
            Assert.That(result.Report.rules_matched, Is.EqualTo(1));
        }

        [Test]
        public void Generator_RejectsInvalidLanguageItemRules()
        {
            var fixture = typeof(UdonBindingGeneratorFixture).FullName;
            var structFixture = typeof(UdonApiStructFixture).FullName;

            var duplicateFrom = UdonBindingGenerationConfig.CreateDefault();
            duplicateFrom.lang = new[]
            {
                new UdonBindingLangRule { from = fixture, item = "maybe" },
                new UdonBindingLangRule { from = fixture, item = "network_event_target" }
            };
            Assert.That(
                () => CreateGenerator(duplicateFrom).Generate(new[]
                {
                    typeof(UdonBindingGeneratorFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("Conflicting language item rules"));

            var duplicateItem = UdonBindingGenerationConfig.CreateDefault();
            duplicateItem.lang = new[]
            {
                new UdonBindingLangRule { from = fixture, item = "maybe" },
                new UdonBindingLangRule { from = structFixture, item = "maybe" }
            };
            Assert.That(
                () => CreateGenerator(duplicateItem).Generate(new[]
                {
                    typeof(UdonBindingGeneratorFixture),
                    typeof(UdonApiStructFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("assigned more than once"));

            var stale = UdonBindingGenerationConfig.CreateDefault();
            stale.lang = new[]
            {
                new UdonBindingLangRule { from = "Missing.Type", item = "maybe" }
            };
            Assert.That(
                () => CreateGenerator(stale).Generate(new[]
                {
                    typeof(UdonBindingGeneratorFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("did not match"));

            var emptyFrom = UdonBindingGenerationConfig.CreateDefault();
            emptyFrom.lang = new[]
            {
                new UdonBindingLangRule { from = string.Empty, item = "maybe" }
            };
            Assert.That(
                () => CreateGenerator(emptyFrom).Generate(new[]
                {
                    typeof(UdonBindingGeneratorFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("malformed CLR identity"));

            var emptyItem = UdonBindingGenerationConfig.CreateDefault();
            emptyItem.lang = new[]
            {
                new UdonBindingLangRule { from = fixture, item = string.Empty }
            };
            Assert.That(
                () => CreateGenerator(emptyItem).Generate(new[]
                {
                    typeof(UdonBindingGeneratorFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("empty item"));

            var staticClass = UdonBindingGenerationConfig.CreateDefault();
            staticClass.lang = new[]
            {
                new UdonBindingLangRule
                {
                    from = typeof(UdonApiStaticFixture).FullName,
                    item = "network_event_target"
                }
            };
            Assert.That(
                () => CreateGenerator(staticClass).Generate(new[]
                {
                    typeof(UdonApiStaticFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("does not generate a type declaration"));

            var nullRule = UdonBindingGenerationConfig.CreateDefault();
            nullRule.lang = new UdonBindingLangRule[] { null };
            Assert.That(
                () => CreateGenerator(nullRule).Generate(new[]
                {
                    typeof(UdonBindingGeneratorFixture)
                }),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("language item rule is null"));

            Assert.That(
                () => LoadConfig(ConfigurationJson("[]").Replace(
                    "\"lang\":[]",
                    "\"lang\":[{\"item\":\"maybe\"}]")),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("Required property 'from'"));
            Assert.That(
                () => LoadConfig(ConfigurationJson("[]").Replace(
                    "\"lang\":[]",
                    "\"lang\":[{\"from\":\"Example.Type\"}]")),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("Required property 'item'"));
        }
    }
}
