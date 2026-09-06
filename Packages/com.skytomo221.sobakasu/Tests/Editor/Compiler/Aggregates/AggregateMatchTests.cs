using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEditor;
using UnityEngine;

using static Skytomo221.Sobakasu.Tests.Editor.AggregateTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class AggregateMatchTests
    {
        private readonly List<string> _cleanupAssetPaths = new();

        [TearDown]
        public void TearDown()
        {
            if (_cleanupAssetPaths.Count == 0)
            {
                return;
            }

            if (_cleanupAssetPaths.Count == 0)
                return;

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

        [Test]
        public void Compiler_CompilesPreludeMaybeConstructionAndMatch()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on start {
  let value: Maybe<i32> = Maybe.Nothing;
  let other: Maybe<i32> = Maybe.Just(42);
  let resolved = match other {
    Maybe.Just(x) => x,
    Maybe.Nothing => 0,
  };
  extern UnityEngine.Debug.Log(resolved);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Maybe"));
            Assert.That(result.Uasm, Does.Contain("op_Equality"));
        }

        [Test]
        public void Lexer_RecognizesMatchFatArrowAndPreservesRelatedOperators()
        {
            var tokens = LexAll("match => = == > >= ->");

            Assert.That(tokens.ConvertAll(token => token.Kind), Is.EqualTo(new[]
            {
                SyntaxKind.MatchKeyword,
                SyntaxKind.FatArrowToken,
                SyntaxKind.EqualsToken,
                SyntaxKind.EqualsEqualsToken,
                SyntaxKind.GreaterToken,
                SyntaxKind.GreaterOrEqualsToken,
                SyntaxKind.ArrowToken,
                SyntaxKind.EndOfFile
            }));
        }

        [Test]
        public void Parser_ParsesMatchPatternsBlockArmsAndOptionalTrailingComma()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"enum Option { None, Some(i32), }
fn choose(value: Option) -> i32 {
  match value {
    Option.None => { 0 },
    Option.Some(value) => value
  }
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = (FunctionDeclarationSyntax)syntax.Members[1];
            var match = function.Body.TrailingExpression as MatchExpressionSyntax;
            Assert.That(match, Is.Not.Null);
            Assert.That(match.Expression, Is.TypeOf<NameExpressionSyntax>());
            Assert.That(match.Arms.Count, Is.EqualTo(2));
            Assert.That(match.Arms[0].Pattern, Is.TypeOf<EnumUnitVariantPatternSyntax>());
            Assert.That(match.Arms[0].Expression, Is.TypeOf<BlockExpressionSyntax>());
            Assert.That(match.Arms[0].CommaToken, Is.Not.Null);
            Assert.That(match.Arms[1].Pattern, Is.TypeOf<EnumTupleVariantPatternSyntax>());
            Assert.That(match.Arms[1].CommaToken, Is.Null);
        }

        [Test]
        public void Parser_DoesNotConsumeMatchScrutineeBraceAsAggregateInitializer()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"enum Choice { First, Second, }
fn choose(value: Choice) -> i32 {
  match value { Choice.First => 1, Choice.Second => 2, }
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = (FunctionDeclarationSyntax)syntax.Members[1];
            var match = (MatchExpressionSyntax)function.Body.TrailingExpression;
            Assert.That(match.Expression, Is.TypeOf<NameExpressionSyntax>());
            Assert.That(match.Arms.Count, Is.EqualTo(2));
        }

        [Test]
        public void Parser_RecoversFromMalformedMatchBeforeFollowingFunction()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"fn broken(value: i32) -> i32 {
  match value { 0 1, _ => 2, }
}
fn after() -> i32 { 3 }
on start {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members.Count, Is.EqualTo(3));
            Assert.That(((FunctionDeclarationSyntax)syntax.Members[1]).Identifier.Text,
                Is.EqualTo("after"));
        }

        [Test]
        public void Compiler_CompilesGenericOptionMatchMethodsAndNeverArm()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"enum Option<T> { None, Some(T), }
enum Result<T> { Ok(T), Err(string), }
impl<T> Option<T> {
  pub fn unwrap_or(default: T) -> T {
    match self {
      Option.None => default,
      Option.Some(value) => value,
    }
  }
  pub fn is_some? -> bool {
    match self {
      Option.None => false,
      Option.Some(_) => true,
    }
  }
}
fn has_value(value: Option<i32>) -> bool {
  match value {
    Option.Some(_) => true,
    _ => false,
  }
}
fn unwrap(result: Result<i32>) -> i32 {
  match result {
    Result.Ok(value) => value,
    Result.Err(_) => { return 0; },
  }
}
on start {
  let option = Option.Some(10);
  let value: i32 = option.unwrap_or(20);
  let present: bool = option.is_some?;
  let fallback: bool = has_value(option);
  let unwrapped = unwrap(Result.Ok(value));
  extern UnityEngine.Debug.Log(unwrapped);
  extern UnityEngine.Debug.Log(present);
  extern UnityEngine.Debug.Log(fallback);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("op_Equality"));
            Assert.That(result.Uasm, Does.Not.Contain("MATCH,"));
        }

        [Test]
        public void Compiler_EvaluatesMatchScrutineeExactlyOnce()
        {
            const string signature =
                "UnityEngineMathf.__Abs__SystemInt32__SystemInt32";
            var result = SobakasuCompiler.CompileToUasm(
                @"enum Option { None, Some(i32), }
fn create() -> Option { Option.Some(extern UnityEngine.Mathf.Abs(-1)) }
on start {
  let value = match create() {
    Option.None => 0,
    Option.Some(value) => value,
  };
  extern UnityEngine.Debug.Log(value);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, signature), Is.EqualTo(1));
        }

        [Test]
        public void IrLowerer_DoesNotWriteMatchResultForNeverArm()
        {
            var (program, diagnostics) = Bind(
                @"enum Option { None, Some(i32), }
on start {
  let option = Option.Some(10);
  let result = match option {
    Option.None => { return; },
    Option.Some(value) => value,
  };
  extern UnityEngine.Debug.Log(result);
}");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);

            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));
            IrBasicBlock neverArm = null;
            foreach (var block in ir.Modules[0].Blocks)
            {
                if (block.Label.Contains("match_arm"))
                {
                    neverArm = block;
                    break;
                }
            }

            Assert.That(neverArm, Is.Not.Null);
            Assert.That(neverArm.Terminator, Is.TypeOf<IrReturnTerminator>());
            Assert.That(neverArm.Instructions, Is.Empty);
        }

        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { match v { Option.Some(x) => x, } } on start {}", "SBK2126")]
        [TestCase("fn f(v: bool) -> i32 { match v { true => 1, } } on start {}", "SBK2126")]
        [TestCase("fn f(v: i32) -> i32 { match v { 0 => 0, } } on start {}", "SBK2126")]
        [TestCase("fn f(v: char) -> i32 { match v { 'a' => 1, } } on start {}", "SBK2126")]
        [TestCase("fn f(v: string) -> i32 { match v { \"a\" => 1, } } on start {}", "SBK2126")]
        [TestCase("fn f(v: i32) -> i32 { match v { _ => 0, 1 => 1, } } on start {}", "SBK2127")]
        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { match v { Option.Some(x) => x, Option.Some(y) => y, Option.None => 0, } } on start {}", "SBK2127")]
        [TestCase("fn f(v: i32) -> i32 { match v { 0 => 1, 0 => 2, _ => 3, } } on start {}", "SBK2127")]
        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { match v { Option.None => 0, Option.Some(x) => x, _ => 1, } } on start {}", "SBK2127")]
        [TestCase("fn f(v: bool) -> i32 { match v { true => 1, false => 0, _ => 2, } } on start {}", "SBK2127")]
        [TestCase("enum Option { None, } fn f(v: Option) -> i32 { match v { Option.Missing => 0, Option.None => 1, } } on start {}", "SBK2111")]
        [TestCase("enum A { X, } enum B { X, } fn f(v: A) -> i32 { match v { B.X => 0, A.X => 1, } } on start {}", "SBK2128")]
        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { match v { Option.None => 0, Option.Some() => 1, } } on start {}", "SBK2129")]
        [TestCase("enum Event { Click { x: i32, y: i32, }, } fn f(v: Event) -> i32 { match v { Event.Click { x, z } => x, } } on start {}", "SBK2130")]
        [TestCase("enum Event { Click { x: i32, }, } fn f(v: Event) -> i32 { match v { Event.Click { x, x } => x, } } on start {}", "SBK2131")]
        [TestCase("enum Event { Click { x: i32, y: i32, }, } fn f(v: Event) -> i32 { match v { Event.Click { x } => x, } } on start {}", "SBK2132")]
        [TestCase("fn f(v: i32) -> i32 { match v { 1u8 => 1, _ => 0, } } on start {}", "SBK2133")]
        [TestCase("enum Pair { Values(i32, i32), } fn f(v: Pair) -> i32 { match v { Pair.Values(x, x) => x, } } on start {}", "SBK2134")]
        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { match v { Option.None => 0, Option.Some(value) => \"value\", } } on start {}", "SBK2135")]
        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { match v { Option.None => 0, Option.Some(0) => 1, } } on start {}", "SBK1027")]
        [TestCase("fn f(v: i32) -> i32 { match v { 1.0 => 1, _ => 0, } } on start {}", "SBK1027")]
        [TestCase("fn f(v: string) -> i32 { match v { null => 1, _ => 0, } } on start {}", "SBK0007")]
        [TestCase("fn f(v: bool) -> i32 { match v { true if v => 1, false => 0, } } on start {}", "SBK1027")]
        [TestCase("fn f(v: bool) -> i32 { match v { true | false => 1, _ => 0, } } on start {}", "SBK1027")]
        [TestCase("fn f(v: i32) -> i32 { match v { 0..=10 => 1, _ => 0, } } on start {}", "SBK1027")]
        [TestCase("struct Point { x: i32, y: i32, } fn f(v: Point) -> i32 { match v { Point { x, y } => x, _ => 0, } } on start {}", "SBK1027")]
        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { match v { Some(x) => x, _ => 0, } } on start {}", "SBK1027")]
        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { match v { .Some(x) => x, _ => 0, } } on start {}", "SBK1027")]
        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { match v { Option.None => 0, Option.Some(value) => { value = 1; value }, } } on start {}", "SBK2016")]
        [TestCase("enum Option { None, Some(i32), } fn f(v: Option) -> i32 { let result = match v { Option.None => 0, Option.Some(value) => value, }; value } on start {}", "SBK2002")]
        public void Compiler_ReportsMatchDiagnostics(string source, string expectedCode)
        {
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, expectedCode), Is.True,
                result.ErrorText);
        }
    }
}
