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

using static Skytomo221.Sobakasu.Tests.Editor.UdonBindingGeneratorTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class UdonBindingGeneratorNestedTypeTests
    {

        [Test]
        public void Generator_HoistsNestedStructAndEnumAndPreservesRuntimeIdentity()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiNestedOuterFixture.NestedValue),
                typeof(UdonApiNestedOuterFixture.NestedEnum)
            });
            var valueSource = GetTypeSource(result, typeof(UdonApiNestedOuterFixture.NestedValue));
            var enumSource = GetTypeSource(result, typeof(UdonApiNestedOuterFixture.NestedEnum));

            Assert.That(valueSource, Does.StartWith("pub struct NestedValue = extern ")
                .And.Contain("UdonApiNestedOuterFixture.NestedValue"));
            Assert.That(enumSource, Does.StartWith("pub enum NestedEnum = extern ")
                .And.Contain("UdonApiNestedOuterFixture.NestedEnum"));
            Assert.That(valueSource, Does.Not.Contain("struct UdonApiNestedOuterFixture"));
            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void InstalledGenerator_NestedRuntimeTypeBindsAgainstInstalledCatalog()
        {
            var result = UdonBindingGenerator.CreateDefault()
                .Generate(new[] { typeof(UnityEngine.ParticleSystem.Burst) });
            var source = GetTypeSource(result, typeof(UnityEngine.ParticleSystem.Burst));
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser));

            var binder = new SobakasuBinder();
            binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Generator_ReportsNestedHoistCollisionAndAcceptsExplicitRename()
        {
            var types = new[]
            {
                typeof(UdonApiNestedCollisionA.Value),
                typeof(UdonApiNestedCollisionB.Value)
            };
            Assert.That(() => CreateGenerator().Generate(types),
                Throws.TypeOf<UdonBindingConfigurationException>()
                    .With.Message.Contains("require generated module path"));

            var config = UdonBindingGenerationConfig.CreateDefault();
            config.renames.types = new[]
            {
                new UdonBindingTypeRenameRule
                {
                    from = ClrMemberId.GetClrTypeName(typeof(UdonApiNestedCollisionB.Value)),
                    to = "OtherValue"
                }
            };
            var result = CreateGenerator(config).Generate(types);
            Assert.That(result.Files.Keys, Has.Some.Contains("other_value"));
        }
    }
}
