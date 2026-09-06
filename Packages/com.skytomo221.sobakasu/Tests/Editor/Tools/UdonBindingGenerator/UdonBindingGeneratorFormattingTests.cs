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
    public class UdonBindingGeneratorFormattingTests
    {

        [TestCase("GameObject", "game_object")]
        [TestCase("VRCPlayerApi", "vrc_player_api")]
        [TestCase("URLLoader", "url_loader")]
        [TestCase("HTTPRequest", "http_request")]
        [TestCase("activeSelf", "active_self")]
        [TestCase("isActiveAndEnabled", "is_active_and_enabled")]
        public void NameUtility_ConvertsStableSnakeCase(
            string source,
            string expected)
        {
            Assert.That(SobakasuNameUtility.ToSnakeCase(source), Is.EqualTo(expected));
        }

        [Test]
        public void ClrMemberId_FormatsReflectionAndDiscoveredMembersCanonically()
        {
            var type = typeof(UdonBindingGeneratorFixture);
            var prefix = type.FullName.Replace('+', '.');
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.FlattenHierarchy;

            Assert.That(ClrMemberId.Format(type.GetConstructor(Type.EmptyTypes)),
                Is.EqualTo(prefix + "()"));
            Assert.That(ClrMemberId.Format(type.GetConstructor(new[] { typeof(int) })),
                Is.EqualTo(prefix + "(System.Int32)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "Find", flags, null, new[] { typeof(string) }, null)),
                Is.EqualTo(prefix + ".Find(System.String)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "Mix", flags, null, new[] { typeof(int) }, null)),
                Is.EqualTo(prefix + ".Mix(System.Int32)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "Mix", flags, null, new[] { typeof(float) }, null)),
                Is.EqualTo(prefix + ".Mix(System.Single)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "ArrayValue", flags, null, new[] { typeof(string[]) }, null)),
                Is.EqualTo(prefix + ".ArrayValue(System.String[])"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "RefValue", flags, null,
                new[] { typeof(int).MakeByRefType() }, null)),
                Is.EqualTo(prefix + ".RefValue(System.Int32&)"));
            Assert.That(ClrMemberId.Format(type.GetMethod(
                "Nested", flags, null,
                new[] { typeof(UdonBindingGeneratorFixture.NestedValue) }, null)),
                Is.EqualTo(prefix + ".Nested(" + prefix + ".NestedValue)"));
            Assert.That(ClrMemberId.Format(type.GetProperty("Count")),
                Is.EqualTo(prefix + ".Count"));
            Assert.That(ClrMemberId.Format(type.GetField("Number")),
                Is.EqualTo(prefix + ".Number"));

            var formatter = new UdonBindingTypeFormatter();
            var discovered = new UdonApiDiscovery(
                new FixtureExposure(),
                formatter).Discover(new[] { type });
            UdonApiMemberModel discoveredMix = null;
            foreach (var member in discovered.Types[0].Members)
            {
                if (member.MemberName == "Mix" &&
                    member.Callable.GetParameters()[0].ParameterType == typeof(int))
                {
                    discoveredMix = member;
                    break;
                }
            }
            Assert.That(discoveredMix, Is.Not.Null);
            Assert.That(ClrMemberId.Format(discoveredMix),
                Is.EqualTo(prefix + ".Mix(System.Int32)"));
        }

        [Test]
        public void TypeFormatter_ReusesBuiltInsAndRejectsUnsupportedShapes()
        {
            var formatter = new UdonBindingTypeFormatter();

            AssertFormats(formatter, typeof(int), "i32");
            AssertFormats(formatter, typeof(long), "i64");
            AssertFormats(formatter, typeof(float), "f32");
            AssertFormats(formatter, typeof(double), "f64");
            AssertFormats(formatter, typeof(bool), "bool");
            AssertFormats(formatter, typeof(string), "string");
            AssertFormats(formatter, typeof(int[]), "[i32]");
            AssertFormats(formatter, typeof(int).MakeByRefType(), "i32");

            Assert.That(formatter.TryFormat(
                typeof(UdonBindingGeneratorFixture),
                typeof(UdonBindingGeneratorFixture),
                out var selfType,
                out _), Is.True);
            Assert.That(selfType, Is.EqualTo("Self"));

            Assert.That(formatter.TryFormat(
                typeof(int[,]),
                typeof(UdonBindingGeneratorFixture),
                out _,
                out var arrayReason), Is.False);
            Assert.That(arrayReason, Does.Contain("Array shape"));

            AssertFormats(formatter, typeof(List<int>),
                "System.Collections.Generic.List<i32>");
            var genericParameter = typeof(UdonBindingGeneratorFixture)
                .GetMethod("Generic").GetGenericArguments()[0];
            Assert.That(formatter.TryFormat(
                genericParameter.MakeArrayType(),
                typeof(UdonBindingGeneratorFixture),
                out var genericArray,
                out _), Is.True);
            Assert.That(genericArray, Is.EqualTo("[T]"));

            Assert.That(formatter.TryFormat(
                typeof(int).MakePointerType(),
                typeof(UdonBindingGeneratorFixture),
                out _,
                out var pointerReason), Is.False);
            Assert.That(pointerReason, Does.Contain("Pointer type"));
        }

        [Test]
        public void TypeFormatter_AllowsCanonicalPrimitivesWithInstalledCatalog()
        {
            var formatter = new UdonBindingTypeFormatter(
                SobakasuBuiltInEnvironment.Default.ExternCatalog);

            Assert.That(formatter.CanDeclareType(typeof(long), out var reason),
                Is.True, reason);
            Assert.That(formatter.CanDeclareType(typeof(object), out _), Is.False);
        }
    }
}
