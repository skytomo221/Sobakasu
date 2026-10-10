using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;

namespace Skytomo221.Sobakasu.Tests.Standalone
{
    public sealed class UdonBindingSourceModelTests
    {
        [Test]
        public void VersionOneCatalogRequiresManualRegeneration()
        {
            var catalog = new UdonApiCatalogData { formatVersion = 1 };
            var error = Assert.Throws<InvalidDataException>(() => UdonBindingSourceModel.FromCatalog(catalog));
            Assert.That(error.Message, Does.Contain("Regenerate it manually"));
        }

        [Test]
        public void CatalogTypeIndexExcludesMetadataClosureTypes()
        {
            var catalog = new UdonApiCatalogData
            {
                formatVersion = 2,
                types = new List<UdonApiTypeRecord>
                {
                    new() { runtimeName = "Example.Widget", clrNamespace = "Example", shape = "Reference", isStaticApiContainer = false },
                    new() { runtimeName = "Example.Base", clrNamespace = "Example", shape = "Reference", isStaticApiContainer = false }
                },
                unexposedClrTypeNames = new List<string> { "Example.Base" }
            };
            var model = UdonBindingSourceModel.FromCatalog(catalog);
            Assert.That(model.Types, Has.Count.EqualTo(1));
            Assert.That(model.Types[0].TypePath, Is.EqualTo("Widget"));
            Assert.That(model.TypesByRuntimeName, Contains.Key("Example.Base"));
        }

        [Test]
        public void FormatterUsesPersistedNamespaceForNestedRuntimeNames()
        {
            var catalog = new UdonApiCatalogData
            {
                formatVersion = 2,
                types = new List<UdonApiTypeRecord>
                {
                    new() { runtimeName = "Example.Outer+Inner", clrNamespace = "Example", shape = "Reference", isStaticApiContainer = false }
                }
            };
            var model = UdonBindingSourceModel.FromCatalog(catalog);
            var formatter = new UdonBindingTypeFormatter(model);
            Assert.That(formatter.TryFormat(
                new ExternTypeRef { kind = "Named", runtimeName = "Example.Outer+Inner" },
                "Example.Host", null, out var formatted, out var reason), Is.True, reason);
            Assert.That(formatted, Is.EqualTo("Example::Outer::Inner"));
        }

        [Test]
        public void MissingCatalogFailsWithoutDiscoveryFallback()
        {
            var error = Assert.Throws<FileNotFoundException>(() => UdonBindingSourceModel.Load(Path.Combine(Path.GetTempPath(), "sobakasu-missing-udon-api-catalog.json")));
            Assert.That(error.Message, Does.Contain("Window/Sobakasu/Build Udon API Catalog"));
        }
    }
}
