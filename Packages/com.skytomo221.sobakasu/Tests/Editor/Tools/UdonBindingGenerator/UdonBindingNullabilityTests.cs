using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class UdonBindingNullabilityTests
    {
        private static UdonBindingSourceModel Fixture()
        {
            UdonApiParameterRecord Parameter(string name, string type, string passing = null) =>
                new() { name = name, type = UdonApiTypeRef.Named(type), passing = passing };
            UdonApiMethodRecord Method(string name, string result, params UdonApiParameterRecord[] parameters) =>
                new() { name = name, returnType = UdonApiTypeRef.Named(result), parameters = parameters.ToList(),
                    udonSignature = "ExampleApi.__" + name + "__" + string.Join("_", parameters.Select(p => p.type.named.Replace(".", ""))) + "__" + result.Replace(".", "") };
            var methods = new List<UdonApiMethodRecord>
            {
                Method("Find", "System.String", Parameter("value", "System.String")),
                Method("Find", "System.String", Parameter("value", "System.Int32")),
                Method("Text", "System.String"),
                Method("Read", "System.Int32", Parameter("first", "System.String", "out"), Parameter("second", "System.String", "out")),
                Method("ByRef", "System.Int32", Parameter("value", "System.String", "ref")),
                Method("Select", "System.String")
            };
            var generic = Method("Select", "System.String");
            generic.genericParameters = new List<UdonApiGenericParameterRecord> { new() { name = "T" } };
            generic.udonSignature += "Generic";
            methods.Add(generic);
            var catalog = new UdonApiCatalogData
            {
                formatVersion = 2,
                namespaces = new List<UdonApiNamespaceRecord>
                {
                    new() { name = "Example", exposed = new UdonApiTypeSet { classes = new List<UdonApiClassRecord>
                    {
                        new() { path = new List<string> { "Api" }, exposed = new UdonApiMemberExposure
                        {
                            instance = new UdonApiMemberSet
                            {
                                methods = methods,
                                propertyGetters = new List<UdonApiPropertyAccessorRecord> { new() { name = "Text", type = UdonApiTypeRef.Named("System.String"), udonSignature = "ExampleApi.__get_Text__SystemString" } },
                                propertySetters = new List<UdonApiPropertyAccessorRecord> { new() { name = "Text", type = UdonApiTypeRef.Named("System.String"), udonSignature = "ExampleApi.__set_Text__SystemString__SystemVoid" } }
                            }
                        } }
                    } } },
                    new() { name = "System", unexposed = new UdonApiTypeSet { classes = new List<UdonApiClassRecord>
                    {
                        new() { path = new List<string> { "String" } }
                    } } }
                }
            };
            return UdonBindingSourceModel.FromCatalog(catalog);
        }

        private static UdonApiGeneratedTypeModel Apply(UdonBindingSourceModel source, UdonBindingGenerationConfig config) =>
            new UdonBindingGenerationPolicy().Apply(source, config, "").Types.Single(t => t.Physical.RuntimeName == "Example.Api");

        [Test]
        public void NonMaybeReturn_SelectsOneOverloadAndRendererRemovesMaybe()
        {
            var source = Fixture();
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.maybe.nonMaybeReturns = new[] { "Example.Api.Find(System.String)" };
            var type = Apply(source, config);
            var overloads = type.Members.Where(m => m.Physical.Name == "Find").ToArray();
            Assert.That(overloads.Single(m => m.Physical.Parameters[0].type.runtimeName == "System.String").ReturnProjection,
                Is.EqualTo(UdonApiGeneratedProjection.Raw));
            Assert.That(overloads.Single(m => m.Physical.Parameters[0].type.runtimeName == "System.Int32").ReturnProjection,
                Is.EqualTo(UdonApiGeneratedProjection.Maybe));
            var rendered = new SobakasuBindingRenderer(new UdonBindingTypeFormatter(source)).RenderType(type);
            Assert.That(rendered, Does.Contain("= extern self.Find(value)"));
            Assert.That(rendered, Does.Contain("= maybe extern self.Find(value)"));
        }

        [Test]
        public void NonMaybeOut_SelectsOnlyNamedOutAndRejectsRef()
        {
            var source = Fixture();
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.maybe.nonMaybeOuts = new[] { new UdonBindingNonMaybeOutRule
                { member = "Example.Api.Read(System.String&,System.String&)", parameters = new[] { "second" } } };
            var member = Apply(source, config).Members.Single(m => m.Physical.Name == "Read");
            Assert.That(member.GetOutProjection(0), Is.EqualTo(UdonApiGeneratedProjection.Maybe));
            Assert.That(member.GetOutProjection(1), Is.EqualTo(UdonApiGeneratedProjection.Raw));
            config.maybe.nonMaybeOuts = new[] { new UdonBindingNonMaybeOutRule
                { member = "Example.Api.ByRef(System.String&)", parameters = new[] { "value" } } };
            Assert.That(() => Apply(source, config), Throws.TypeOf<UdonBindingConfigurationException>().With.Message.Contains("is not out"));
        }

        [Test]
        public void NonMaybeGetter_DoesNotChangeSetterOrSameNamedMethod()
        {
            var source = Fixture();
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.maybe.nonMaybeReturns = new[] { "Example.Api.Text" };
            var members = Apply(source, config).Members.Where(m => m.Physical.Name == "Text").ToArray();
            Assert.That(members.Single(m => m.Physical.Kind == UdonApiMemberKind.PropertyGetter).ReturnProjection,
                Is.EqualTo(UdonApiGeneratedProjection.Raw));
            Assert.That(members.Single(m => m.Physical.Kind == UdonApiMemberKind.PropertySetter).ReturnProjection,
                Is.EqualTo(UdonApiGeneratedProjection.Raw));
            Assert.That(members.Single(m => m.Physical.Kind == UdonApiMemberKind.InstanceMethod).ReturnProjection,
                Is.EqualTo(UdonApiGeneratedProjection.Maybe));
        }

        [Test]
        public void LegacyMaybeSelector_CannotSafelySelectGenericArityCollision()
        {
            var members = Fixture().Types.Single(t => t.RuntimeName == "Example.Api").Members.Where(m => m.Name == "Select").ToArray();
            Assert.That(members.Select(m => m.GenericParameters.Count), Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(members.Select(m => m.ClrSignature).Distinct().Count(), Is.EqualTo(1));
            Assert.That(members.Select(UdonBindingMemberSelector.FromPhysical).Distinct().Count(), Is.EqualTo(2));
            // Task F requires one physical match; this legacy string must not be adopted.
            Assert.That(members.Count(m => m.ClrSignature == "Example.Api.Select()"), Is.EqualTo(2));
        }

        [Test]
        public void NonMaybeReturn_StillRejectsUnmatchedRule()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.maybe.nonMaybeReturns = new[] { "Example.Api.Missing()" };
            Assert.That(() => Apply(Fixture(), config), Throws.TypeOf<UdonBindingConfigurationException>().With.Message.Contains("did not match"));
        }

        [Test]
        public void BundledNonMaybeRules_HaveOnePhysicalTargetAndOnlyChangeTheirReturns()
        {
            var source = UdonBindingSourceModel.Load(StandardLibraryGenerator.DefaultCatalogPath);
            var config = UdonBindingGenerationConfig.Load(StandardLibraryGenerator.DefaultConfigurationPath);
            Assert.That(config.maybe.nonMaybeTypes, Is.Empty, "No type-wide assertion was approved.");
            Assert.That(config.maybe.nonMaybeOuts, Is.Empty, "No out candidate passed the high-confidence review gate.");
            var signatures = config.maybe.nonMaybeReturns.ToHashSet(StringComparer.Ordinal);
            Assert.That(signatures.Count, Is.GreaterThan(0));
            foreach (var signature in signatures)
                Assert.That(source.Types.SelectMany(t => t.Members).Count(m => m.ClrSignature == signature), Is.EqualTo(1), signature);
            var after = new UdonBindingGenerationPolicy().Apply(source, config, "");
            foreach (var signature in signatures)
                Assert.That(config.GetRuleMatchCount("maybe.nonMaybeReturn:" + signature), Is.EqualTo(1), signature);
            var baseline = UdonBindingGenerationConfig.Load(StandardLibraryGenerator.DefaultConfigurationPath);
            baseline.maybe.nonMaybeReturns = Array.Empty<string>();
            var before = new UdonBindingGenerationPolicy().Apply(source, baseline, "").Types
                .SelectMany(t => t.Members).ToDictionary(m => m.Physical);
            var changes = 0;
            foreach (var member in after.Types.SelectMany(t => t.Members))
            {
                var original = before[member.Physical];
                if (signatures.Contains(member.Physical.ClrSignature))
                {
                    Assert.That(original.ReturnProjection, Is.EqualTo(UdonApiGeneratedProjection.Maybe));
                    Assert.That(member.ReturnProjection, Is.EqualTo(UdonApiGeneratedProjection.Raw));
                    changes++;
                }
                else Assert.That(member.ReturnProjection, Is.EqualTo(original.ReturnProjection), member.Physical.DisplaySignature);
                for (var index = 0; index < member.Physical.Parameters.Count; index++)
                    Assert.That(member.GetOutProjection(index), Is.EqualTo(original.GetOutProjection(index)));
            }
            Assert.That(changes, Is.EqualTo(signatures.Count));
            var generated = Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.UdonBindingGenerator.CreateDefault(StandardLibraryGenerator.DefaultCatalogPath,
                StandardLibraryGenerator.DefaultConfigurationPath).Generate().Report;
            Assert.That(generated.unmatched_rules, Is.Empty);
            Assert.That(generated.rules_matched, Is.EqualTo(generated.rules_configured));
        }
    }
}
