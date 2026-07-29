using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuFunctionDeclarationTests
    {
        [Test]
        public void Parser_ParsesFunctionDeclarationWithReturnType()
        {
            var function = ParseSingleFunction(
                @"fn add(x: i32, y: i32) -> i32 {
  return x + y;
}");

            Assert.That(function.Identifier.Text, Is.EqualTo("add"));
            Assert.That(function.Parameters.Count, Is.EqualTo(2));
            Assert.That(function.Parameters[0].Identifier.Text, Is.EqualTo("x"));
            Assert.That(function.Parameters[0].Type.GetText(), Is.EqualTo("i32"));
            Assert.That(function.ReturnTypeAnnotation, Is.Not.Null);
            Assert.That(function.ReturnTypeAnnotation.Type.GetText(), Is.EqualTo("i32"));
            Assert.That(function.Body.TrailingExpression, Is.Null);
        }

        [Test]
        public void Parser_ParsesFunctionTrailingExpression()
        {
            var function = ParseSingleFunction(
                @"fn add(x: i32, y: i32) -> i32 {
  x + y
}");

            Assert.That(function.Body.Statements, Is.Empty);
            Assert.That(function.Body.TrailingExpression, Is.Not.Null);
        }

        [Test]
        public void Parser_KeepsSemicolonExpressionAsStatement()
        {
            var function = ParseSingleFunction(
                @"fn add(x: i32, y: i32) -> i32 {
  x + y;
}");

            Assert.That(function.Body.Statements.Count, Is.EqualTo(1));
            Assert.That(function.Body.TrailingExpression, Is.Null);
        }

        [Test]
        public void Binder_BindsOrderIndependentFunctionCallFromEvent()
        {
            var program = BindProgram(
                @"on Interact() {
  Debug.Log(message());
}

fn message() -> string {
  ""Hello""
}");

            Assert.That(program.Functions.Count, Is.EqualTo(1));
            var statement = program.Events[0].Body.Statements[0] as BoundExpressionStatement;
            Assert.That(statement, Is.Not.Null);

            var logCall = statement.Expression as BoundCallExpression;
            Assert.That(logCall, Is.Not.Null);
            Assert.That(logCall.Arguments[0], Is.TypeOf<BoundUserFunctionCallExpression>());
        }

        [Test]
        public void Binder_TreatsTrailingExpressionAsFunctionReturn()
        {
            var program = BindProgram(
                @"fn add(x: i32, y: i32) -> i32 {
  x + y
}

on Interact() {
  add(1, 2);
}");

            var function = program.Functions[0];
            Assert.That(function.Body.Statements.Count, Is.EqualTo(1));
            Assert.That(function.Body.Statements[0], Is.TypeOf<BoundReturnStatement>());
        }

        [TestCase(
            @"fn add() -> i32 {
  1 + 1;
}

on Interact() {
}",
            "SBK2038")]
        [TestCase(
            @"fn log() {
  return 1;
}

on Interact() {
}",
            "SBK2039")]
        [TestCase(
            @"fn value() -> i32 {
  return ""x"";
}

on Interact() {
}",
            "SBK2040")]
        [TestCase(
            @"fn value(x: i32, x: i32) {
}

on Interact() {
}",
            "SBK2041")]
        [TestCase(
            @"fn value() {
}

fn value() {
}

on Interact() {
}",
            "SBK2043")]
        [TestCase(
            @"fn value(x: i32) {
}

on Interact() {
  value();
}",
            "SBK2004")]
        [TestCase(
            @"fn value(x: i32) {
}

on Interact() {
  value(""x"");
}",
            "SBK2005")]
        [TestCase(
            @"fn value() -> i32 {
  return value();
}

on Interact() {
}",
            "SBK2045")]
        [TestCase(
            @"fn a() -> i32 {
  return b();
}

fn b() -> i32 {
  return a();
}

on Interact() {
}",
            "SBK2045")]
        [TestCase(
            @"fn value() -> i32 {
  1
}

on Interact() {
  let value = 1;
  value();
}",
            "SBK2012")]
        [TestCase(
            @"use UnityEngine.Debug.Log as message;

fn message() {
}

on Interact() {
  message();
}",
            "SBK2044")]
        public void Binder_ReportsExpectedFunctionDiagnostics(
            string source,
            string expectedDiagnosticCode)
        {
            var binder = CreateBinder(source);

            Assert.That(
                ContainsDiagnosticCode(binder.Diagnostics.Diagnostics, expectedDiagnosticCode),
                Is.True,
                BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void CompileToUasm_InlinesValueReturningFunctionCall()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on Interact() {
  Debug.Log(message());
}

fn message() -> string {
  ""Hello""
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export _interact"));
            Assert.That(result.Uasm, Does.Not.Contain(".export message"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineDebug.__Log__SystemObject__SystemVoid"));
        }

        [Test]
        public void CompileToUasm_InlinesU0FunctionCall()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn log_message(message: string) {
  Debug.Log(message);
}

on Interact() {
  log_message(""Hello"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export _interact"));
            Assert.That(result.Uasm, Does.Not.Contain(".export log_message"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineDebug.__Log__SystemObject__SystemVoid"));
        }

        [Test]
        public void CompileToUasm_CanInlineSameFunctionMultipleTimes()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn add(x: i32, y: i32) -> i32 {
  x + y
}

on Interact() {
  Debug.Log(add(1, 2));
  Debug.Log(add(3, 4));
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("__temp_0"));
            Assert.That(result.Uasm, Does.Contain("__temp_1"));
            Assert.That(result.Uasm, Does.Not.Contain(".export add"));
        }

        private static FunctionDeclarationSyntax ParseSingleFunction(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);
            Assert.That(syntax.Members.Count, Is.EqualTo(1));

            var function = syntax.Members[0] as FunctionDeclarationSyntax;
            Assert.That(function, Is.Not.Null);
            return function;
        }

        private static SobakasuBinder CreateBinder(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);

            var binder = new SobakasuBinder();
            binder.BindProgram(syntax);
            return binder;
        }

        private static BoundProgram BindProgram(string source)
        {
            var binder = CreateBinder(source);
            Assert.That(
                binder.Diagnostics.Diagnostics,
                Is.Empty,
                BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));

            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            var cleanBinder = new SobakasuBinder();
            return cleanBinder.BindProgram(syntax);
        }

        private static bool ContainsDiagnosticCode(
            IReadOnlyList<Diagnostic> diagnostics,
            string expectedCode)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == expectedCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildDiagnosticMessage(IReadOnlyList<Diagnostic> diagnostics)
        {
            var lines = new List<string>();
            foreach (var diagnostic in diagnostics)
            {
                lines.Add($"{diagnostic.Code}: {diagnostic.Message}");
            }

            return string.Join("\n", lines);
        }
    }
}
