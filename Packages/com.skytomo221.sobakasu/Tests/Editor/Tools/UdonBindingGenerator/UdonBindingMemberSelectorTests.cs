using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class UdonBindingMemberSelectorTests
    {
        private static UdonBindingGenerationConfig Load(string entries, string version = "5")
        {
            var path = Path.Combine(Path.GetTempPath(), "sobakasu-selector-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "{\"version\":\"" + version + "\",\"renames\":{\"namespaces\":[],\"types\":[],\"members\":[]}," +
                    "\"lang\":[],\"prelude\":{\"namespaces\":[],\"types\":[],\"members\":[]}," +
                    "\"maybe\":{\"nonMaybeTypes\":[],\"nonMaybeReturns\":[],\"nonMaybeOuts\":[]}," +
                    "\"excludes\":{\"namespaces\":[],\"types\":[],\"members\":[]},\"receivers\":{\"readonly\":[" + entries + "]}}");
                return UdonBindingGenerationConfig.Load(path);
            }
            finally { File.Delete(path); }
        }

        private static string Selector(string kind, string member = "Example.Value.x", string extra = "") =>
            "{\"member\":\"" + member + "\",\"kind\":\"" + kind + "\"" + extra + "}";

        [TestCase("propertyGetter")]
        [TestCase("propertySetter")]
        [TestCase("fieldGetter")]
        [TestCase("fieldSetter")]
        [TestCase("instanceMethod", ",\"parameters\":[]")]
        public void Configuration_LoadsStructuredSelectors(string kind, string extra = "")
        {
            Assert.That(Load(Selector(kind, extra: extra)).receivers.Selectors.Count, Is.EqualTo(1));
        }

        [TestCase("\"Example.Value.x\"")]
        [TestCase("null")]
        [TestCase("{\"member\":\"   \",\"kind\":\"propertyGetter\"}")]
        [TestCase("{\"member\":\"Example.Value.x\",\"kind\":\"unknown\"}")]
        [TestCase("{\"member\":\"Example.Value.x\",\"kind\":\"staticMethod\",\"parameters\":[]}")]
        [TestCase("{\"member\":\"Example.Value.x\",\"kind\":\"constructor\"}")]
        [TestCase("{\"member\":\"Example.Value.Foo\",\"kind\":\"instanceMethod\"}")]
        [TestCase("{\"member\":\"Example.Value.Foo()\",\"kind\":\"instanceMethod\",\"parameters\":[]}")]
        [TestCase("{\"member\":\"Example.Value.x\",\"kind\":\"propertyGetter\",\"unknown\":0}")]
        [TestCase("{\"member\":\"Example.Value.x\",\"member\":\"Example.Value.x\",\"kind\":\"propertyGetter\"}")]
        [TestCase("{\"member\":null,\"kind\":\"propertyGetter\"}")]
        [TestCase("{\"member\":\"Example.Value.x\",\"kind\":null}")]
        [TestCase("{\"member\":\"Example.Value.x\",\"kind\":\"propertyGetter\",\"genericArity\":-1}")]
        [TestCase("{\"member\":\"Example.Value.Foo\",\"kind\":\"instanceMethod\",\"parameters\":null}")]
        [TestCase("{\"member\":\"Example.Value.Foo\",\"kind\":\"instanceMethod\",\"parameters\":[{}]}")]
        [TestCase("{\"member\":\"Example.Value.Foo\",\"kind\":\"instanceMethod\",\"parameters\":[{\"type\":null}]}")]
        [TestCase("{\"member\":\"Example.Value.Foo\",\"kind\":\"instanceMethod\",\"parameters\":[{\"type\":\"System.Int32\",\"passing\":\"value\"}]}")]
        [TestCase("{\"member\":\"Example.Value.Foo\",\"kind\":\"instanceMethod\",\"parameters\":[{\"type\":\"System.Int32\",\"type\":\"System.Int32\"}]}")]
        [TestCase("{\"member\":\"Example.Value.Foo\",\"kind\":\"instanceMethod\",\"parameters\":[{\"type\":{\"array\":\"System.Int32\",\"array\":\"System.Int32\"}}]}")]
        public void Configuration_RejectsInvalidShape(string entry) =>
            Assert.Throws<UdonBindingConfigurationException>(() => Load(entry));

        [Test]
        public void Configuration_V4ExplainsMigrationBeforeLegacyShapeValidation()
        {
            var error = Assert.Throws<UdonBindingConfigurationException>(() => Load("\"UnityEngine.Vector3.ToString()\"", "4"));
            Assert.That(error.Message, Does.Contain("version 4 is no longer supported").And.Contain("version 5")
                .And.Contain("receivers.readonly").And.Contain("CLR-signature strings").And.Contain("structured selectors")
                .And.Contain("ToString()").And.Contain("\"member\": \"UnityEngine.Vector3.ToString\"")
                .And.Contain("\"parameters\": []"));
        }

        [Test]
        public void Configuration_ReusesCatalogTypeRefsAndRetainsGenericArity()
        {
            var extra = ",\"genericArity\":1,\"parameters\":[{\"type\":{\"generic\":\"Example.Box`1\",\"arguments\":[{\"array\":{\"methodParameter\":0}}]},\"passing\":\"in\"}]";
            var selector = Load(Selector("instanceMethod", "Example.Value.Foo", extra)).receivers.Selectors.Single();
            Assert.That(selector.GenericArity, Is.EqualTo(1));
            var other = Load(Selector("instanceMethod", "Example.Value.Foo", extra.Replace("genericArity\":1", "genericArity\":2"))).receivers.Selectors.Single();
            Assert.That(selector, Is.Not.EqualTo(other));
            Assert.Throws<UdonBindingConfigurationException>(() => Load(
                Selector("instanceMethod", "Example.Value.Foo", extra) + "," + Selector("instanceMethod", "Example.Value.Foo", extra)));
        }

        [Test]
        public void Configuration_DuplicateUsesStructuralEquality()
        {
            var getter = Selector("propertyGetter");
            Assert.Throws<UdonBindingConfigurationException>(() => Load(getter + "," + getter));
            Assert.Throws<UdonBindingConfigurationException>(() => Load(getter + "," +
                Selector("propertyGetter", extra: ",\"parameters\":[],\"genericArity\":0")));
            Assert.That(Load(getter + "," + Selector("propertySetter")).receivers.Selectors.Count, Is.EqualTo(2));
        }

        private static UdonBindingSourceModel Model(bool duplicate = false, bool staticAccessors = false)
        {
            var getters = new List<UdonApiPropertyAccessorRecord> { new() { name = "x", type = UdonApiTypeRef.Named("System.Int32"), udonSignature = "getter" } };
            if (duplicate) getters.Add(new UdonApiPropertyAccessorRecord { name = "x", type = UdonApiTypeRef.Named("System.Int32"), udonSignature = "other-getter" });
            var set = new UdonApiMemberSet
            {
                propertyGetters = getters,
                propertySetters = new List<UdonApiPropertyAccessorRecord> { new() { name = "x", type = UdonApiTypeRef.Named("System.Int32"), udonSignature = "setter" } },
                fieldGetters = new List<UdonApiFieldAccessorRecord> { new() { name = "field", type = UdonApiTypeRef.Named("System.Int32"), udonSignature = "field-getter" } },
                fieldSetters = new List<UdonApiFieldAccessorRecord> { new() { name = "field", type = UdonApiTypeRef.Named("System.Int32"), udonSignature = "field-setter" } },
                methods = new List<UdonApiMethodRecord>()
            };
            foreach (var type in new[] { null, "System.Int32", "System.String" })
                set.methods.Add(new UdonApiMethodRecord { name = "Foo", returnType = UdonApiTypeRef.Named("System.Void"),
                    parameters = type == null ? null : new List<UdonApiParameterRecord> { new() { name = "ignored", type = UdonApiTypeRef.Named(type) } }, udonSignature = "Foo-" + type });
            foreach (var passing in new[] { null, "ref", "out", "in" })
                set.methods.Add(new UdonApiMethodRecord { name = "Pass", returnType = UdonApiTypeRef.Named("System.Void"),
                    parameters = new List<UdonApiParameterRecord> { new() { name = "ignored", type = UdonApiTypeRef.Named("System.Int32"), passing = passing } }, udonSignature = "Pass-" + passing });
            return UdonBindingSourceModel.FromCatalog(new UdonApiCatalogData { namespaces = new List<UdonApiNamespaceRecord>
            {
                new() { name = "Example", exposed = new UdonApiTypeSet { structs = new List<UdonApiStructRecord>
                {
                    new() { path = new List<string> { "Value" }, exposed = new UdonApiMemberExposure { instance = staticAccessors ? null : set, @static = staticAccessors ? set : null } }
                } } }
            } });
        }

        private static UdonApiGeneratedModel Apply(UdonBindingSourceModel model, UdonBindingGenerationConfig config) =>
            new UdonBindingGenerationPolicy().Apply(model, config, "");

        private static UdonBindingSourceModel InheritedModel()
        {
            return UdonBindingSourceModel.FromCatalog(new UdonApiCatalogData
            {
                namespaces = new List<UdonApiNamespaceRecord>
                {
                    new() { name = "Example", exposed = new UdonApiTypeSet
                    {
                        classes = new List<UdonApiClassRecord>
                        {
                            new() { path = new List<string> { "Base" }, exposed = new UdonApiMemberExposure
                            {
                                instance = new UdonApiMemberSet { methods = new List<UdonApiMethodRecord>
                                {
                                    new() { name = "Foo", returnType = UdonApiTypeRef.Named("System.Void"),
                                        udonSignature = "ExampleBase.__Foo__SystemVoid" }
                                } }
                            } },
                            new() { path = new List<string> { "DerivedA" }, baseType = UdonApiTypeRef.Named("Example.Base") },
                            new() { path = new List<string> { "DerivedB" }, baseType = UdonApiTypeRef.Named("Example.Base") }
                        }
                    } }
                },
                unmodeledUdonSignatures = new List<string>
                {
                    "ExampleDerivedA.__Foo__SystemVoid", "ExampleDerivedB.__Foo__SystemVoid"
                }
            });
        }

        [Test]
        public void InheritedMember_IdentityUsesPhysicalHostAndPreservesDeclaration()
        {
            var model = InheritedModel();
            var first = model.Types.Single(type => type.RuntimeName == "Example.DerivedA").Members.Single();
            var second = model.Types.Single(type => type.RuntimeName == "Example.DerivedB").Members.Single();
            Assert.That(first.DeclaringRuntimeName, Is.EqualTo("Example.Base"));
            Assert.That(second.DeclaringRuntimeName, Is.EqualTo(first.DeclaringRuntimeName));
            Assert.That(first.ClrSignature, Is.EqualTo(second.ClrSignature));
            var firstIdentity = UdonBindingMemberSelector.FromPhysical(first);
            var secondIdentity = UdonBindingMemberSelector.FromPhysical(second);
            Assert.That(firstIdentity.Member, Is.EqualTo("Example.DerivedA.Foo"));
            Assert.That(secondIdentity.Member, Is.EqualTo("Example.DerivedB.Foo"));
            Assert.That(firstIdentity, Is.Not.EqualTo(secondIdentity));
        }

        [TestCase("DerivedA")]
        [TestCase("DerivedB")]
        [TestCase("Base")]
        public void InheritedMember_ReadonlyRuleMatchesOnlySelectedHost(string selectedHost)
        {
            var model = InheritedModel();
            var baseline = Apply(model, UdonBindingGenerationConfig.CreateDefault());
            var config = Load(Selector("instanceMethod", "Example." + selectedHost + ".Foo", ",\"parameters\":[]"));
            var generated = Apply(model, config);
            foreach (var type in generated.Types)
            {
                var member = type.Members.Single();
                var selected = type.Physical.RuntimeName == "Example." + selectedHost;
                Assert.That(config.receivers.MatchCount(UdonBindingMemberSelector.FromPhysical(member.Physical)),
                    Is.EqualTo(selected ? 1 : 0), type.Physical.RuntimeName);
                if (selected)
                    Assert.That(member.IsMutableReceiver, Is.False);
                else
                    Assert.That(member.IsMutableReceiver, Is.EqualTo(baseline.Types.Single(
                        candidate => candidate.Physical.RuntimeName == type.Physical.RuntimeName).Members.Single().IsMutableReceiver));
            }
        }

        [Test]
        public void InheritedMember_DeclaringRuleIsStaleWhenItsOwnHostIsExcluded()
        {
            var config = Load(Selector("instanceMethod", "Example.Base.Foo", ",\"parameters\":[]"));
            config.excludes.types = new[] { "Example.Base" };
            Assert.That(() => Apply(InheritedModel(), config),
                Throws.TypeOf<UdonBindingConfigurationException>().With.Message.Contains("did not match"));
            Assert.That(config.receivers.MatchCount(config.receivers.Selectors.Single()), Is.Zero);
        }

        [Test]
        public void BundledInheritedMember_GetTypeHasDistinctPhysicalHostIdentities()
        {
            var model = UdonBindingSourceModel.Load(StandardLibraryGenerator.DefaultCatalogPath);
            var first = model.Types.Single(type => type.RuntimeName == "UnityEngine.Vector2")
                .Members.Single(member => member.Name == "GetType");
            var second = model.Types.Single(type => type.RuntimeName == "UnityEngine.Vector3")
                .Members.Single(member => member.Name == "GetType");
            Assert.That(first.DeclaringRuntimeName, Is.EqualTo("System.Object"));
            Assert.That(second.DeclaringRuntimeName, Is.EqualTo(first.DeclaringRuntimeName));
            Assert.That(UdonBindingMemberSelector.FromPhysical(first).Member, Is.EqualTo("UnityEngine.Vector2.GetType"));
            Assert.That(UdonBindingMemberSelector.FromPhysical(second).Member, Is.EqualTo("UnityEngine.Vector3.GetType"));
            Assert.That(UdonBindingMemberSelector.FromPhysical(first), Is.Not.EqualTo(UdonBindingMemberSelector.FromPhysical(second)));
            foreach (var selected in new[] { first, second })
            {
                var config = UdonBindingGenerationConfig.Load(StandardLibraryGenerator.DefaultConfigurationPath);
                config.receivers.@readonly = config.receivers.@readonly.Concat(new[]
                {
                    new UdonBindingMemberSelectorRule { member = selected.HostRuntimeName + ".GetType",
                        kind = "instanceMethod", parameters = Array.Empty<UdonBindingSelectorParameterRule>() }
                }).ToArray();
                var generated = Apply(model, config);
                Assert.That(config.receivers.MatchCount(UdonBindingMemberSelector.FromPhysical(selected)), Is.EqualTo(1));
                var other = selected == first ? second : first;
                Assert.That(config.receivers.MatchCount(UdonBindingMemberSelector.FromPhysical(other)), Is.Zero);
                var receivers = generated.Types.Where(type => type.Physical.RuntimeName == first.HostRuntimeName ||
                    type.Physical.RuntimeName == second.HostRuntimeName).SelectMany(type => type.Members)
                    .Where(member => member.Physical.Name == "GetType").ToArray();
                Assert.That(receivers.Single(member => member.Physical.HostRuntimeName == selected.HostRuntimeName).IsMutableReceiver, Is.False);
                Assert.That(receivers.Single(member => member.Physical.HostRuntimeName == other.HostRuntimeName).IsMutableReceiver, Is.True);
            }
        }

        [TestCase("propertyGetter", "PropertyGetter")]
        [TestCase("propertySetter", "PropertySetter")]
        [TestCase("fieldGetter", "FieldGetter")]
        [TestCase("fieldSetter", "FieldSetter")]
        public void Matching_AccessorKindSelectsExactlyOneMember(string kind, string expected)
        {
            var model = Model();
            var name = kind.StartsWith("field", StringComparison.Ordinal) ? "field" : "x";
            var physical = model.Types.Single().Members.Where(member => member.Name == name).ToArray();
            Assert.That(physical[0].ClrSignature, Is.EqualTo(physical[1].ClrSignature));
            var members = Apply(model, Load(Selector(kind, "Example.Value." + name))).Types.Single().Members;
            Assert.That(members.Single(member => member.Physical.Kind.ToString() == expected).IsMutableReceiver, Is.False);
            Assert.That(members.Where(member => member.Physical.Kind.ToString() != expected).All(member => member.IsMutableReceiver), Is.True);
        }

        [TestCase("")]
        [TestCase("System.Int32")]
        [TestCase("System.String")]
        public void Matching_OverloadParameters(string type)
        {
            var parameters = type.Length == 0 ? "[]" : "[{\"type\":\"" + type + "\"}]";
            var config = Load(Selector("instanceMethod", "Example.Value.Foo", ",\"parameters\":" + parameters));
            var methods = Apply(Model(), config).Types.Single().Members.Where(member => member.Physical.Name == "Foo").ToArray();
            Assert.That(methods.Count(member => !member.IsMutableReceiver), Is.EqualTo(1));
            Assert.That(methods.Single(member => !member.IsMutableReceiver).Physical.Parameters.Count, Is.EqualTo(type.Length == 0 ? 0 : 1));
            if (type.Length > 0) Assert.That(methods.Single(member => !member.IsMutableReceiver).Physical.Parameters[0].type.runtimeName, Is.EqualTo(type));
        }

        [TestCase(null, "Value")]
        [TestCase("ref", "Ref")]
        [TestCase("out", "Out")]
        [TestCase("in", "In")]
        public void Matching_PreservesPassingMode(string passing, string expected)
        {
            var parameter = "[{\"type\":\"System.Int32\"" + (passing == null ? "" : ",\"passing\":\"" + passing + "\"") + "}]";
            var config = Load(Selector("instanceMethod", "Example.Value.Pass", ",\"parameters\":" + parameter));
            var methods = Apply(Model(), config).Types.Single().Members.Where(member => member.Physical.Name == "Pass").ToArray();
            Assert.That(methods.Count(member => !member.IsMutableReceiver), Is.EqualTo(1));
            Assert.That(methods.Single(member => !member.IsMutableReceiver).Physical.Parameters[0].passingMode, Is.EqualTo(expected));
        }

        [Test]
        public void Matching_RejectsStaleAmbiguousAndStaticAccessors()
        {
            Assert.That(() => Apply(Model(), Load(Selector("propertyGetter", "Example.Value.missing"))),
                Throws.TypeOf<UdonBindingConfigurationException>().With.Message.Contains("did not match"));
            Assert.That(() => Apply(Model(duplicate: true), Load(Selector("propertyGetter"))),
                Throws.TypeOf<UdonBindingConfigurationException>().With.Message.Contains("Ambiguous readonly receiver"));
            Assert.That(() => Apply(Model(staticAccessors: true), Load(Selector("propertyGetter"))),
                Throws.TypeOf<UdonBindingConfigurationException>().With.Message.Contains("instance member"));
        }

        [Test]
        public void BundledVector3_FieldAccessorsCanBeSelectedSeparately()
        {
            var model = UdonBindingSourceModel.Load(StandardLibraryGenerator.DefaultCatalogPath);
            foreach (var kind in new[] { "fieldGetter", "fieldSetter" })
            {
                var config = UdonBindingGenerationConfig.Load(StandardLibraryGenerator.DefaultConfigurationPath);
                config.receivers.@readonly = config.receivers.@readonly.Concat(new[]
                {
                    new UdonBindingMemberSelectorRule { member = "UnityEngine.Vector3.x", kind = kind }
                }).ToArray();
                var accessors = Apply(model, config).Types.Single(type => type.Physical.RuntimeName == "UnityEngine.Vector3")
                    .Members.Where(member => member.Physical.Name == "x").ToArray();
                Assert.That(accessors, Has.Length.EqualTo(2));
                var selected = kind == "fieldGetter" ? UdonApiMemberKind.FieldGetter : UdonApiMemberKind.FieldSetter;
                Assert.That(accessors.Single(member => member.Physical.Kind == selected).IsMutableReceiver, Is.False);
                Assert.That(accessors.Single(member => member.Physical.Kind != selected).IsMutableReceiver, Is.True);
            }
        }

        [Test]
        public void DefaultConfiguration_PreservesVector3ReadonlyRendering()
        {
            var config = UdonBindingGenerationConfig.Load(StandardLibraryGenerator.DefaultConfigurationPath);
            Assert.That(config.version, Is.EqualTo("5"));
            Assert.That(config.receivers.Selectors.Select(selector => selector.Member), Is.EquivalentTo(new[]
                { "UnityEngine.Vector3.ToString", "UnityEngine.Vector3.GetHashCode" }));
            var result = Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.UdonBindingGenerator.CreateDefault(StandardLibraryGenerator.DefaultCatalogPath,
                StandardLibraryGenerator.DefaultConfigurationPath).Generate();
            var source = result.Files["unity/vector3_binding.library.sobakasu"];
            var existing = File.ReadAllText(Path.Combine(StandardLibraryGenerator.PackageRoot,
                "StandardLibrary~", "unity", "vector3_binding.library.sobakasu"));
            Assert.That(source.Replace("\r\n", "\n"), Is.EqualTo(existing.Replace("\r\n", "\n")));
            Assert.That(source, Does.Contain("to_string(self)").And.Contain("get_hash_code(self)"));
            Assert.That(source, Does.Not.Contain("to_string(mutable self)").And.Not.Contain("get_hash_code(mutable self)"));
        }
    }
}
