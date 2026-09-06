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

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class UdonBindingGeneratorTestSupport
    {
        internal static UdonBindingGenerator CreateGenerator(
            UdonBindingGenerationConfig configuration = null,
            IUdonApiExposure exposure = null)
        {
            var formatter = new UdonBindingTypeFormatter();
            exposure ??= new FixtureExposure();
            return new UdonBindingGenerator(
                new UdonApiDiscovery(exposure, formatter),
                new SobakasuBindingRenderer(formatter),
                configuration);
        }
        internal static UdonBindingGenerator CreateInstalledGenerator(
            UdonBindingGenerationConfig configuration)
        {
            var formatter = new UdonBindingTypeFormatter(
                SobakasuBuiltInEnvironment.Default.ExternCatalog);
            return new UdonBindingGenerator(
                new UdonApiDiscovery(
                    new InstalledUdonApiExposure(UdonExposedNodeCache.Default),
                    formatter),
                new SobakasuBindingRenderer(formatter),
                configuration);
        }
        internal static UdonBindingGenerationConfig CreateTypeNamespaceCollisionConfig(
            string parentNamespace,
            string childNamespace)
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule
                {
                    from = typeof(UdonApiStaticFixture).Namespace,
                    to = parentNamespace
                },
                new UdonBindingNamespaceRenameRule
                {
                    from = typeof(PolicyFixtures.Deep.DeepNamespaceFixture).Namespace,
                    to = childNamespace
                }
            };
            config.renames.types = new[]
            {
                new UdonBindingTypeRenameRule
                {
                    from = typeof(UdonApiStaticFixture).FullName,
                    to = "Deep"
                }
            };
            return config;
        }
        internal static string MemberRule(
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
            var clrParameterTypes = new Type[parameterTypes.Count];
            for (var index = 0; index < parameterTypes.Count; index++)
                clrParameterTypes[index] = parameterTypes[index];
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static;
            System.Reflection.MethodBase callable =
                string.Equals(memberKind, "constructor", StringComparison.Ordinal)
                    ? declaringType.GetConstructor(
                        flags,
                        null,
                        clrParameterTypes,
                        null)
                    : FindCallableInHierarchy(
                        declaringType,
                        member,
                        flags,
                        clrParameterTypes);
            Assert.That(callable, Is.Not.Null,
                $"No reflection callable was found for {declaringType.FullName}.{member}.");
            return ClrMemberId.Format(callable);
        }
        internal static System.Reflection.MethodInfo FindCallableInHierarchy(
            Type declaringType,
            string member,
            System.Reflection.BindingFlags flags,
            Type[] parameterTypes)
        {
            for (var current = declaringType;
                 current != null;
                 current = current.BaseType)
            {
                var callable = current.GetMethod(
                    member,
                    flags | System.Reflection.BindingFlags.DeclaredOnly,
                    null,
                    parameterTypes,
                    null);
                if (callable != null)
                    return callable;
            }
            return null;
        }
        internal static void AssertFormats(
            UdonBindingTypeFormatter formatter,
            Type type,
            string expected)
        {
            Assert.That(formatter.TryFormat(
                type,
                typeof(UdonBindingGeneratorFixture),
                out var actual,
                out var reason), Is.True, reason);
            Assert.That(actual, Is.EqualTo(expected));
        }
        internal static void AssertParses(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser));
        }
        internal static void AssertAllBindingSourcesParse(
            UdonBindingGenerationResult result)
        {
            foreach (var pair in result.Files)
            {
                if (!pair.Key.EndsWith(".sobakasu", StringComparison.Ordinal))
                    continue;
                AssertParses(pair.Value);
            }
        }
        internal static void WithGeneratedLibrary(
            UdonBindingGenerationResult result,
            Action<string> action)
        {
            var root = NewTemporaryPath();
            try
            {
                WriteTextFiles(root, result.Files);
                action(root);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
        internal static string GetFixtureSource(UdonBindingGenerationResult result)
        {
            return GetTypeSource(result, typeof(UdonBindingGeneratorFixture));
        }
        internal static string GetTypeSource(
            UdonBindingGenerationResult result,
            Type type)
        {
            var record = FindGeneratedType(result.Report, type);
            var skipReason = string.Empty;
            foreach (var skippedType in result.Report.skipped_types)
            {
                if (string.Equals(
                    skippedType.clr_declaring_type,
                    type.FullName,
                    StringComparison.Ordinal))
                {
                    skipReason = skippedType.reason;
                    break;
                }
            }
            Assert.That(record.generated_file, Is.Not.Empty,
                $"The generated file for '{type.FullName}' is empty. " +
                $"Skip reason: {skipReason}");
            return GetSource(result, record.generated_file);
        }
        internal static string GetSource(
            UdonBindingGenerationResult result,
            string fileName)
        {
            if (result.Files.TryGetValue(fileName, out var source))
                return source;

            Assert.Fail($"The fixture binding '{fileName}' was not generated.");
            return null;
        }
        internal static UdonApiSkipRecord FindSkip(
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
        internal static UdonApiGeneratedTypeRecord FindGeneratedType(
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
        internal static void WriteTextFiles(
            string root,
            IReadOnlyDictionary<string, string> files)
        {
            var encoding = new UTF8Encoding(false);
            foreach (var pair in files)
            {
                var filePath = Path.Combine(
                    root,
                    pair.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, pair.Value, encoding);
            }
        }
        internal static UdonApiPhysicalRecord FindPhysical(
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
        internal static int CountOccurrences(string text, string value)
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
        internal static string FormatDiagnostics(SobakasuParser parser)
        {
            return FormatDiagnostics(parser.Diagnostics.Diagnostics);
        }
        internal static string FormatDiagnostics(
            IReadOnlyList<Diagnostic> diagnostics)
        {
            var messages = new List<string>();
            foreach (var diagnostic in diagnostics)
                messages.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", messages);
        }
        internal static string NewTemporaryPath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                $"SobakasuUdonBindingGeneratorTests_{Guid.NewGuid():N}");
        }
        internal static Type FindLoadedType(string qualifiedName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(qualifiedName, false);
                if (type != null)
                    return type;
            }
            return null;
        }
        internal sealed class FixtureExposure : IUdonApiExposure
        {
            private readonly HashSet<string> _exposedSignatures =
                new(StringComparer.Ordinal);
            private readonly string[] _fixturePrefixes =
            {
                GetPrefix(typeof(UdonBindingGeneratorFixture)),
                GetPrefix(typeof(UdonApiInheritedParentFixture)),
                GetPrefix(typeof(UdonApiInheritedChildAFixture)),
                GetPrefix(typeof(UdonApiInheritedChildBFixture)),
                GetPrefix(typeof(UdonApiGenericCoverageFixture)),
                GetPrefix(typeof(UdonApiNormalConstructorFixture)),
                GetPrefix(typeof(UdonApiRefConstructorFixture)),
                GetPrefix(typeof(UdonApiOutConstructorFixture)),
                GetPrefix(typeof(UdonApiMixedConstructorFixture)),
                GetPrefix(typeof(UdonApiStructFixture)),
                GetPrefix(typeof(UdonApiOperatorFixture)),
                GetPrefix(typeof(UdonApiEnumFixture)),
                GetPrefix(typeof(UdonApiNestedOuterFixture.NestedValue)),
                GetPrefix(typeof(UdonApiNestedOuterFixture.NestedEnum)),
                GetPrefix(typeof(UdonApiNestedCollisionA.Value)),
                GetPrefix(typeof(UdonApiNestedCollisionB.Value)),
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
                if (_exposedSignatures.Contains(externSignature))
                    return true;
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

            internal static string GetPrefix(Type type)
            {
                return UdonExternSignatureFormatter.GetUdonTypeName(type) + ".";
            }
        }
        internal sealed class NoMemberExposure : IUdonApiExposure
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
        internal static string ConfigurationJson(string namespaceRules)
        {
            return
                "{\"version\":\"3\"," +
                "\"renames\":{\"namespaces\":" + namespaceRules +
                ",\"types\":[],\"members\":[]}," +
                "\"lang\":[]," +
                "\"prelude\":{\"namespaces\":[],\"types\":[],\"members\":[]}," +
                "\"maybe\":{\"returns\":[],\"outs\":[]}," +
                "\"excludes\":{\"namespaces\":[],\"types\":[],\"members\":[]}}";
        }
        internal static UdonBindingGenerationConfig LoadConfig(string json)
        {
            var path = NewTemporaryPath() + ".json";
            try
            {
                File.WriteAllText(path, json);
                return UdonBindingGenerationConfig.Load(path);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
