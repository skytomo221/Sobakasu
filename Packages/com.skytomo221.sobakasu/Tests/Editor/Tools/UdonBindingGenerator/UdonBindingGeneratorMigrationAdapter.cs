using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;
using Skytomo221.Sobakasu.Tools.UdonApi;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    /// <summary>
    /// Migration-only compatibility adapter for the existing reflection fixtures.
    /// The fixture discovery stays reflection based, but all policy/rendering work is
    /// delegated to the production Catalog-source-model generator.
    /// </summary>
    internal sealed class UdonApiDiscovery
    {
        private readonly Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.UdonApiDiscovery _inner;

        public UdonApiDiscovery(
            IUdonApiExposure exposure,
            UdonBindingTypeFormatter formatter)
        {
            _inner = new Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.UdonApiDiscovery(
                exposure ?? throw new ArgumentNullException(nameof(exposure)));
            _ = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        public UdonApiDiscovery(IUdonApiExposure exposure)
        {
            _inner = new Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.UdonApiDiscovery(
                exposure ?? throw new ArgumentNullException(nameof(exposure)));
        }

        public UdonApiModel Discover() => _inner.Discover();

        public UdonApiModel Discover(IReadOnlyList<Type> candidateTypes) =>
            _inner.Discover(candidateTypes);
    }

    internal sealed class UdonBindingGenerator
    {
        public const string ReportFileName =
            Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.UdonBindingGenerator.ReportFileName;
        public const string SkippedMembersFileName =
            Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.UdonBindingGenerator.SkippedMembersFileName;

        private readonly UdonApiDiscovery _discovery;
        private readonly UdonBindingGenerationConfig _configuration;
        private readonly bool _validateGeneratedBindings;

        public UdonBindingGenerator(
            UdonApiDiscovery discovery,
            SobakasuBindingRenderer renderer,
            UdonBindingGenerationConfig configuration = null,
            bool validateGeneratedBindings = false)
        {
            _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
            _configuration = configuration ?? UdonBindingGenerationConfig.CreateDefault();
            _validateGeneratedBindings = validateGeneratedBindings;
            _ = renderer ?? throw new ArgumentNullException(nameof(renderer));
        }

        public static UdonBindingGenerator CreateDefault()
        {
            var formatter = new UdonBindingTypeFormatter(
                SobakasuTestEnvironment.Default.IsExternTypeAvailable);
            return new UdonBindingGenerator(
                new UdonApiDiscovery(
                    new InstalledUdonApiExposure(UdonExposedNodeCache.Default),
                    formatter),
                new SobakasuBindingRenderer(formatter),
                UdonBindingGenerationConfig.CreateDefault());
        }

        public UdonBindingGenerationResult Generate()
        {
            return GenerateModel(_discovery.Discover());
        }

        public UdonBindingGenerationResult Generate(IReadOnlyList<Type> candidateTypes)
        {
            return GenerateModel(_discovery.Discover(candidateTypes));
        }

        private UdonBindingGenerationResult GenerateModel(UdonApiModel reflected)
        {
            var formatter = new UdonBindingTypeFormatter();
            foreach (var type in reflected.Types)
                if (!formatter.CanDeclareType(type.ClrType, out var reason))
                    type.SkipReason = reason;
            var model = UdonBindingSourceModel.FromReflectionModel(reflected);
            if (_validateGeneratedBindings)
            {
                // Installed-API tests use the same physical catalog surfaces as
                // production, including static containers and inherited members.
                var selected = new HashSet<string>(reflected.Types.Select(type => type.ClrType.FullName));
                using var reader = File.OpenText(StandardLibraryGenerator.DefaultCatalogPath);
                var catalog = UdonApiCatalogReader.Read(reader);
                foreach (var ns in catalog.namespaces)
                {
                    if (ns.exposed == null) continue;
                    ns.unexposed ??= new UdonApiTypeSet();
                    SelectTypes(ns.name, ns.exposed.classes, ref ns.unexposed.classes, selected);
                    SelectTypes(ns.name, ns.exposed.interfaces, ref ns.unexposed.interfaces, selected);
                    SelectTypes(ns.name, ns.exposed.structs, ref ns.unexposed.structs, selected);
                    SelectTypes(ns.name, ns.exposed.enums, ref ns.unexposed.enums, selected);
                }
                model = UdonBindingSourceModel.FromCatalog(catalog);
            }
            // Reflection fixtures often pass only the surface type under test. Avoid
            // treating unrelated referenced CLR types as missing Catalog entries;
            // production CLI generation uses the complete Catalog-backed formatter.
            var renderer = new SobakasuBindingRenderer(new UdonBindingTypeFormatter());
            var generator = new Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.UdonBindingGenerator(
                model,
                renderer,
                SobakasuTestEnvironment.Default,
                _configuration,
                validateGeneratedBindings: _validateGeneratedBindings);
            return generator.Generate();
        }

        private static void SelectTypes<T>(string ns, List<T> exposed, ref List<T> unexposed,
            ISet<string> selected) where T : UdonApiTypeRecord
        {
            if (exposed == null) return;
            unexposed ??= new List<T>();
            foreach (var record in exposed.ToArray())
            {
                var runtimeName = (string.IsNullOrEmpty(ns) ? "" : ns + ".") + string.Join("+", record.path);
                if (selected.Contains(runtimeName)) continue;
                exposed.Remove(record);
                unexposed.Add(record);
            }
        }
    }
}
