using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;
using Skytomo221.Sobakasu.Tools.UdonApi;
using Skytomo221.Sobakasu.Tools.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal sealed class UdonApiCatalogGeneratorTests
    {
        [Test]
        public void Generate_SeparatesExposedAndUnexposedClrTypes()
        {
            var generator = new UdonApiCatalogGenerator();
            var result = generator.Generate(
                new[]
                {
                    typeof(UdonBindingGeneratorFixture),
                    typeof(UdonApiCatalogUnexposedFixture),
                    typeof(UdonApiCatalogUnsupportedFixture),
                    typeof(UdonApiCatalogMetadataFixture),
                    typeof(UdonApiEnumFixture),
                    typeof(IUdonApiCatalogSupertypeFixture),
                    typeof(UdonApiCatalogBaseFixture),
                    typeof(UdonApiCatalogDerivedFixture)
                },
                new CatalogFixtureExposure());

            var exposed = FindType(result.Catalog, typeof(UdonBindingGeneratorFixture));
            var exposedRuntimeName = GetRuntimeName(typeof(UdonBindingGeneratorFixture));
            var unexposedRuntimeName = GetRuntimeName(typeof(UdonApiCatalogUnexposedFixture));
            Assert.That(exposed, Is.Not.Null);
            Assert.That(result.Catalog.unexposedClrTypeNames, Does.Not.Contain(exposedRuntimeName));
            Assert.That(FindType(result.Catalog, typeof(UdonApiCatalogUnexposedFixture)), Is.Null);
            Assert.That(result.Catalog.unexposedClrTypeNames, Does.Contain(unexposedRuntimeName));
            Assert.That(result.Catalog.formatVersion, Is.EqualTo(1));
            foreach (var type in result.Catalog.types)
                Assert.That(result.Catalog.unexposedClrTypeNames, Does.Not.Contain(type.runtimeName));

            Assert.That(result.Json, Does.Not.Contain("\"udonExposed\""));

            var hidden = FindUnexposedMember(result.Catalog, "Hidden");
            var hiddenMethod = typeof(UdonBindingGeneratorFixture).GetMethod("Hidden");
            Assert.That(hidden.hostType, Is.EqualTo(exposedRuntimeName));
            Assert.That(hidden.name, Is.EqualTo("Hidden"));
            Assert.That(hidden.kind, Is.EqualTo("InstanceMethod"));
            Assert.That(hidden.externSignature,
                Is.EqualTo(UdonExternSignatureFormatter.GetUdonMethodName(hiddenMethod)));
            Assert.That(result.Catalog.members.Exists(candidate => candidate.name == "Hidden"), Is.False);

            var @params = FindMember(result.Catalog, "Params");
            Assert.That(@params.clrSignature, Does.Contain(".Params("));
            Assert.That(@params.externSignature, Is.Not.Empty);
            Assert.That(result.Catalog.unexposedMembers.Exists(
                candidate => candidate.name == "Params"), Is.False);

            Assert.That(result.Catalog.members.Exists(
                candidate => candidate.name == "UnexposedTypeMember"), Is.False);
            Assert.That(result.Catalog.unexposedMembers.Exists(
                candidate => candidate.name == "UnexposedTypeMember"), Is.False);

            var unexposedMembersJson = GetUnexposedMembersJson(result.Json);
            Assert.That(unexposedMembersJson, Does.Not.Contain("\"clrSignature\""));
            Assert.That(unexposedMembersJson, Does.Not.Contain("\"clrDeclaringType\""));
            Assert.That(unexposedMembersJson, Does.Not.Contain("\"isStatic\""));
            Assert.That(unexposedMembersJson, Does.Not.Contain("\"genericParameters\""));
            Assert.That(unexposedMembersJson, Does.Not.Contain("\"abiParameters\""));
            Assert.That(unexposedMembersJson, Does.Not.Contain("\"abiReturnType\""));

            var enumType = FindType(result.Catalog, typeof(UdonApiEnumFixture));
            Assert.That(enumType.@enum.underlyingType.runtimeName, Is.EqualTo("System.Int32"));
            Assert.That(enumType.@enum.constants, Has.Count.EqualTo(3));
            Assert.That(enumType.@enum.constants.Find(constant => constant.name == "Second").value,
                Is.EqualTo("20"));

            var constrained = FindMember(result.Catalog, "Constrained");
            Assert.That(constrained.genericParameters[0].referenceTypeConstraint, Is.True);
            Assert.That(constrained.genericParameters[0].defaultConstructorConstraint, Is.True);
            Assert.That(constrained.genericParameters[0].typeConstraints, Has.Count.EqualTo(1));
            Assert.That(constrained.genericParameters[0].typeConstraints[0].runtimeName,
                Is.EqualTo("System.IDisposable"));

            var derived = FindType(result.Catalog, typeof(UdonApiCatalogDerivedFixture));
            var supertypeNames = new List<string>();
            foreach (var supertype in derived.supertypes)
                supertypeNames.Add(supertype.runtimeName);
            Assert.That(supertypeNames, Does.Contain(GetRuntimeName(typeof(UdonApiCatalogBaseFixture))));
            Assert.That(supertypeNames, Does.Contain(GetRuntimeName(typeof(IUdonApiCatalogSupertypeFixture))));
            Assert.That(supertypeNames, Does.Contain("System.Object"));
            Assert.That(supertypeNames, Does.Not.Contain(GetRuntimeName(typeof(UdonApiCatalogDerivedFixture))));
        }

        [Test]
        public void Generate_RecordsGenericAbiAndStructuredTypeReferences()
        {
            var result = new UdonApiCatalogGenerator().Generate(
                new[] { typeof(UdonBindingGeneratorFixture) },
                new CatalogFixtureExposure());

            var generic = FindMember(result.Catalog, "Generic");
            Assert.That(generic.genericParameters, Has.Count.EqualTo(1));
            Assert.That(generic.abiParameters[0].passingMode, Is.EqualTo("GenericTypeArgument"));
            Assert.That(generic.abiParameters[0].name, Is.EqualTo("T"));
            Assert.That(generic.abiParameters[1].name, Is.EqualTo("value"));
            Assert.That(generic.abiParameters[1].type.kind, Is.EqualTo("GenericParameter"));
            Assert.That(generic.abiReturnType.kind, Is.EqualTo("GenericParameter"));

            var array = FindMember(result.Catalog, "GenericArray");
            Assert.That(array.abiReturnType.kind, Is.EqualTo("Array"));
            Assert.That(array.abiReturnType.element.kind, Is.EqualTo("GenericParameter"));

            var capability = result.Catalog.capabilities.arrays.Find(candidate =>
                candidate.arrayType.kind == "Array" &&
                candidate.arrayType.element.runtimeName == "System.String");
            Assert.That(capability, Is.Not.Null);
            Assert.That(capability.constructorSignature, Is.Not.Empty);
            Assert.That(capability.getterSignature, Is.Not.Empty);
            Assert.That(capability.setterSignature, Is.Not.Empty);
            Assert.That(capability.lengthSignature, Is.Not.Empty);
        }

        [Test]
        public void Serialize_IsCanonicalAndRoundTrips()
        {
            var catalog = new UdonApiCatalogData
            {
                target = new UdonApiCatalogTarget
                {
                    unityVersion = "2022.3.0f1",
                    vrchatSdkVersion = "3.10.4"
                },
                types = new List<UdonApiTypeRecord>
                {
                    new()
                    {
                        runtimeName = "Z.Type",
                        supertypes = new List<ExternTypeRef>
                        {
                            new() { kind = "Named", runtimeName = "Z.Super" },
                            new() { kind = "Named", runtimeName = "A.Super" }
                        }
                    },
                    new() { runtimeName = "A.Type" }
                },
                unexposedClrTypeNames = new List<string>
                {
                    "Z.Unexposed",
                    "A.Unexposed"
                },
                unexposedMembers = new List<UdonApiUnexposedMemberRecord>
                {
                    new()
                    {
                        hostType = "Z.Host",
                        name = "Member",
                        kind = "InstanceMethod",
                        externSignature = "z"
                    },
                    new()
                    {
                        hostType = "A.Host",
                        name = "Member",
                        kind = "StaticMethod",
                        externSignature = "z"
                    },
                    new()
                    {
                        hostType = "A.Host",
                        name = "Member",
                        kind = "InstanceMethod",
                        externSignature = "z"
                    },
                    new()
                    {
                        hostType = "A.Host",
                        name = "Member",
                        kind = "InstanceMethod",
                        externSignature = "a"
                    }
                },
                unmatchedUdonSignatures = new List<string> { "z", "a" }
            };

            var first = UdonApiCatalogGenerator.Serialize(catalog);
            var second = UdonApiCatalogGenerator.Serialize(
                UdonApiCatalogGenerator.Deserialize(first));

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Not.Contain("\r"));
            Assert.That(first.EndsWith("\n", StringComparison.Ordinal), Is.True);
            Assert.That(first, Does.Not.Contain("compilerSupported"));
            Assert.That(first, Does.Not.Contain("unsupportedReason"));
            Assert.That(first.IndexOf("A.Type", StringComparison.Ordinal),
                Is.LessThan(first.IndexOf("Z.Type", StringComparison.Ordinal)));
            Assert.That(first.IndexOf("A.Unexposed", StringComparison.Ordinal),
                Is.LessThan(first.IndexOf("Z.Unexposed", StringComparison.Ordinal)));

            var unexposedMembersJson = GetUnexposedMembersJson(first);
            Assert.That(unexposedMembersJson.IndexOf("\"hostType\": \"A.Host\"", StringComparison.Ordinal),
                Is.LessThan(unexposedMembersJson.IndexOf("\"hostType\": \"Z.Host\"", StringComparison.Ordinal)));
            Assert.That(unexposedMembersJson.IndexOf("\"externSignature\": \"a\"", StringComparison.Ordinal),
                Is.LessThan(unexposedMembersJson.IndexOf("\"externSignature\": \"z\"", StringComparison.Ordinal)));

            var typesJson = GetTypesJson(first);
            Assert.That(typesJson.IndexOf("\"A.Super\"", StringComparison.Ordinal),
                Is.LessThan(typesJson.IndexOf("\"Z.Super\"", StringComparison.Ordinal)));
            Assert.That(catalog.formatVersion, Is.EqualTo(1));
        }

        private static UdonApiTypeRecord FindType(UdonApiCatalogData catalog, Type type)
        {
            var runtimeName = GetRuntimeName(type);
            return catalog.types.Find(candidate => candidate.runtimeName == runtimeName);
        }

        private static string GetRuntimeName(Type type)
        {
            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        private static string GetTypesJson(string json)
        {
            const string typesStart = "  \"types\": [";
            const string typesEnd = "  \"unexposedClrTypeNames\": [";
            var start = json.IndexOf(typesStart, StringComparison.Ordinal);
            var end = json.IndexOf(typesEnd, start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return json.Substring(start, end - start);
        }

        private static string GetUnexposedMembersJson(string json)
        {
            const string membersStart = "  \"unexposedMembers\": [";
            const string membersEnd = "  \"capabilities\": {";
            var start = json.IndexOf(membersStart, StringComparison.Ordinal);
            var end = json.IndexOf(membersEnd, start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return json.Substring(start, end - start);
        }

        private static UdonApiMemberRecord FindMember(UdonApiCatalogData catalog, string name)
        {
            var member = catalog.members.Find(candidate => candidate.name == name);
            Assert.That(member, Is.Not.Null, $"Member '{name}' was not discovered.");
            return member;
        }

        private static UdonApiUnexposedMemberRecord FindUnexposedMember(
            UdonApiCatalogData catalog,
            string name)
        {
            var member = catalog.unexposedMembers.Find(candidate => candidate.name == name);
            Assert.That(member, Is.Not.Null, $"Unexposed member '{name}' was not discovered.");
            return member;
        }

        private sealed class CatalogFixtureExposure : IUdonApiExposure
        {
            private readonly HashSet<string> _signatures = new(StringComparer.Ordinal);

            public IReadOnlyCollection<string> ExposedSignatures => _signatures;

            public bool IsTypeExposed(Type type) => type != typeof(UdonApiCatalogUnexposedFixture);

            public bool IsMemberExposed(string externSignature)
            {
                if (externSignature.IndexOf("__Hidden", StringComparison.Ordinal) >= 0)
                    return false;
                _signatures.Add(externSignature);
                return true;
            }
        }
    }

    public sealed class UdonApiCatalogUnexposedFixture
    {
        public void UnexposedTypeMember()
        {
        }
    }

    public interface IUdonApiCatalogSupertypeFixture
    {
    }

    public class UdonApiCatalogBaseFixture
    {
    }

    public sealed class UdonApiCatalogDerivedFixture
        : UdonApiCatalogBaseFixture,
          IUdonApiCatalogSupertypeFixture
    {
    }

    public sealed class UdonApiCatalogUnsupportedFixture
    {
        public void Params(params int[] values)
        {
        }
    }

    public sealed class UdonApiCatalogMetadataFixture
    {
        public T Constrained<T>(T value)
            where T : class, IDisposable, new()
        {
            return value;
        }
    }
}
