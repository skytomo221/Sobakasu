using System.IO;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;
using static Skytomo221.Sobakasu.Tests.Editor.UdonBindingGeneratorTestSupport;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal sealed class UdonBindingGeneratorCatalogTests
    {
        [Test]
        public void CatalogFixtureProvidesExplicitPhysicalMetadataAndIndexesAbiTypes()
        {
            var model = UdonBindingSourceModel.FromCatalog(CreateCatalogFixture());

            Assert.That(model.TypesByRuntimeName.ContainsKey("Example.Outer+Inner"), Is.True);
            Assert.That(model.TypesByRuntimeName["Example.Outer+Inner"].clrNamespace, Is.EqualTo("Example"));
            Assert.That(model.Types, Has.None.Matches<UdonBindingSourceType>(type => type.RuntimeName == "Example.BaseMetadata"));
            Assert.That(model.UnexposedTypeNames, Does.Contain("Example.BaseMetadata"));
            Assert.That(model.MembersByHostRuntimeName["Example.Widget"], Has.Some.Matches<UdonBindingSourceMember>(member => member.SourceKind == "FieldGetter"));
            Assert.That(model.MembersByHostRuntimeName["Example.Widget"], Has.Some.Matches<UdonBindingSourceMember>(member => member.SourceKind == "PropertyGetter"));
        }

        [Test]
        public void CatalogGenerationPreservesEnumStructOperatorAndRefOutSurfaces()
        {
            var configuration = UdonBindingGenerationConfig.CreateDefault();
            configuration.renames.namespaces = new[]
            {
                new UdonBindingNamespaceRenameRule { from = "Example", to = "example" }
            };
            var result = CreateGenerator(configuration: configuration).Generate();

            Assert.That(result.Files.ContainsKey("example/widget_binding.library.sobakasu"), Is.True);
            Assert.That(result.Files["example/widget_binding.library.sobakasu"], Does.Contain("public type Widget = extern Example.Widget"));
            Assert.That(result.Files["example/widget_binding.library.sobakasu"], Does.Contain("out string label"));
            Assert.That(result.Files["example/point_binding.library.sobakasu"], Does.Contain("public struct Point = extern Example.Point"));
            Assert.That(result.Files["example/mode_binding.library.sobakasu"], Does.Contain("Running = extern Running"));
            Assert.That(result.Files["example/math_api.library.sobakasu"], Does.Contain("public function clamp(value: f32)"));
        }

        [Test]
        public void VersionOneCatalogIsRejectedWithManualRegenerationGuidance()
        {
            var catalog = new UdonApiCatalogData { formatVersion = 1 };
            var error = Assert.Throws<InvalidDataException>(() => UdonBindingSourceModel.FromCatalog(catalog));
            Assert.That(error.Message, Does.Contain("Regenerate it manually"));
            Assert.That(error.Message, Does.Contain("Window/Sobakasu/Build Udon API Catalog"));
        }
    }
}
