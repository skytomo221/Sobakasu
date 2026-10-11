using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal sealed class UdonApiCatalogLoaderTests
    {
        [Test]
        public void Load_V2SourceMembersProjectToCompilerExternModel()
        {
            var catalog = UdonApiCatalogLoader.Load(CatalogJson);
            Assert.That(catalog.TryGetTypeSymbol("Test.Host", out var host), Is.True);
            var method = catalog.GetExternalMethodGroup(host, "Echo").Methods[0] as ExternMethodSymbol;
            Assert.That(method, Is.Not.Null);
            Assert.That(method.IsStatic, Is.False);
            Assert.That(method.Parameters.Count, Is.EqualTo(2)); // receiver plus source parameter
            Assert.That(method.Parameters[1].Type, Is.EqualTo(TypeSymbol.I32));
            Assert.That(method.AbiParameters.Count, Is.EqualTo(1));
            Assert.That(method.AbiReturnType, Is.EqualTo(TypeSymbol.I32));
        }

        [Test]
        public void Load_RejectsOldFormatVersion()
        {
            Assert.Throws<InvalidDataException>(() => UdonApiCatalogLoader.Load(CatalogJson.Replace("\"formatVersion\": 2", "\"formatVersion\": 1")));
        }

        [TestCase("\"obsolete\": true")]
        [TestCase("\"namespaces\": []")]
        [TestCase("\"capabilities\": {}")]
        public void Load_RejectsUnknownOrNonCanonicalRootFields(string replacement)
        {
            var json = CatalogJson.Replace("\"formatVersion\": 2,", "\"formatVersion\": 2,\n  " + replacement + ",");
            Assert.Throws<InvalidDataException>(() => UdonApiCatalogLoader.Load(json));
        }

        [Test]
        public void Load_RejectsInvalidTypeRefUnion()
        {
            var json = CatalogJson.Replace("\"type\": \"System.Int32\"", "\"type\": { \"array\": \"System.Int32\", \"typeParameter\": 0 }");
            Assert.Throws<InvalidDataException>(() => UdonApiCatalogLoader.Load(json));
        }

        [TestCase("unknownProperty")]
        [TestCase("nullNamespaces")]
        [TestCase("emptyNamespaces")]
        [TestCase("emptyTarget")]
        [TestCase("emptyTypeSet")]
        [TestCase("explicitFalse")]
        [TestCase("emptyMemberArray")]
        [TestCase("rankOne")]
        [TestCase("unknownType")]
        [TestCase("badGenericOrdinal")]
        [TestCase("duplicateNamespace")]
        [TestCase("duplicateType")]
        [TestCase("typeInBothExposureSets")]
        [TestCase("duplicateMember")]
        [TestCase("memberInBothExposureSets")]
        [TestCase("missingSignature")]
        [TestCase("signatureOnUnexposedMember")]
        [TestCase("invalidPassing")]
        [TestCase("paramsOnScalar")]
        [TestCase("paramsAndPassing")]
        public void Load_RejectsMalformedV2Records(string mutation)
        {
            var root = JObject.Parse(CatalogJson);
            var testNamespace = (JObject)root["namespaces"][1];
            var exposedClass = (JObject)testNamespace["exposed"]["classes"][0];
            var method = (JObject)exposedClass["exposed"]["instance"]["methods"][0];
            switch (mutation)
            {
                case "unknownProperty": root["obsolete"] = true; break;
                case "nullNamespaces": root["namespaces"] = JValue.CreateNull(); break;
                case "emptyNamespaces": root["namespaces"] = new JArray(); break;
                case "emptyTarget": root["target"] = new JObject(); break;
                case "emptyTypeSet": testNamespace["exposed"] = new JObject(); break;
                case "explicitFalse": exposedClass["abstract"] = false; break;
                case "emptyMemberArray": exposedClass["exposed"]["instance"]["methods"] = new JArray(); break;
                case "rankOne": method["parameters"][0]["type"] = new JObject { ["array"] = "System.Int32", ["rank"] = 1 }; break;
                case "unknownType": method["returnType"] = "Missing.Type"; break;
                case "badGenericOrdinal": method["returnType"] = new JObject { ["methodParameter"] = 0 }; break;
                case "duplicateNamespace": ((JArray)root["namespaces"]).Add(((JObject)root["namespaces"][0]).DeepClone()); break;
                case "duplicateType": ((JArray)testNamespace["exposed"]["classes"]).Add(exposedClass.DeepClone()); break;
                case "typeInBothExposureSets": testNamespace["unexposed"] = new JObject { ["classes"] = new JArray(exposedClass.DeepClone()) }; break;
                case "duplicateMember": ((JArray)exposedClass["exposed"]["instance"]["methods"]).Add(method.DeepClone()); break;
                case "memberInBothExposureSets": exposedClass["unexposed"] = new JObject { ["instance"] = new JObject { ["methods"] = new JArray(method.DeepClone()) } }; break;
                case "missingSignature": method.Remove("udonSignature"); break;
                case "signatureOnUnexposedMember":
                    exposedClass["unexposed"] = new JObject { ["instance"] = new JObject { ["methods"] = new JArray(method.DeepClone()) } };
                    exposedClass["exposed"]["instance"]["methods"] = new JArray();
                    break;
                case "invalidPassing": method["parameters"][0]["passing"] = "value"; break;
                case "paramsOnScalar": method["parameters"][0]["params"] = true; break;
                case "paramsAndPassing": method["parameters"][0]["params"] = true; method["parameters"][0]["passing"] = "ref"; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(mutation));
            }
            Assert.Throws<InvalidDataException>(() => UdonApiCatalogLoader.Load(root.ToString()));
        }

        private const string CatalogJson = "{\n" +
            "  \"formatVersion\": 2,\n" +
            "  \"target\": { \"unityVersion\": \"2022.3\", \"vrchatSdkVersion\": \"3.10\" },\n" +
            "  \"namespaces\": [\n" +
            "    { \"name\": \"System\", \"unexposed\": { \"classes\": [ { \"path\": [\"Type\"] } ], \"structs\": [ { \"path\": [\"Int32\"] } ] } },\n" +
            "    { \"name\": \"Test\", \"exposed\": { \"classes\": [\n" +
            "      { \"path\": [\"Host\"], \"exposed\": { \"instance\": { \"methods\": [ { \"name\": \"Echo\", \"parameters\": [ { \"name\": \"value\", \"type\": \"System.Int32\" } ], \"returnType\": \"System.Int32\", \"udonSignature\": \"TestHost.__Echo__SystemInt32__SystemInt32\" } ] } } }\n" +
            "    ] } }\n" +
            "  ]\n" +
            "}";
    }
}
