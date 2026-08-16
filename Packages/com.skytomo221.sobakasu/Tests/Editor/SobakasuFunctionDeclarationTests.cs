using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuFunctionDeclarationTests
    {
        [Test]
        public void Lexer_SeparatesCallableQuestionSuffixFromIdentifiersAndOperators()
        {
            var tokens = LexAll("!ready? != other && value & mask");

            Assert.That(tokens, Has.Count.EqualTo(10));
            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.BangToken));
            Assert.That(tokens[1].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[1].Text, Is.EqualTo("ready"));
            Assert.That(tokens[2].Kind, Is.EqualTo(SyntaxKind.QuestionToken));
            Assert.That(tokens[3].Kind, Is.EqualTo(SyntaxKind.BangEqualsToken));
            Assert.That(tokens[5].Kind, Is.EqualTo(SyntaxKind.AmpersandAmpersandToken));
            Assert.That(tokens[7].Kind, Is.EqualTo(SyntaxKind.AmpersandToken));
            Assert.That(tokens[9].Kind, Is.EqualTo(SyntaxKind.EndOfFile));
        }

        [TestCase("fn reset {}", "reset", false)]
        [TestCase("fn reset() {}", "reset", true)]
        [TestCase("fn ready? -> bool { true }", "ready?", false)]
        [TestCase("fn ready?() -> bool { true }", "ready?", true)]
        public void Parser_ParsesOptionalZeroArgumentFunctionParentheses(
            string source,
            string expectedName,
            bool hasParentheses)
        {
            var function = ParseSingleFunction(source);

            Assert.That(function.Name, Is.EqualTo(expectedName));
            Assert.That(function.Parameters, Is.Empty);
            Assert.That(function.OpenParenToken != null, Is.EqualTo(hasParentheses));
            Assert.That(function.CloseParenToken != null, Is.EqualTo(hasParentheses));
        }

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
                @"on interact() {
  extern UnityEngine.Debug.Log(message());
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
        public void Binder_BindsParenthesizedAndBareNamesToTheSameZeroArgumentFunction()
        {
            var program = BindProgram(
                @"fn reset {
}

fn ready? -> bool {
  true
}

on interact {
  reset;
  reset();
  ready?;
  ready?();
}");

            var bareCall = ((BoundExpressionStatement)program.Events[0].Body.Statements[0])
                .Expression as BoundUserFunctionCallExpression;
            var parenthesizedCall = ((BoundExpressionStatement)program.Events[0].Body.Statements[1])
                .Expression as BoundUserFunctionCallExpression;

            Assert.That(bareCall, Is.Not.Null);
            Assert.That(parenthesizedCall, Is.Not.Null);
            Assert.That(bareCall.Function, Is.SameAs(parenthesizedCall.Function));
            Assert.That(bareCall.Arguments, Is.Empty);
            Assert.That(parenthesizedCall.Arguments, Is.Empty);

            var bareQuestionCall = ((BoundExpressionStatement)program.Events[0].Body.Statements[2])
                .Expression as BoundUserFunctionCallExpression;
            var parenthesizedQuestionCall =
                ((BoundExpressionStatement)program.Events[0].Body.Statements[3])
                .Expression as BoundUserFunctionCallExpression;
            Assert.That(bareQuestionCall, Is.Not.Null);
            Assert.That(parenthesizedQuestionCall, Is.Not.Null);
            Assert.That(
                bareQuestionCall.Function,
                Is.SameAs(parenthesizedQuestionCall.Function));
            Assert.That(bareQuestionCall.Function.Name, Is.EqualTo("ready?"));
        }

        [Test]
        public void Binder_BindsQuestionFunctionAndLogicalNegationWithoutBoolNameConstraint()
        {
            var program = BindProgram(
                @"fn ready? -> bool { true }
fn answer? -> i32 { 42 }

on interact {
  if !ready? {
  }
  extern UnityEngine.Debug.Log(answer?);
}");

            Assert.That(program.Functions[0].Name, Is.EqualTo("ready?"));
            Assert.That(program.Functions[1].Name, Is.EqualTo("answer?"));
        }

        [Test]
        public void Binder_KeepsLocalParameterAndStateNamesAsValueReferences()
        {
            var program = BindProgram(
                @"state state_value = true;

fn echo(value: bool) -> bool { value }

on interact {
  let local_value = true;
  local_value;
  state_value;
  echo(false);
}");

            var localReference = ((BoundExpressionStatement)program.Events[0].Body.Statements[1])
                .Expression as BoundNameExpression;
            var stateReference = ((BoundExpressionStatement)program.Events[0].Body.Statements[2])
                .Expression as BoundNameExpression;
            var parameterReturn = program.Functions[0].Body.Statements[0]
                as BoundReturnStatement;
            var parameterReference = parameterReturn.Expression as BoundNameExpression;

            Assert.That(localReference.Symbol, Is.TypeOf<LocalVariableSymbol>());
            Assert.That(stateReference.Symbol, Is.TypeOf<StateVariableSymbol>());
            Assert.That(parameterReference.Symbol, Is.TypeOf<ParameterSymbol>());
        }

        [Test]
        public void Binder_TreatsTrailingExpressionAsFunctionReturn()
        {
            var program = BindProgram(
                @"fn add(x: i32, y: i32) -> i32 {
  x + y
}

on interact() {
  add(1, 2);
}");

            var function = program.Functions[0];
            Assert.That(function.Body.Statements.Count, Is.EqualTo(1));
            Assert.That(function.Body.Statements[0], Is.TypeOf<BoundReturnStatement>());
        }

        [Test]
        public void Binder_ResolvesTopLevelOverloadsByTypeArityAndExistingWidening()
        {
            var program = BindProgram(
                @"fn choose(value: i32) -> i32 { 11 }
fn choose(value: string) -> i32 { 22 }
fn many(value: i32) -> i32 { 31 }
fn many(value: i32, other: i32) -> i32 { 32 }
fn widen(value: i16) -> i32 { 41 }
fn widen(value: i32) -> i32 { 42 }
fn call_widen(value: i8) -> i32 { widen(value) }

on interact {
  choose(1);
  choose(""value"");
  many(1);
  many(1, 2);
}");

            var first = GetEventFunctionCall(program, 0);
            var second = GetEventFunctionCall(program, 1);
            var third = GetEventFunctionCall(program, 2);
            var fourth = GetEventFunctionCall(program, 3);
            Assert.That(first.Function.Parameters[0].Type, Is.SameAs(TypeSymbol.I32));
            Assert.That(second.Function.Parameters[0].Type, Is.SameAs(TypeSymbol.String));
            Assert.That(third.Function.Parameters, Has.Count.EqualTo(1));
            Assert.That(fourth.Function.Parameters, Has.Count.EqualTo(2));

            var wideningCaller = FindFunction(program, "call_widen");
            var wideningReturn = wideningCaller.Body.Statements[0] as BoundReturnStatement;
            var wideningCall = wideningReturn.Expression as BoundUserFunctionCallExpression;
            Assert.That(wideningCall, Is.Not.Null);
            Assert.That(wideningCall.Function.Parameters[0].Type, Is.SameAs(TypeSymbol.I16));

            var chooseFunctions = FindFunctions(program, "choose");
            Assert.That(chooseFunctions, Has.Count.EqualTo(2));
            Assert.That(
                chooseFunctions[0].FunctionSymbol.InternalIdentity,
                Is.Not.EqualTo(chooseFunctions[1].FunctionSymbol.InternalIdentity));
        }

        [Test]
        public void Binder_ReportsNoMatchAndAmbiguousTopLevelOverloadsWithCandidates()
        {
            var noMatch = CreateBinder(
                @"fn parse(value: i32) {}
fn parse(value: f32) {}
on interact { parse(""value""); }");
            Assert.That(
                ContainsDiagnosticCode(noMatch.Diagnostics.Diagnostics, "SBK2155"),
                Is.True,
                BuildDiagnosticMessage(noMatch.Diagnostics.Diagnostics));
            Assert.That(noMatch.Diagnostics.Diagnostics[0].Message, Does.Contain("parse(i32)"));
            Assert.That(noMatch.Diagnostics.Diagnostics[0].Message, Does.Contain("parse(f32)"));

            var ambiguous = CreateBinder(
                @"fn choose(left: i16, right: i32) {}
fn choose(left: i32, right: i16) {}
fn invoke(value: i8) { choose(value, value); }");
            Assert.That(
                ContainsDiagnosticCode(ambiguous.Diagnostics.Diagnostics, "SBK2156"),
                Is.True,
                BuildDiagnosticMessage(ambiguous.Diagnostics.Diagnostics));
            Assert.That(ambiguous.Diagnostics.Diagnostics[0].Message, Does.Contain("choose(i16, i32)"));
            Assert.That(ambiguous.Diagnostics.Diagnostics[0].Message, Does.Contain("choose(i32, i16)"));
        }

        [TestCase(
            @"fn add() -> i32 {
  1 + 1;
}

on interact() {
}",
            "SBK2038")]
        [TestCase(
            @"fn log() {
  return 1;
}

on interact() {
}",
            "SBK2039")]
        [TestCase(
            @"fn value() -> i32 {
  return ""x"";
}

on interact() {
}",
            "SBK2040")]
        [TestCase(
            @"fn value(x: i32, x: i32) {
}

on interact() {
}",
            "SBK2041")]
        [TestCase(
            @"fn value() {
}

fn value() {
}

on interact() {
}",
            "SBK2154")]
        [TestCase(
            @"fn value(input: i32) -> i32 { input }
fn value(input: i32) -> string { ""value"" }
on interact {}",
            "SBK2154")]
        [TestCase(
            @"fn value(x: i32) {
}

on interact() {
  value();
}",
            "SBK2004")]
        [TestCase(
            @"fn value(x: i32) {
}

on interact() {
  value(""x"");
}",
            "SBK2005")]
        [TestCase(
            @"fn value() -> i32 {
  return value();
}

on interact() {
}",
            "SBK2045")]
        [TestCase(
            @"fn a() -> i32 {
  return b();
}

fn b() -> i32 {
  return a();
}

on interact() {
}",
            "SBK2045")]
        [TestCase(
            @"fn value() -> i32 {
  1
}

on interact() {
  let value = 1;
  value();
}",
            "SBK2012")]
        [TestCase(
            @"use UnityEngine.Debug.Log as message;

fn message() {
}

on interact() {
  message();
}",
            "SBK4011")]
        [TestCase(
            @"fn value(x: i32) {
}

on interact {
  value;
}",
            "SBK2064")]
        [TestCase(
            @"state enabled = true;

fn enabled {
}",
            "SBK2063")]
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
                @"on interact() {
  extern UnityEngine.Debug.Log(message());
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
  extern UnityEngine.Debug.Log(message);
}

on interact() {
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

on interact() {
  extern UnityEngine.Debug.Log(add(1, 2));
  extern UnityEngine.Debug.Log(add(3, 4));
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("__temp_0"));
            Assert.That(result.Uasm, Does.Contain("__temp_1"));
            Assert.That(result.Uasm, Does.Not.Contain(".export add"));
        }

        [Test]
        public void CompileToUasm_InlinesTheSelectedOverloadBodyAndExternWrapper()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn emit(value: i32) {
  extern UnityEngine.Debug.Log(""integer overload"");
}
fn emit(value: string) {
  extern UnityEngine.Debug.Log(""string overload"");
}
pub fn log(value: string) {
  extern UnityEngine.Debug.Log(value);
}
pub fn log(value: object) {
  extern UnityEngine.Debug.Log(value);
}
on interact {
  emit(1);
  emit(""value"");
  log(""Hello"");
  log(42);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("__fn_end_4"));
            Assert.That(result.Uasm, Does.Not.Contain(".export emit"));
            Assert.That(result.Uasm, Does.Not.Contain(".export log"));
        }

        [Test]
        public void CompileToUasm_ParenthesizedAndBareZeroArgumentFormsAreEquivalent()
        {
            var bare = SobakasuCompiler.CompileToUasm(
                @"fn ready? -> bool { true }
fn reset { extern UnityEngine.Debug.Log(""reset""); }
on interact { if ready? { reset; } }");
            var parenthesized = SobakasuCompiler.CompileToUasm(
                @"fn ready?() -> bool { true }
fn reset() { extern UnityEngine.Debug.Log(""reset""); }
on interact() { if ready?() { reset(); } }");

            Assert.That(bare.Success, Is.True, bare.ErrorText);
            Assert.That(parenthesized.Success, Is.True, parenthesized.ErrorText);
            Assert.That(bare.Uasm, Is.EqualTo(parenthesized.Uasm));
            Assert.That(bare.Uasm, Does.Contain(".export _interact"));
        }

        [TestCase("fn set_value value: i32 {}", "SBK1021")]
        [TestCase("on player_joined player: VRCPlayerApi {}", "SBK1021")]
        [TestCase("fn ready?? {}", "SBK1019")]
        [TestCase("fn rea?dy {}", "SBK1022")]
        [TestCase("fn sort! {}", "SBK1020")]
        [TestCase("on interact? {}", "SBK1018")]
        [TestCase("on interact { let ready? = true; }", "SBK1018")]
        [TestCase("fn set(value?: i32) {}", "SBK1018")]
        [TestCase("state ready? = true;", "SBK1018")]
        [TestCase("fn value -> bool? { true }", "SBK1018")]
        public void Parser_ReportsCallableNameAndParenthesisDiagnostics(
            string source,
            string expectedDiagnosticCode)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(
                ContainsDiagnosticCode(parser.Diagnostics.Diagnostics, expectedDiagnosticCode),
                Is.True,
                BuildDiagnosticMessage(parser.Diagnostics.Diagnostics));
        }

        [Test]
        public void Parser_RecoversAfterUnparenthesizedParameters()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"fn bad value: i32 {}
fn good {}
on interact {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(
                ContainsDiagnosticCode(parser.Diagnostics.Diagnostics, "SBK1021"),
                Is.True,
                BuildDiagnosticMessage(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(3));
            Assert.That(syntax.Members[1], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members[2], Is.TypeOf<EventDeclarationSyntax>());
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

        private static List<SyntaxToken> LexAll(string source)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));
            var tokens = new List<SyntaxToken>();
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                tokens.Add(token);
            }
            while (token.Kind != SyntaxKind.EndOfFile);

            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty);
            return tokens;
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

        private static BoundUserFunctionCallExpression GetEventFunctionCall(
            BoundProgram program,
            int statementIndex)
        {
            var statement = program.Events[0].Body.Statements[statementIndex]
                as BoundExpressionStatement;
            Assert.That(statement, Is.Not.Null);
            var call = statement.Expression as BoundUserFunctionCallExpression;
            Assert.That(call, Is.Not.Null);
            return call;
        }

        private static BoundFunctionDeclaration FindFunction(
            BoundProgram program,
            string name)
        {
            foreach (var function in program.Functions)
            {
                if (function.Name == name)
                    return function;
            }

            Assert.Fail($"Function '{name}' was not found.");
            return null;
        }

        private static List<BoundFunctionDeclaration> FindFunctions(
            BoundProgram program,
            string name)
        {
            var matches = new List<BoundFunctionDeclaration>();
            foreach (var function in program.Functions)
            {
                if (function.Name == name)
                    matches.Add(function);
            }
            return matches;
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
