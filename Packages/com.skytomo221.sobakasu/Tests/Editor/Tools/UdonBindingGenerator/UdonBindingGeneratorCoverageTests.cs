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
    public class UdonBindingGeneratorCoverageTests
    {

        [Test]
        public void Generator_MaintainsCompletenessInvariants()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonBindingGeneratorFixture),
                typeof(int)
            });
            var report = result.Report;

            Assert.That(report.types_discovered,
                Is.EqualTo(report.types_generated + report.types_skipped));
            Assert.That(report.members_discovered,
                Is.EqualTo(report.members_generated + report.members_skipped));
            Assert.That(report.types_discovered, Is.EqualTo(2));
            Assert.That(report.types_generated, Is.EqualTo(2));
            Assert.That(report.types_skipped, Is.Zero);
        }

        [Test]
        public void Generator_PreservesInheritedSurfacesAndDeduplicatesPhysicalApi()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiInheritedChildBFixture),
                typeof(UdonApiInheritedChildAFixture)
            });
            var childA = GetTypeSource(
                result,
                typeof(UdonApiInheritedChildAFixture));
            var childB = GetTypeSource(
                result,
                typeof(UdonApiInheritedChildBFixture));
            var foo = typeof(UdonApiInheritedParentFixture).GetMethod("Foo");
            var signature = UdonExternSignatureFormatter.GetUdonMethodName(foo);
            var physical = FindPhysical(result.Report, signature);

            Assert.That(childA, Does.Contain("pub fn foo()"));
            Assert.That(childB, Does.Contain("pub fn foo()"));
            Assert.That(physical.clr_declaring_type,
                Is.EqualTo(typeof(UdonApiInheritedParentFixture).FullName));
            Assert.That(physical.surface_types, Is.EqualTo(new[]
            {
                typeof(UdonApiInheritedChildAFixture).FullName,
                typeof(UdonApiInheritedChildBFixture).FullName
            }));
            Assert.That(physical.generated_surface_types,
                Is.EqualTo(physical.surface_types));
            Assert.That(result.Report.udon_api.FindAll(record =>
                record.extern_signature == signature), Has.Count.EqualTo(1));
            Assert.That(result.Report.udon_signatures_exposed,
                Is.EqualTo(result.Report.udon_api.FindAll(record =>
                    record.is_udon_exposed).Count));

            var eventFailures = result.Report.skipped_members.FindAll(record =>
                string.IsNullOrEmpty(record.extern_signature) &&
                record.full_name.EndsWith(".Changed", StringComparison.Ordinal));
            Assert.That(eventFailures, Has.Count.EqualTo(2));
            Assert.That(result.Report.udon_api.Exists(record =>
                string.IsNullOrEmpty(record.extern_signature)), Is.False);

            var physicalSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in result.Report.udon_api)
            {
                Assert.That(record.extern_signature, Is.Not.Empty);
                Assert.That(physicalSignatures.Add(record.extern_signature), Is.True,
                    record.extern_signature);
            }
            var skippedPhysicalSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in result.Report.skipped_members)
            {
                if (string.IsNullOrEmpty(record.extern_signature))
                    continue;
                Assert.That(skippedPhysicalSignatures.Add(record.extern_signature),
                    Is.True,
                    record.extern_signature);
            }
        }

        [Test]
        public void Generator_CoversPhysicalApiWhenAnyInheritedSurfaceGenerates()
        {
            var config = UdonBindingGenerationConfig.CreateDefault();
            config.excludes.types = new[]
            {
                typeof(UdonApiInheritedChildBFixture).FullName
            };
            var result = CreateGenerator(config).Generate(new[]
            {
                typeof(UdonApiInheritedChildAFixture),
                typeof(UdonApiInheritedChildBFixture)
            });
            var signature = UdonExternSignatureFormatter.GetUdonMethodName(
                typeof(UdonApiInheritedParentFixture).GetMethod("Foo"));
            var physical = FindPhysical(result.Report, signature);
            var skipped = result.Report.skipped_members.Find(record =>
                record.extern_signature == signature);

            Assert.That(physical.is_covered, Is.True);
            Assert.That(physical.generated_surface_types, Is.EqualTo(new[]
            {
                typeof(UdonApiInheritedChildAFixture).FullName
            }));
            Assert.That(skipped, Is.Not.Null);
            Assert.That(skipped.surface_failures.Exists(failure =>
                failure.surface_type ==
                    typeof(UdonApiInheritedChildBFixture).FullName), Is.True);
        }

        [Test]
        public void Generator_SeparatesUdonExposureFromGenericSupportAndCalculatesCoverage()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiGenericCoverageFixture)
            });
            var exposedSignature = UdonExternSignatureFormatter.GetUdonMethodName(
                typeof(UdonApiGenericCoverageFixture).GetMethod("ExposedGeneric"));
            var unexposedSignature = UdonExternSignatureFormatter.GetUdonMethodName(
                typeof(UdonApiGenericCoverageFixture).GetMethod("UnexposedGeneric"));
            var exposed = FindPhysical(result.Report, exposedSignature);
            var unexposed = FindPhysical(result.Report, unexposedSignature);

            Assert.That(exposed.is_udon_exposed, Is.True);
            Assert.That(exposed.is_covered, Is.True);
            Assert.That(unexposed.is_udon_exposed, Is.False);
            Assert.That(unexposed.is_covered, Is.False);
            Assert.That(result.Report.udon_signatures_exposed, Is.EqualTo(2));
            Assert.That(result.Report.udon_signatures_covered, Is.EqualTo(2));
            Assert.That(result.Report.udon_signatures_unsupported, Is.Zero);
            Assert.That(result.Report.udon_signatures_exposed, Is.EqualTo(
                result.Report.udon_signatures_covered +
                result.Report.udon_signatures_unsupported));
            Assert.That(result.Report.udon_api_coverage_percent, Is.EqualTo(100.0));
            Assert.That(result.Report.skipped_members.FindAll(record =>
                record.extern_signature == exposedSignature), Is.Empty);
            Assert.That(result.Diagnostics[UdonBindingGenerator.ReportFileName],
                Does.Contain("\"udon_signatures_exposed\": 2"));
            Assert.That(result.Diagnostics[UdonBindingGenerator.ReportFileName],
                Does.Contain("\"is_udon_exposed\": true"));
        }

        [Test]
        public void Generator_UsesZeroCoverageWhenNoPhysicalSignatureIsExposed()
        {
            var result = CreateGenerator(exposure: new NoMemberExposure()).Generate(
                new[] { typeof(UdonApiGenericCoverageFixture) });

            Assert.That(result.Report.udon_signatures_exposed, Is.Zero);
            Assert.That(result.Report.udon_signatures_covered, Is.Zero);
            Assert.That(result.Report.udon_signatures_unsupported, Is.Zero);
            Assert.That(result.Report.udon_api_coverage_percent, Is.EqualTo(0.0));
            Assert.That(double.IsNaN(result.Report.udon_api_coverage_percent),
                Is.False);
            Assert.That(double.IsInfinity(result.Report.udon_api_coverage_percent),
                Is.False);
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
    }
}
