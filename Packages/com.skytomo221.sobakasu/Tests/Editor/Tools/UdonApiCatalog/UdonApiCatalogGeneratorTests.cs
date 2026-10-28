using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Tools.UdonApi;
using Skytomo221.Sobakasu.Tools.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal sealed class UdonApiCatalogGeneratorTests
    {
        [Test]
        public void Generate_EmitsCanonicalV2TypeAndMemberTree()
        {
            var result = new UdonApiCatalogGenerator().Generate(new[] { typeof(CatalogFixture) }, new FixtureExposure());
            Assert.That(result.Json, Does.Contain("\"formatVersion\": 2"));
            Assert.That(result.Json, Does.Not.Contain("\"types\""));
            Assert.That(result.Json, Does.Not.Contain("\"members\""));
            Assert.That(result.Json, Does.Contain("\"name\": \"" + typeof(CatalogFixture).Namespace + "\""));
            Assert.That(result.Json, Does.Contain("\"name\": \"Echo\""));
            Assert.That(result.Json, Does.Contain("\"returnType\": \"System.Int32\""));
            Assert.That(result.Json.EndsWith("\n", StringComparison.Ordinal), Is.True);
            Assert.That(result.Json, Does.Not.Contain("\r"));
            Assert.That(result.Json.StartsWith("\uFEFF", StringComparison.Ordinal), Is.False);
        }

        [Test]
        public void Generate_PreservesNestedClrIdentityAndDirectInheritance()
        {
            var result = new UdonApiCatalogGenerator().Generate(new[] { typeof(Outer<>.Inner<>) }, new FixtureExposure());
            Assert.That(result.Json, Does.Contain("\"Outer`1\""));
            Assert.That(result.Json, Does.Contain("\"Inner`1\""));
            Assert.That(result.Json, Does.Not.Contain("Outer`1.Inner`1"));
        }

        [Test]
        public void Generate_UsesClosureAndDeclarationOwnedSourceMembers()
        {
            var result = new UdonApiCatalogGenerator().Generate(new[] { typeof(CatalogFixture) }, new FixtureExposure());
            var catalog = result.Catalog;
            var testNamespace = catalog.namespaces.Single(item => item.name == typeof(CatalogFixture).Namespace);
            var host = testNamespace.exposed.classes.Single(item => item.path.SequenceEqual(new[] { nameof(CatalogFixture) }));
            Assert.That(host.exposed, Is.Not.Null, result.Json);
            Assert.That(host.exposed.constructors, Is.Not.Null);
            Assert.That(host.exposed.instance.methods.Any(item => item.name == "Echo"), Is.True);
            Assert.That(host.exposed.instance.methods.Count(item => item.name == "Inherited"), Is.EqualTo(0));
            var paramsMethod = host.exposed.instance.methods.Single(item => item.name == "Params");
            Assert.That(paramsMethod.parameters.Single().@params, Is.True);
            Assert.That(paramsMethod.udonSignature, Is.Not.Empty,
                "Generator policy failures must not erase actual Udon exposure.");
            Assert.That(catalog.unmodeledUdonSignatures, Does.Not.Contain(paramsMethod.udonSignature));
            Assert.That(host.baseType, Is.Not.Null, result.Json);
            Assert.That(host.baseType.named, Is.EqualTo(typeof(CatalogFixtureBase).FullName));
            Assert.That(host.exposed.instance.propertySetters.Single(item => item.name == "Value").parameters, Is.Null);
            Assert.That(host.unexposed?.instance?.events, Is.Not.Null, result.Json);
            Assert.That(host.unexposed.instance.events.Single().name, Is.EqualTo("Changed"));
            Assert.That(result.Json, Does.Not.Contain("abiParameters"));
            Assert.That(result.Json, Does.Not.Contain("abiReturnType"));
            Assert.That(catalog.capabilities.arrays, Is.Not.Empty);
        }

        [Test]
        public void Serialize_RoundTripsByteForByte()
        {
            var result = new UdonApiCatalogGenerator().Generate(new[] { typeof(CatalogFixture) }, new FixtureExposure());
            var roundTrip = UdonApiCatalogGenerator.Serialize(UdonApiCatalogGenerator.Deserialize(result.Json));
            Assert.That(roundTrip, Is.EqualTo(result.Json));
        }

        [Test]
        public void Generate_ModelsSpecialObjectMethodsAtTheirClrDeclaration()
        {
            var result = new UdonApiCatalogGenerator().Generate(new[] { typeof(object) }, new ObjectExposure());
            var objectRecord = result.Catalog.namespaces.Single(item => item.name == "System")
                .exposed.classes.Single(item => item.path.SequenceEqual(new[] { "Object" }));
            var getType = objectRecord.exposed.instance.methods.Single(item => item.name == "GetType");
            Assert.That(getType.returnType.named, Is.EqualTo("System.Type"));
            Assert.That(getType.udonSignature, Is.EqualTo("SystemObject.__GetType__SystemType"));
            Assert.That(result.Catalog.unmodeledUdonSignatures?.Contains(getType.udonSignature), Is.Not.True);
        }

        public sealed class Outer<T> { public sealed class Inner<U> { } }

        private sealed class FixtureExposure : IUdonApiExposure
        {
            private readonly HashSet<string> _signatures = new(StringComparer.Ordinal);
            public IReadOnlyCollection<string> ExposedSignatures => _signatures;
            public bool IsTypeExposed(Type type) => type == typeof(CatalogFixture) || type == typeof(CatalogFixtureBase) || type == typeof(Outer<>) || type == typeof(Outer<>.Inner<>);
            public bool IsMemberExposed(string signature) { _signatures.Add(signature); return true; }
        }

        private sealed class ObjectExposure : IUdonApiExposure
        {
            public IReadOnlyCollection<string> ExposedSignatures => Array.Empty<string>();
            public bool IsTypeExposed(Type type) => type == typeof(object);
            public bool IsMemberExposed(string signature) => true;
        }
    }

    public class CatalogFixtureBase { public void Inherited() { } }
    public sealed class CatalogFixture : CatalogFixtureBase
    {
        public int Echo(int value) => value;
        public int Params(params int[] values) => values.Length;
        public int Value { get; set; }
        public int Field;
        public event EventHandler Changed;
    }
}
