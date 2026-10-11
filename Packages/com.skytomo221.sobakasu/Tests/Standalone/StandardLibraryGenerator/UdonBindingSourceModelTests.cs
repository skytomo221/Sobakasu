using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;

namespace Skytomo221.Sobakasu.Tests.Standalone
{
    public sealed class UdonBindingSourceModelTests
    {
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
