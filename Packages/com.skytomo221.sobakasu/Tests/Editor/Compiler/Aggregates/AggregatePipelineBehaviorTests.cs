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
    public class AggregatePipelineBehaviorTests : AggregateTestFixture
    {


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
        public void Compiler_LowersGameObjectFindSafeWrapperThroughUtilitiesIsValid()
        {
            const string findSignature =
                "UnityEngineGameObject.__Find__SystemString__UnityEngineGameObject";
            const string isValidSignature =
                "VRCSDKBaseUtilities.__IsValid__SystemObject__SystemBoolean";
            var result = SobakasuCompiler.CompileToUasm(
                @"use unity::GameObject;
on start {
  let found = GameObject::find(""Sobakasu"");
  let present = match found {
    Maybe::Just(_) => true,
    Maybe::Nothing => false,
  };
  extern UnityEngine.Debug.Log(present);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, findSignature), Is.EqualTo(1));
            Assert.That(CountOccurrences(result.Uasm, isValidSignature), Is.EqualTo(1));
            Assert.That(result.Uasm, Does.Not.Contain("MATCH,"));
        }

        [Test]
        public void Compiler_CompilesStructFieldsCopiesFunctionsAndImplMethods()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i32, y: i32, }
impl Point {
  fn sum(self) -> i32 { self.x + self.y }
}
fn moved(point: Point) -> Point {
  Point { x: point.x + 1, y: point.y + 1, }
}
on interact {
  let point = Point { x: 10, y: 20, };
  let mut copy = moved(point);
  copy.x = 30;
  extern UnityEngine.Debug.Log(copy.sum());
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Point"));
            Assert.That(CountOccurrences(result.Uasm, "COPY"), Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void Compiler_ImportsAndReExportsPublicAggregateTypes()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-aggregate-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "api"));
            try
            {
                File.WriteAllText(
                    Path.Combine(root, "api.sobakasu"),
                    "pub mod model; pub use model::Point;");
                File.WriteAllText(
                    Path.Combine(root, "api", "model.sobakasu"),
                    "pub struct Point { x: i32, y: i32, }");

                var result = SobakasuCompiler.CompileToUasm(
                    @"use api::Point;
on start {
  let point = Point { x: 1, y: 2, };
  extern UnityEngine.Debug.Log(point.x);
}",
                    root);

                Assert.That(result.Success, Is.True, result.ErrorText);
                Assert.That(result.Uasm, Does.Not.Contain("%Point"));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
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
  let none = Event::None;
  let key = Event::Key('A');
  let ip = identity(Event::Ip(127u8, 0u8, 0u8, 1u8));
  let at = Event::At(Point { x: 1i64, y: 2i64, });
  let click = Event::Click { y: 20i64, x: 10i64, };
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
    Event::None => 0,
    Event::Ip(a, _, c, d) => if a == c { 1 } else { 2 },
    Event::At(point) => point.x + point.y,
    Event::Click { y, x } => x + y,
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
  let value = event_value(Event::At(selected));
  let nested = 1 + bool_value(true);
  extern UnityEngine.Debug.Log(value + nested);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Event"));
            Assert.That(result.Uasm, Does.Not.Contain("%Point"));
        }


    }
}
