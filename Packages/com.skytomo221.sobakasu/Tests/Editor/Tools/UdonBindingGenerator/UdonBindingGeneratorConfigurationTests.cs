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
    public class UdonBindingGeneratorConfigurationTests
    {

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
