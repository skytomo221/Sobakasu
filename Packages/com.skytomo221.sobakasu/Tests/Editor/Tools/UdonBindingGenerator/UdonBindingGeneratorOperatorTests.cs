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
    public class UdonBindingGeneratorOperatorTests
    {

        [Test]
        public void Generator_ProjectsOperatorsToFirstOperandAndPreservesPhysicalIdentity()
        {
            var result = CreateGenerator().Generate(new[]
            {
                typeof(UdonApiOperatorFixture),
                typeof(float)
            });
            var hostSource = GetTypeSource(result, typeof(float));
            var declaringSource = GetTypeSource(
                result,
                typeof(UdonApiOperatorFixture));
            var multiply = typeof(UdonApiOperatorFixture).GetMethod(
                "op_Multiply",
                BindingFlags.Public | BindingFlags.Static);
            var externSignature =
                UdonExternSignatureFormatter.GetUdonMethodName(multiply);
            var physical = FindPhysical(result.Report, externSignature);

            Assert.That(hostSource, Does.Contain("pub fn *(rhs: ")
                .And.Contain("= extern self * rhs"));
            Assert.That(declaringSource, Does.Contain("pub fn @-")
                .And.Contain("= extern -self")
                .And.Contain("pub fn @~")
                .And.Contain("= extern ~self"));
            Assert.That(declaringSource, Does.Not.Contain("fn op_"));
            Assert.That(physical.clr_declaring_type,
                Is.EqualTo(typeof(UdonApiOperatorFixture).FullName));
            Assert.That(physical.generated_surface_types,
                Does.Contain(typeof(float).FullName));

            foreach (var unsupportedName in new[]
                     {
                         "op_Implicit",
                         "op_Explicit",
                         "op_Increment",
                         "op_Decrement"
                     })
            {
                Assert.That(result.Report.skipped_members.Exists(record =>
                    record.full_name.EndsWith(
                        "." + unsupportedName,
                        StringComparison.Ordinal)), Is.True);
            }
            Assert.That(result.Report.skipped_members.Exists(record =>
                record.reason.IndexOf(
                    "Conversion operators are outside",
                    StringComparison.Ordinal) >= 0), Is.True);
            Assert.That(result.Report.skipped_members.Exists(record =>
                record.reason.IndexOf(
                    "Increment and decrement operators are outside",
                    StringComparison.Ordinal) >= 0), Is.True);

            AssertAllBindingSourcesParse(result);
        }

        [Test]
        public void Generator_DiscoversUdonOnlyPrimitiveOperators()
        {
            var signatures = new[]
            {
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(int),
                    "op_Addition",
                    new[] { typeof(int), typeof(int) },
                    typeof(int)),
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(int),
                    "op_UnaryNegation",
                    new[] { typeof(int) },
                    typeof(int)),
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(int),
                    "op_OnesComplement",
                    new[] { typeof(int) },
                    typeof(int)),
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(float),
                    "op_Addition",
                    new[] { typeof(float), typeof(float) },
                    typeof(float)),
                ExternCatalog.BuildOperatorExternSignature(
                    typeof(float),
                    "op_UnaryNegation",
                    new[] { typeof(float) },
                    typeof(float))
            };
            var result = CreateGenerator(
                exposure: new FixtureExposure(signatures)).Generate(new[]
            {
                typeof(int),
                typeof(float)
            });
            var integerSource = GetTypeSource(result, typeof(int));
            var floatSource = GetTypeSource(result, typeof(float));

            Assert.That(integerSource, Does.Contain("pub impl i32 = extern System.Int32")
                .And.Contain("pub fn +(rhs: Self) -> Self")
                .And.Contain("= extern self + rhs")
                .And.Contain("pub fn @- -> Self")
                .And.Contain("pub fn @~ -> Self"));
            Assert.That(floatSource, Does.Contain("pub impl f32 = extern System.Single")
                .And.Contain("pub fn +(rhs: Self) -> Self")
                .And.Contain("pub fn @- -> Self"));
            foreach (var signature in signatures)
            {
                var physical = FindPhysical(result.Report, signature);
                var expectedType = signature.StartsWith(
                    UdonExternSignatureFormatter.GetUdonTypeName(typeof(float)) + ".",
                    StringComparison.Ordinal)
                    ? typeof(float)
                    : typeof(int);
                Assert.That(physical.clr_declaring_type,
                    Is.EqualTo(expectedType.FullName));
                Assert.That(physical.generated_surface_types,
                    Does.Contain(expectedType.FullName));
            }
            AssertAllBindingSourcesParse(result);
        }
    }
}
