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
    public class UdonBindingGeneratorReportTests
    {

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

        [Test]
        public void InstalledGenerator_SkipsDeclarationsTheCompilerCannotBind()
        {
            var result = UdonBindingGenerator.CreateDefault()
                .Generate(new[] { typeof(UnityEngine.AnimatorStateInfo) });
            var source = GetTypeSource(
                result,
                typeof(UnityEngine.AnimatorStateInfo));

            Assert.That(source, Does.Not.Contain("extern self.loop"));
            Assert.That(FindSkip(result.Report, "loop").reason,
                Does.Contain("member-access syntax"));
        }

        [Test]
        public void Generator_ReportsHiddenAndUnsupportedMembers()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture)
            });
            var source = GetFixtureSource(result);

            Assert.That(source, Does.Not.Contain("fn hidden"));
            Assert.That(source,
                Does.Contain("pub fn generic<T>(value: T) -> T"));
            Assert.That(source,
                Does.Contain("= extern self.Generic<T>(value)"));
            Assert.That(source,
                Does.Contain("pub fn generic_array<T>() -> [T]"));
            Assert.That(source,
                Does.Contain("values: System.Collections.Generic.List<T>"));
            Assert.That(FindSkip(result.Report, "Hidden").reason,
                Does.Contain("not exposed to Udon"));
            Assert.That(FindSkip(result.Report, "Item").reason,
                Does.Contain("Indexed properties"));
            Assert.That(FindSkip(result.Report, "Changed").reason,
                Does.Contain("CLR events"));
        }

        [Test]
        public void Generator_ReportsUnmatchedExposedNodesOutsideCoverageDenominator()
        {
            const string unmatched = "Unmatched.__Node__SystemVoid";
            var exposure = new FixtureExposure(new[] { unmatched });
            var result = CreateGenerator(exposure: exposure).Generate(new[]
            {
                typeof(UdonApiGenericCoverageFixture)
            });

            Assert.That(result.Report.udon_exposed_unmatched_signatures,
                Is.EqualTo(new[] { unmatched }));
            Assert.That(result.Report.udon_exposed_unmatched_signatures_count,
                Is.EqualTo(1));
            Assert.That(result.Report.udon_signatures_exposed, Is.EqualTo(2));
            Assert.That(result.Report.udon_api.Exists(record =>
                record.extern_signature == unmatched), Is.False);
        }

        [Test]
        public void Generator_ProducesDeterministicFilesAndOrdering()
        {
            var generator = CreateGenerator();
            var first = generator.Generate(new[]
            {
                typeof(UdonApiStaticFixture),
                typeof(UdonApiStructFixture),
                typeof(UdonBindingGeneratorFixture),
                typeof(int)
            });
            var second = generator.Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture),
                typeof(UdonApiStructFixture),
                typeof(UdonApiStaticFixture),
                typeof(int)
            });

            Assert.That(second.Files.Keys, Is.EqualTo(first.Files.Keys));
            foreach (var pair in first.Files)
            {
                Assert.That(second.Files[pair.Key], Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void Generator_SeparatesDiagnosticsFromBindingSources()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture)
            });

            Assert.That(result.Files.Keys,
                Does.Contain("external/udon_binding_generator_fixture.sobakasu"));
            Assert.That(result.Files.Keys,
                Does.Not.Contain(UdonBindingGenerator.ReportFileName));
            Assert.That(result.Files.Keys,
                Does.Not.Contain(UdonBindingGenerator.SkippedMembersFileName));
            Assert.That(result.Diagnostics.Keys,
                Does.Contain(UdonBindingGenerator.ReportFileName));
            Assert.That(result.Diagnostics.Keys,
                Does.Contain(UdonBindingGenerator.SkippedMembersFileName));
        }
    }
}
