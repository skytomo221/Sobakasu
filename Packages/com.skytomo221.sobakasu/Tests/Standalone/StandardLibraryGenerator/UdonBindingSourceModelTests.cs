using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;

namespace Skytomo221.Sobakasu.Tests.Standalone
{
    public sealed class UdonBindingSourceModelTests
    {
        [Test]
        public void PhysicalSelector_PreservesTypesPassingAndGenericArityWithoutSignatureParsing()
        {
            var catalog = CreateCatalog();
            var method = catalog.namespaces[0].exposed.classes[0].exposed.@static.methods[0];
            method.genericParameters = new List<UdonApiGenericParameterRecord> { new() { name = "T" } };
            method.parameters = new List<UdonApiParameterRecord>
            {
                new() { name = "array", type = new UdonApiTypeRef { array = UdonApiTypeRef.Named("System.Int32"), rank = 2 }, passing = "in" },
                new() { name = "generic", type = new UdonApiTypeRef { generic = "Example.Box`1", arguments = new List<UdonApiTypeRef>
                    { new() { array = new UdonApiTypeRef { methodParameter = 0 } } } }, @params = true }
            };
            var member = UdonBindingSourceModel.FromCatalog(catalog).Types.Single(type => type.RuntimeName == "Example.Api").Members.Single();
            var identity = UdonBindingMemberSelector.FromPhysical(member);
            Assert.That(identity.Member, Is.EqualTo("Example.Api.Find"));
            Assert.That(identity.GenericArity, Is.EqualTo(1));
            var expected = new UdonBindingMemberSelector("Example.Api.Find", UdonApiMemberKind.StaticMethod, true,
                method.parameters.Select(parameter => new UdonBindingParameterIdentity(parameter.type, parameter.passing)), 1);
            Assert.That(identity, Is.EqualTo(expected));
            Assert.That(identity.GetHashCode(), Is.EqualTo(expected.GetHashCode()));
            method.parameters[0].name = "different";
            method.parameters[1].@params = false;
            var renamed = UdonBindingSourceModel.FromCatalog(catalog).Types.Single(type => type.RuntimeName == "Example.Api").Members.Single();
            Assert.That(UdonBindingMemberSelector.FromPhysical(renamed), Is.EqualTo(identity));
            method.parameters[0].type.rank = 3;
            var differentRank = UdonBindingSourceModel.FromCatalog(catalog).Types.Single(type => type.RuntimeName == "Example.Api").Members.Single();
            Assert.That(UdonBindingMemberSelector.FromPhysical(differentRank), Is.Not.EqualTo(identity));
        }

        [Test]
        public void BundledGenerator_PreservesNestedAndLegacyPhysicalSignatureContracts()
        {
            var expected = File.ReadAllLines(Path.Combine(StandardLibraryGenerator.PackageRoot,
                "Tests", "Editor", "TestData", "StandardLibraryGenerator", "nested-runtime-identity-covered-signatures.txt"));
            Assert.That(expected, Has.Length.EqualTo(1042));
            var report = UdonBindingGenerator.CreateDefault(
                StandardLibraryGenerator.DefaultCatalogPath, StandardLibraryGenerator.DefaultConfigurationPath)
                .Generate().Report;
            Assert.That(expected.Except(report.physical_covered_signatures), Is.Empty);
            var legacyEnvelope = File.ReadAllLines(Path.Combine(StandardLibraryGenerator.PackageRoot,
                "Tests", "Editor", "TestData", "StandardLibraryGenerator", "legacy-covered-physical-envelope.txt"));
            Assert.That(legacyEnvelope, Has.Length.EqualTo(9332));
            Assert.That(legacyEnvelope.Except(report.physical_declared_covered_signatures), Is.Empty);
            Assert.That(report.physical_declared_api_total, Is.EqualTo(9528));
            Assert.That(report.udon_exposed_signature_total, Is.EqualTo(35213));
            Assert.That(report.physical_api_total, Is.EqualTo(32898));
            Assert.That(report.physical_api_total + report.non_extern_udon_signatures.Count,
                Is.EqualTo(report.udon_exposed_signature_total));
            Assert.That(report.non_extern_udon_signatures, Does.Contain("Branch"));
            Assert.That(report.physical_unsupported_signatures, Does.Not.Contain("Branch"));
        }

        [Test]
        public void CatalogV2_InheritanceKeepsAccessorKindsAndPassingModesDistinct()
        {
            var catalog = CreateCatalog();
            var parent = catalog.namespaces[0].exposed.classes[0];
            parent.exposed.instance = parent.exposed.@static;
            parent.exposed.@static = null;
            var child = catalog.namespaces[0].exposed.classes[1];
            child.baseType = UdonApiTypeRef.Named("Example.Api");
            child.exposed = new UdonApiMemberExposure { instance = new UdonApiMemberSet
            {
                methods = new List<UdonApiMethodRecord> { new() { name = "Find",
                    parameters = new List<UdonApiParameterRecord> { new() { name = "value",
                        type = UdonApiTypeRef.Named("System.String"), passing = "ref" } },
                    returnType = UdonApiTypeRef.Named("System.Int32"),
                    udonSignature = "ExampleOuterInner.__Find__SystemStringRef__SystemInt32" } },
                propertyGetters = new List<UdonApiPropertyAccessorRecord> { new() { name = "Find",
                    type = UdonApiTypeRef.Named("System.Int32"), udonSignature = "ExampleOuterInner.__get_Find__SystemInt32" } },
                propertySetters = new List<UdonApiPropertyAccessorRecord> { new() { name = "Find",
                    type = UdonApiTypeRef.Named("System.Int32"), udonSignature = "ExampleOuterInner.__set_Find__SystemInt32__SystemVoid" } }
            } };
            var surface = UdonBindingSourceModel.FromCatalog(catalog).Types
                .Single(type => type.RuntimeName == "Example.Outer+Inner").Members;
            Assert.That(surface, Has.Count.EqualTo(4));
            Assert.That(surface.Count(member => member.Kind == UdonApiMemberKind.InstanceMethod), Is.EqualTo(2));
            Assert.That(surface.Count(member => member.Kind == UdonApiMemberKind.PropertyGetter), Is.EqualTo(1));
            Assert.That(surface.Count(member => member.Kind == UdonApiMemberKind.PropertySetter), Is.EqualTo(1));
        }

        [Test]
        public void CatalogV2_PreservesConstructedBaseIdentityAndTraversesItsAncestry()
        {
            var catalog = CreateCatalog();
            var ns = catalog.namespaces[0];
            ns.exposed.classes[0].exposed.instance = ns.exposed.classes[0].exposed.@static;
            ns.exposed.classes[0].exposed.@static = null;
            ns.unexposed.classes.Add(new UdonApiClassRecord
            {
                path = new List<string> { "GenericBase`1" },
                genericParameters = new List<UdonApiGenericParameterRecord> { new() { name = "T" } },
                baseType = UdonApiTypeRef.Named("Example.Api")
            });
            ns.exposed.classes[1].baseType = new UdonApiTypeRef { generic = "Example.GenericBase`1",
                arguments = new List<UdonApiTypeRef> { UdonApiTypeRef.Named("Example.Outer+Inner") } };
            var child = UdonBindingSourceModel.FromCatalog(catalog).Types
                .Single(type => type.RuntimeName == "Example.Outer+Inner");
            Assert.That(child.Record.baseRuntimeName, Is.EqualTo("Example.GenericBase`1"));
            Assert.That(child.Record.baseType.arguments[0].runtimeName, Is.EqualTo("Example.Outer+Inner"));
            Assert.That(child.Members.Single().DeclaringRuntimeName, Is.EqualTo("Example.Api"));
        }

        [Test]
        public void BundledCatalog_PreservesAll113PreviouslyUnmodeledPhysicalDeclarations()
        {
            var catalog = UdonApiCatalogReader.Parse(File.ReadAllText(StandardLibraryGenerator.DefaultCatalogPath));
            var originalUnmodeled = new HashSet<string>(catalog.unmodeledUdonSignatures, StringComparer.Ordinal);
            var model = UdonBindingSourceModel.FromCatalog(catalog);
            var expected = File.ReadAllLines(Path.Combine(StandardLibraryGenerator.PackageRoot,
                "Tests", "Editor", "TestData", "StandardLibraryGenerator", "catalog-migration-regression-signatures.txt"));
            Assert.That(expected, Has.Length.EqualTo(113));
            var physical = model.Types.SelectMany(type => type.Members)
                .Select(member => member.ExternSignature).ToHashSet(StringComparer.Ordinal);
            Assert.That(expected.Except(physical), Is.Empty);
            Assert.That(expected.Intersect(model.UnmatchedUdonSignatures), Is.Empty);
            Assert.That(model.ReconciledSignatures,
                Is.EquivalentTo(expected.Where(originalUnmodeled.Contains)));
            Assert.That(UdonApiCatalogExposureReconciler.Reconcile(catalog), Is.Empty,
                "Compatibility projection must be idempotent.");
        }

        [Test]
        public void BundledCatalog_DollyCartCombinesDirectComponentAndObjectSurfaces()
        {
            var model = UdonBindingSourceModel.Load(StandardLibraryGenerator.DefaultCatalogPath);
            var cart = model.Types.Single(type => type.RuntimeName == "Cinemachine.CinemachineDollyCart");
            Assert.That(cart.Members.Any(member => member.Kind == UdonApiMemberKind.FieldGetter &&
                member.DeclaringRuntimeName == cart.RuntimeName), Is.True);
            var component = cart.Members.First(member => member.Name == "GetComponent" &&
                member.DeclaringRuntimeName == "UnityEngine.Component");
            Assert.That(component.HostRuntimeName, Is.EqualTo(cart.RuntimeName));
            Assert.That(component.ExternSignature, Does.StartWith("CinemachineCinemachineDollyCart."));
            Assert.That(model.UdonExposedSignatures, Does.Contain(component.ExternSignature));
            Assert.That(cart.Members.Any(member => member.Name == "GetType" &&
                member.DeclaringRuntimeName == "System.Object"), Is.True);
        }

        [Test]
        public void Generator_IsolatesMemberValidationFailureFromValueTypeOwner()
        {
            var catalog = new UdonApiCatalogData
            {
                namespaces = new List<UdonApiNamespaceRecord>
                {
                    new() { name = "System", exposed = new UdonApiTypeSet
                    {
                        structs = new List<UdonApiStructRecord>
                        {
                            new() { path = new List<string> { "DateTime" }, exposed = new UdonApiMemberExposure
                            {
                                instance = new UdonApiMemberSet { methods = new List<UdonApiMethodRecord>
                                {
                                    new() { name = "AddDays", returnType = UdonApiTypeRef.Named("System.DateTime"),
                                        parameters = new List<UdonApiParameterRecord>
                                        { new() { name = "value", type = UdonApiTypeRef.Named("System.Double") } },
                                        udonSignature = "SystemDateTime.__AddDays__SystemDouble__SystemDateTime" },
                                    new() { name = "MissingMemberOnly", returnType = UdonApiTypeRef.Named("System.DateTime"),
                                        udonSignature = "SystemDateTime.__MissingMemberOnly__SystemDateTime" }
                                } }
                            } }
                        }
                    } }
                }
            };
            var model = UdonBindingSourceModel.FromCatalog(catalog);
            var environment = SobakasuCompilationEnvironment.FromUdonApiCatalogJson(
                File.ReadAllText(StandardLibraryGenerator.DefaultCatalogPath));
            var generator = new UdonBindingGenerator(model,
                new SobakasuBindingRenderer(new UdonBindingTypeFormatter(model)), environment,
                validateGeneratedBindings: true);
            var result = generator.Generate();
            Assert.That(result.Report.types_generated, Is.EqualTo(1));
            Assert.That(result.Report.members_generated, Is.EqualTo(1));
            Assert.That(result.Report.skipped_members.Single().extern_signature,
                Is.EqualTo("SystemDateTime.__MissingMemberOnly__SystemDateTime"));
            Assert.That(string.Join("\n", result.Files.Values), Does.Contain("add_days(mutable self"));
        }

        [Test]
        public void CatalogV2_RecordLayoutDoesNotChangePhysicalSignatureUniverse()
        {
            var members = CreateCatalog();
            var signature = members.namespaces[0].exposed.classes[0].exposed.@static.methods[0].udonSignature;
            members.unmodeledUdonSignatures.AddRange(new[] { "Array.__get", "Array.__set", "Array.__length" });
            var capabilities = CreateCatalog();
            capabilities.namespaces[0].exposed.classes[0].exposed.@static.methods.Clear();
            capabilities.capabilities = new UdonApiCapabilities
            {
                arrays = new List<ArrayCapabilityRecord>
                {
                    new() { type = new UdonApiTypeRef { array = UdonApiTypeRef.Named("System.String") },
                        constructor = signature, getter = "Array.__get", setter = "Array.__set", length = "Array.__length" }
                }
            };
            Assert.That(UdonBindingSourceModel.FromCatalog(capabilities).UdonExposedSignatures,
                Is.EquivalentTo(UdonBindingSourceModel.FromCatalog(members).UdonExposedSignatures));
        }

        [Test]
        public void CatalogV2_NestedIdentitiesSurviveEveryTypeRefPosition()
        {
            var catalog = CreateCatalog();
            var method = catalog.namespaces[0].exposed.classes[0].exposed.@static.methods[0];
            method.returnType = UdonApiTypeRef.Named("Example.Outer+Inner");
            method.parameters = new List<UdonApiParameterRecord>
            {
                new() { name = "value", type = UdonApiTypeRef.Named("Example.Outer+Inner") },
                new() { name = "array", type = new UdonApiTypeRef { array = UdonApiTypeRef.Named("Example.Outer+Inner") } },
                new() { name = "generic", type = new UdonApiTypeRef { generic = "Example.Box`1",
                    arguments = new List<UdonApiTypeRef> { UdonApiTypeRef.Named("Example.Outer+Inner") } } }
            };
            var member = UdonBindingSourceModel.FromCatalog(catalog).Types
                .Single(type => type.RuntimeName == "Example.Api").Members.Single();
            Assert.That(member.ReturnType.runtimeName, Is.EqualTo("Example.Outer+Inner"));
            Assert.That(member.Parameters[0].type.runtimeName, Is.EqualTo("Example.Outer+Inner"));
            Assert.That(member.Parameters[1].type.element.runtimeName, Is.EqualTo("Example.Outer+Inner"));
            Assert.That(member.Parameters[2].type.arguments[0].runtimeName, Is.EqualTo("Example.Outer+Inner"));
        }

        [Test]
        public void CatalogV2_ProjectsInheritedSurfaceWithCanonicalOwnerAndHostAbi()
        {
            var catalog = CreateCatalog();
            var declarations = catalog.namespaces[0].exposed.classes;
            var parent = declarations[0];
            parent.exposed.instance = parent.exposed.@static;
            parent.exposed.@static = null;
            declarations[1].baseType = UdonApiTypeRef.Named("Example.Api");
            catalog.unmodeledUdonSignatures.Add("ExampleOuterInner.__Find__SystemString__SystemInt32");

            var model = UdonBindingSourceModel.FromCatalog(catalog);
            var child = model.Types.Single(type => type.RuntimeName == "Example.Outer+Inner");
            Assert.That(child.Record.baseRuntimeName, Is.EqualTo("Example.Api"));
            var inherited = child.Members.Single();
            Assert.That(inherited.HostRuntimeName, Is.EqualTo("Example.Outer+Inner"));
            Assert.That(inherited.DeclaringRuntimeName, Is.EqualTo("Example.Api"));
            Assert.That(inherited.ExternSignature,
                Is.EqualTo("ExampleOuterInner.__Find__SystemString__SystemInt32"));
            Assert.That(parent.exposed.instance.methods, Has.Count.EqualTo(1));
            Assert.That(declarations[1].exposed, Is.Null, "Projection must not flatten the persisted catalog.");
        }

        [Test]
        public void CatalogV2_DerivedDeclarationWinsWithoutCollapsingOverloads()
        {
            var catalog = CreateCatalog();
            var declarations = catalog.namespaces[0].exposed.classes;
            var parent = declarations[0];
            parent.exposed.instance = parent.exposed.@static;
            parent.exposed.@static = null;
            var child = declarations[1];
            child.baseType = UdonApiTypeRef.Named("Example.Api");
            child.exposed = new UdonApiMemberExposure
            {
                instance = new UdonApiMemberSet
                {
                    methods = new List<UdonApiMethodRecord>
                    {
                        new() { name = "Find", parameters = new List<UdonApiParameterRecord>
                        { new() { name = "name", type = UdonApiTypeRef.Named("System.String") } },
                            returnType = UdonApiTypeRef.Named("System.Int32"),
                            udonSignature = "ExampleOuterInner.__Find__SystemString__SystemInt32" },
                        new() { name = "Find", parameters = new List<UdonApiParameterRecord>
                        { new() { name = "index", type = UdonApiTypeRef.Named("System.Int32") } },
                            returnType = UdonApiTypeRef.Named("System.Int32"),
                            udonSignature = "ExampleOuterInner.__Find__SystemInt32__SystemInt32" }
                    }
                }
            };
            var surface = UdonBindingSourceModel.FromCatalog(catalog).Types
                .Single(type => type.RuntimeName == "Example.Outer+Inner").Members;
            Assert.That(surface, Has.Count.EqualTo(2));
            Assert.That(surface.All(member => member.DeclaringRuntimeName == "Example.Outer+Inner"), Is.True);
        }

        [Test]
        public void CatalogV2_PreservesNestedRuntimeIdentityAndIndexesUnexposedMetadata()
        {
            var model = UdonBindingSourceModel.FromCatalog(CreateCatalog());

            Assert.That(model.Types, Has.Count.EqualTo(2));
            Assert.That(model.TypesByRuntimeName, Contains.Key("Example.Outer+Inner"));
            var nested = model.Types.Single(type => type.RuntimeName == "Example.Outer+Inner");
            Assert.That(nested.QualifiedName, Is.EqualTo("Example.Outer.Inner"));
            Assert.That(nested.WrapperName, Is.EqualTo("Inner"));
            Assert.That(model.TypesByRuntimeName, Contains.Key("Example.MetadataOnly"));
            Assert.That(model.UnexposedTypeNames, Does.Contain("Example.MetadataOnly"));
            Assert.That(model.Types, Has.None.Matches<UdonBindingSourceType>(
                type => type.RuntimeName == "Example.MetadataOnly"));
        }

        [Test]
        public void CatalogV2_DerivesEffectiveStaticContainerAndPhysicalMemberSurface()
        {
            var model = UdonBindingSourceModel.FromCatalog(CreateCatalog());
            var api = model.Types.Single(type => type.RuntimeName == "Example.Api");

            // Api is deliberately not persisted as abstract+sealed. As in the old
            // reflection policy, an exposed type whose declared non-constructor API
            // is entirely static is still a top-level API container (Debug/Mathf style).
            Assert.That(api.Record.isStaticApiContainer, Is.False);
            Assert.That(api.IsStaticApiContainer, Is.True);
            Assert.That(api.Members, Has.Count.EqualTo(1));
            Assert.That(api.Members[0].Kind, Is.EqualTo(UdonApiMemberKind.StaticMethod));
            Assert.That(api.Members[0].ClrSignature, Is.EqualTo("Example.Api.Find(System.String)"));
            Assert.That(api.Members[0].ExternSignature,
                Is.EqualTo("ExampleApi.__Find__SystemString__SystemInt32"));
            Assert.That(model.UdonExposedSignatures,
                Does.Contain("ExampleApi.__Find__SystemString__SystemInt32"));
            Assert.That(model.UdonExposedSignatures,
                Does.Contain("Unmodeled.__Signature__SystemVoid"));
        }

        [Test]
        public void UnsupportedCatalogVersion_IsRejectedWithoutDiscoveryFallback()
        {
            var catalog = CreateCatalog();
            catalog.formatVersion = 1;

            var error = Assert.Throws<InvalidDataException>(() =>
                UdonBindingSourceModel.FromCatalog(catalog));

            Assert.That(error.Message, Does.Contain("Regenerate it manually"));
            Assert.That(error.Message,
                Does.Contain("Window/Sobakasu/Build Udon API Catalog"));
        }

        [Test]
        public void MissingCatalog_IsRejectedWithoutDiscoveryFallback()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-missing-udon-api-catalog-" + Guid.NewGuid().ToString("N") + ".json");

            var error = Assert.Throws<FileNotFoundException>(() =>
                UdonBindingSourceModel.Load(path));

            Assert.That(error.Message,
                Does.Contain("Window/Sobakasu/Build Udon API Catalog"));
        }

        [Test]
        public void CatalogReader_RejectsDuplicateJsonProperties()
        {
            const string json = "{\"formatVersion\":2,\"formatVersion\":2}";
            Assert.Throws<InvalidDataException>(() => UdonApiCatalogReader.Parse(json));
        }

        private static UdonApiCatalogData CreateCatalog()
        {
            return new UdonApiCatalogData
            {
                formatVersion = 2,
                target = new UdonApiCatalogTarget
                {
                    unityVersion = "2022.3.22f1",
                    vrchatSdkVersion = "test"
                },
                namespaces = new List<UdonApiNamespaceRecord>
                {
                    new()
                    {
                        name = "Example",
                        exposed = new UdonApiTypeSet
                        {
                            classes = new List<UdonApiClassRecord>
                            {
                                new()
                                {
                                    path = new List<string> { "Api" },
                                    exposed = new UdonApiMemberExposure
                                    {
                                        @static = new UdonApiMemberSet
                                        {
                                            methods = new List<UdonApiMethodRecord>
                                            {
                                                new()
                                                {
                                                    name = "Find",
                                                    parameters = new List<UdonApiParameterRecord>
                                                    {
                                                        new()
                                                        {
                                                            name = "name",
                                                            type = UdonApiTypeRef.Named("System.String")
                                                        }
                                                    },
                                                    returnType = UdonApiTypeRef.Named("System.Int32"),
                                                    udonSignature = "ExampleApi.__Find__SystemString__SystemInt32"
                                                }
                                            }
                                        }
                                    }
                                },
                                new()
                                {
                                    path = new List<string> { "Outer", "Inner" }
                                }
                            }
                        },
                        unexposed = new UdonApiTypeSet
                        {
                            classes = new List<UdonApiClassRecord>
                            {
                                new()
                                {
                                    path = new List<string> { "MetadataOnly" }
                                }
                            }
                        }
                    }
                },
                unmodeledUdonSignatures = new List<string>
                {
                    "Unmodeled.__Signature__SystemVoid"
                }
            };
        }
    }
}
