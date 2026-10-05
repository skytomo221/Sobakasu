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

using static Skytomo221.Sobakasu.Tests.Editor.ImplExternTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class ImplExternDiagnosticsTests
    {

        private const string MaybeDefinition = @"
language item ""maybe""
enum Maybe<T> {
  Nothing,
  Just(T),
}
";
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
public implementation GenericApi = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture {
  public function echo<T>(self, value: T) -> T = extern self.Echo<T>(value)
}
behavior { on start {
  let api = extern new Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture();
  let value = api.echo<i32>(1);
} }", CreateGenericExternEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "SBK2126"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

        [TestCase("let mutable value = 1; value += 2;", "SBK2005")]
        [TestCase("let values = [1]; values[0] += 2;", "SBK2098")]
        [TestCase("let mutable holder = Holder { value: 1 }; holder.value += 2;", "SBK2005")]
        public void Binder_ReportsIncompatibleCompoundOperatorResult(string statement, string expectedCode)
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary($@"
implementation i32 {{ public function +(self, rhs: Self) -> bool {{ true }} }}
struct Holder {{ value: i32, }}
behavior {{ on start {{ {statement} }} }}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, expectedCode), Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_RejectsRemovedNullLiteralBeforeOverloadResolution()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"public implementation GameObject = extern UnityEngine.GameObject {}
implementation i32 {
  function choose(self, value: GameObject) -> i32 { 1 }
  function choose(self, value: string) -> i32 { 2 }
}
behavior { on interact(state) {
  let receiver = 1;
  receiver.choose(null);
} }");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK0007"), Is.True,
                result.ErrorText);
        }

        [Test]
        public void Binder_ReportsNoApplicableMethodOverload()
        {
            var binder = Bind(
                @"implementation i32 {
  function choose(self, value: bool) -> i32 { 1 }
}
behavior { on interact(state) {
  let receiver = 1;
  receiver.choose(2);
} }");

            Assert.That(ContainsCode(binder.Diagnostics.Diagnostics, "SBK2081"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsUnsupportedAndUnknownExternExpressions()
        {
            var unsupported = Bind("behavior { on interact(state) { extern 1; } }");
            Assert.That(ContainsCode(unsupported.Diagnostics.Diagnostics, "SBK2087"), Is.True,
                Format(unsupported.Diagnostics.Diagnostics));

            var unknown = Bind(
                "behavior { on interact(state) { extern UnityEngine.Debug.MemberThatDoesNotExist; } }");
            Assert.That(ContainsCode(unknown.Diagnostics.Diagnostics, "SBK2083"), Is.True,
                Format(unknown.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsExternalExposureAndOverloadDiagnostics()
        {
            var notExposed = Bind(
                "behavior { on interact(state) { extern System.Console.WriteLine(1); } }");
            Assert.That(ContainsCode(notExposed.Diagnostics.Diagnostics, "SBK2002"), Is.True,
                Format(notExposed.Diagnostics.Diagnostics));

            var notApplicable = Bind(
                "behavior { on interact(state) { extern UnityEngine.Mathf.Clamp(\"x\", 0, 1); } }");
            Assert.That(ContainsCode(notApplicable.Diagnostics.Diagnostics, "SBK2085"), Is.True,
                Format(notApplicable.Diagnostics.Diagnostics));

            var ambiguous = Bind(
                "behavior { on interact(state) { extern Test.Api.Call(1); } }",
                CreateAmbiguousExternEnvironment());
            Assert.That(ContainsCode(ambiguous.Diagnostics.Diagnostics, "SBK2086"), Is.True,
                Format(ambiguous.Diagnostics.Diagnostics));
        }

        [Test]
        public void Parser_RejectsGeneralExpressionBodiedFunctionAndRecovers()
        {
            var parser = new SobakasuParser(SourceText.From(
                "public function bad = 123 public function good { }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1038"), Is.True,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(2));
            Assert.That(((FunctionDeclarationSyntax)syntax.Members[1]).Name,
                Is.EqualTo("good"));
        }

        [TestCase("maybe ref Test::Owner owner")]
        [TestCase("maybe Test::Owner owner")]
        public void Parser_RejectsMaybeOnNonOutAbiParameters(string parameter)
        {
            var parser = new SobakasuParser(SourceText.From(
                $"function invalid() = extern Test.Api.TryGet({parameter})"));
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
function invalid() -> Maybe<i32>
  = extern Test.Api.OutInt(maybe out i32 value)",
                environment);
            var invalidReturn = Bind(
                MaybeDefinition + @"
function invalid() -> Test::Owner
  = extern Test.Api.TryGet(maybe out Test::Owner owner)",
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
