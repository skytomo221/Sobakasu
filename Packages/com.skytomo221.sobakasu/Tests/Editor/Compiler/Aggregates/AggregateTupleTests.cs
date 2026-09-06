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
    public class AggregateTupleTests
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
        public void TypeSymbol_InternsStructuralTuplesAndKeepsOneTupleDistinct()
        {
            var first = TypeSymbol.Tuple(new[] { TypeSymbol.I32, TypeSymbol.String });
            var second = TypeSymbol.Tuple(new[] { TypeSymbol.I32, TypeSymbol.String });
            var one = TypeSymbol.Tuple(new[] { TypeSymbol.I32 });
            var oneUnit = TypeSymbol.Tuple(new[] { TypeSymbol.Unit });

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.Name, Is.EqualTo("(i32, string)"));
            Assert.That(first.AggregateFields[0].Name, Is.EqualTo("0"));
            Assert.That(first.AggregateFields[1].Type, Is.SameAs(TypeSymbol.String));
            Assert.That(TypeSymbol.Tuple(Array.Empty<TypeSymbol>()), Is.SameAs(TypeSymbol.Unit));
            Assert.That(one.Name, Is.EqualTo("(i32,)"));
            Assert.That(one, Is.Not.EqualTo(TypeSymbol.I32));
            Assert.That(oneUnit.Name, Is.EqualTo("((),)"));
            Assert.That(oneUnit, Is.Not.EqualTo(TypeSymbol.Unit));
        }

        [Test]
        public void Parser_ParsesTupleTypesValuesAccessAndNestedBindingPatterns()
        {
            var accessTokens = LexAll("value.0.1");
            Assert.That(accessTokens.ConvertAll(token => token.Kind),
                Is.EqualTo(new[]
                {
                    SyntaxKind.Identifier,
                    SyntaxKind.Dot,
                    SyntaxKind.Int32Literal,
                    SyntaxKind.Dot,
                    SyntaxKind.Int32Literal,
                    SyntaxKind.EndOfFile
                }));

            var parser = new SobakasuParser(SourceText.From(
                @"fn value(input: (i32,)) -> ((i32,), string) {
  ((input.0,), ""value"")
}
fn unit() -> () { () }
on start {
  let ((number,), text) = value((42,));
  let grouped: i32 = (number);
  let _ = unit();
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = syntax.Members[0] as FunctionDeclarationSyntax;
            Assert.That(function.Parameters[0].Type.GetText(), Is.EqualTo("(i32,)"));
            Assert.That(function.ReturnTypeAnnotation.Type.GetText(),
                Is.EqualTo("((i32,), string)"));
            var start = syntax.Members[2] as EventDeclarationSyntax;
            var declaration = start.Body.Statements[0] as VariableDeclarationStatementSyntax;
            Assert.That(declaration.Pattern, Is.TypeOf<TupleBindingPatternSyntax>());
        }

        [Test]
        public void Compiler_LowersNestedTuplesToLeafSlotsWithoutRuntimeTupleObjects()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn value(input: (i32,)) -> ((i32,), string) {
  ((input.0,), ""value"")
}
fn unit() -> () { () }
on start {
  let ((number,), text) = value((42,));
  let nested = ((number, text), true);
  let ((copied, _), flag) = nested;
  let grouped: i32 = (copied);
  let _ = unit();
  extern UnityEngine.Debug.Log(grouped);
  extern UnityEngine.Debug.Log(flag);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("%SystemInt32"));
            Assert.That(result.Uasm, Does.Contain("%SystemString"));
            Assert.That(result.Uasm, Does.Contain("%SystemBoolean"));
            Assert.That(result.Uasm, Does.Not.Contain("SystemValueTuple"));
            Assert.That(result.Uasm, Does.Not.Contain("SobakasuTuple"));
        }

        [Test]
        public void Compiler_FlattensPublicTupleStateToLeafSlots()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub state value: ((i32, string), bool);
on start {
  extern UnityEngine.Debug.Log(value.0.0);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export value__0__0"));
            Assert.That(result.Uasm, Does.Contain(".export value__0__1"));
            Assert.That(result.Uasm, Does.Contain(".export value__1"));
            Assert.That(result.HeapPatches, Is.Empty);
        }

        [TestCase("on start { let pair = (1, 2); let value = pair.2; }", "SBK2161")]
        [TestCase("on start { let (left, right) = (1,); }", "SBK2163")]
        [TestCase("on start { let (value,) = 1; }", "SBK2162")]
        [TestCase("fn one() -> (i32,) { 1 } on start {}", "SBK2040")]
        [TestCase("struct Node { next: (Node,), } on start {}", "SBK2105")]
        public void Compiler_ReportsTupleDiagnostics(string source, string expectedCode)
        {
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, expectedCode), Is.True,
                result.ErrorText);
        }

        [Test]
        public void Compiler_CompilesUnitTupleMultipleTupleAndStructEnumVariants()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i64, y: i64, }
enum Event {
  None,
  Key(char),
  Ip(u8, u8, u8, u8),
  At(Point),
  Click { x: i64, y: i64, },
}
fn identity(event: Event) -> Event { event }
on start {
  let none = Event.None;
  let key = Event.Key('A');
  let ip = identity(Event.Ip(127u8, 0u8, 0u8, 1u8));
  let at = Event.At(Point { x: 1i64, y: 2i64, });
  let click = Event.Click { y: 20i64, x: 10i64, };
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Event"));
            Assert.That(result.Uasm, Does.Not.Contain("%Point"));
        }

        [Test]
        public void Compiler_CompilesTupleStructAggregateAndPrimitiveLiteralPatterns()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i32, y: i32, }
enum Event {
  None,
  Ip(u8, u8, u8, u8),
  At(Point),
  Click { x: i32, y: i32, },
}
fn choose_point(value: bool, first: Point, second: Point) -> Point {
  match value { true => first, false => second, }
}
fn event_value(event: Event) -> i32 {
  match event {
    Event.None => 0,
    Event.Ip(a, _, c, d) => if a == c { 1 } else { 2 },
    Event.At(point) => point.x + point.y,
    Event.Click { y, x } => x + y,
  }
}
fn int_value(value: i32) -> string {
  match value { 0 => ""zero"", 42i32 => ""answer"", _ => ""other"", }
}
fn byte_value(value: u8) -> i32 {
  match value { 10u8 => 10, _ => 0, }
}
fn bool_value(value: bool) -> i32 {
  match value { true => 1, false => 0, }
}
fn char_value(value: char) -> i32 {
  match value { 'a' => 1, _ => 0, }
}
fn string_value(value: string) -> i32 {
  match value { ""hello"" => 1, _ => 0, }
}
on start {
  let point = Point { x: 1, y: 2, };
  let selected = choose_point(true, point, Point { x: 3, y: 4, });
  let value = event_value(Event.At(selected));
  let nested = 1 + bool_value(true);
  extern UnityEngine.Debug.Log(value + nested);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Event"));
            Assert.That(result.Uasm, Does.Not.Contain("%Point"));
        }
    }
}
