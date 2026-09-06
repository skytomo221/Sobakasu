using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Desugar;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Optimizer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;
using UnityEditor;
using UnityEngine;

using static Skytomo221.Sobakasu.Tests.Editor.ImplExternTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class ImplExternDiagnosticsTests
    {

        private const string MaybeDefinition = @"
lang ""maybe""
enum Maybe<T> {
  Nothing,
  Just(T),
}
";
        private const string ProjectedTryGetSignature =
            "TestApi.__TryGet__TestOwnerRef__SystemBoolean";
        private const string ProjectedMixedSignature =
            "TestApi.__Mixed__SystemInt32Ref_TestOwnerRef_SystemStringRef__SystemInt32";
        private const string ProjectedValiditySignature =
            "VRCSDKBaseUtilities.__IsValid__TestOwner__SystemBoolean";
        private const string ProjectedConstructorMaybeSignature =
            "TestFoo.__ctor__TestOwnerRef__TestFoo";
        private const string ExternAbiBindingsSource = @"
fn ref_only(value: i32) -> i32
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.RefOnly(
      ref i32 value);
fn out_only() -> i32
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.OutOnly(
      out i32 value);
fn return_and_out() -> (bool, i32)
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.ReturnAndOut(
      out i32 value);
fn mixed(normal: i32, value: i32, flag: bool)
    -> (i32, i32, string, bool)
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.Mixed(
      i32 normal, ref i32 value, out string text, ref bool flag);
";

        private readonly List<string> _cleanupAssetPaths = new();

        [TearDown]
        public void TearDown()
        {
            if (_cleanupAssetPaths.Count == 0)
            {
                return;
            }

            _cleanupAssetPaths.Sort((left, right) => right.Length.CompareTo(left.Length));
            foreach (var assetPath in _cleanupAssetPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null ||
                    AssetDatabase.IsValidFolder(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }

            _cleanupAssetPaths.Clear();
            AssetDatabase.Refresh();
        }
        private SobakasuProgramAsset CreateProgramAsset()
        {
            return SobakasuTestAssetFactory.CreateImportedProgramAsset(
                "SobakasuImplExternTests",
                RegisterForCleanup);
        }
        private void RegisterForCleanup(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
                _cleanupAssetPaths.Add(assetPath);
        }
        public void Binder_ReportsImplAndOperatorDiagnostics(
            string source,
            string expectedCode)
        {
            var binder = Bind(source);

            Assert.That(ContainsCode(binder.Diagnostics.Diagnostics, expectedCode), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void GenericExtern_ReportsClrConstraintViolationInBinder()
        {
            var binder = Bind(@"
pub impl GenericApi = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture {
  pub fn echo<T>(value: T) -> T = extern self.Echo<T>(value)
}
on start {
  let api = extern new Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture();
  let value = api.echo<i32>(1);
}", CreateGenericExternEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "SBK2126"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

        [TestCase("let mut value = 1; value += 2;", "SBK2005")]
        [TestCase("let values = [1]; values[0] += 2;", "SBK2098")]
        [TestCase("let mut holder = Holder { value: 1 }; holder.value += 2;", "SBK2005")]
        public void Binder_ReportsIncompatibleCompoundOperatorResult(string statement, string expectedCode)
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary($@"
impl i32 {{ pub fn +(rhs: Self) -> bool {{ true }} }}
struct Holder {{ value: i32, }}
on start {{ {statement} }}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, expectedCode), Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_RejectsRemovedNullLiteralBeforeOverloadResolution()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl GameObject = extern UnityEngine.GameObject {}
impl i32 {
  fn choose(value: GameObject) -> i32 { 1 }
  fn choose(value: string) -> i32 { 2 }
}
on interact {
  let receiver = 1;
  receiver.choose(null);
}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK0007"), Is.True,
                result.ErrorText);
        }

        [Test]
        public void Binder_ReportsNoApplicableMethodOverload()
        {
            var binder = Bind(
                @"impl i32 {
  fn choose(value: bool) -> i32 { 1 }
}
on interact {
  let receiver = 1;
  receiver.choose(2);
}");

            Assert.That(ContainsCode(binder.Diagnostics.Diagnostics, "SBK2081"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsUnsupportedAndUnknownExternExpressions()
        {
            var unsupported = Bind("on interact { extern 1; }");
            Assert.That(ContainsCode(unsupported.Diagnostics.Diagnostics, "SBK2087"), Is.True,
                Format(unsupported.Diagnostics.Diagnostics));

            var unknown = Bind(
                "on interact { extern UnityEngine.Debug.MemberThatDoesNotExist; }");
            Assert.That(ContainsCode(unknown.Diagnostics.Diagnostics, "SBK2083"), Is.True,
                Format(unknown.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsExternalExposureAndOverloadDiagnostics()
        {
            var notExposed = Bind(
                "on interact { extern System.Console.WriteLine(1); }");
            Assert.That(ContainsCode(notExposed.Diagnostics.Diagnostics, "SBK2084"), Is.True,
                Format(notExposed.Diagnostics.Diagnostics));

            var notApplicable = Bind(
                "on interact { extern UnityEngine.Mathf.Clamp(\"x\", 0, 1); }");
            Assert.That(ContainsCode(notApplicable.Diagnostics.Diagnostics, "SBK2085"), Is.True,
                Format(notApplicable.Diagnostics.Diagnostics));

            var ambiguous = Bind(
                "on interact { extern Test.Api.Call(1); }",
                CreateAmbiguousExternEnvironment());
            Assert.That(ContainsCode(ambiguous.Diagnostics.Diagnostics, "SBK2086"), Is.True,
                Format(ambiguous.Diagnostics.Diagnostics));
        }

        [Test]
        public void Parser_RejectsGeneralExpressionBodiedFunctionAndRecovers()
        {
            var parser = new SobakasuParser(SourceText.From(
                "pub fn bad = 123 pub fn good { }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1038"), Is.True,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(2));
            Assert.That(((FunctionDeclarationSyntax)syntax.Members[1]).Name,
                Is.EqualTo("good"));
        }

        [TestCase("maybe ref Test.Owner owner")]
        [TestCase("maybe Test.Owner owner")]
        public void Parser_RejectsMaybeOnNonOutAbiParameters(string parameter)
        {
            var parser = new SobakasuParser(SourceText.From(
                $"fn invalid() = extern Test.Api.TryGet({parameter})"));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1039"),
                Is.True, Format(parser.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_RejectsMaybeOutForValueTypesAndReturnMismatches()
        {
            var environment = CreateProjectionEnvironment();
            var invalidType = Bind(
                MaybeDefinition + @"
fn invalid() -> Maybe<i32>
  = extern Test.Api.OutInt(maybe out i32 value)",
                environment);
            var invalidReturn = Bind(
                MaybeDefinition + @"
fn invalid() -> Test.Owner
  = extern Test.Api.TryGet(maybe out Test.Owner owner)",
                environment);

            Assert.That(ContainsCode(
                    invalidType.Diagnostics.Diagnostics,
                    "SBK2164"),
                Is.True, Format(invalidType.Diagnostics.Diagnostics));
            Assert.That(ContainsCode(
                    invalidReturn.Diagnostics.Diagnostics,
                    "SBK2159"),
                Is.True, Format(invalidReturn.Diagnostics.Diagnostics));
        }
    }
}
