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
    public class AggregateBindingBehaviorTests : AggregateTestFixture
    {


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
        public void Binder_BindsNominalForwardReferencedNestedAggregates()
        {
            var (program, diagnostics) = Bind(
                @"struct Player { position: Position, score: i32, }
struct Position { x: f32, y: f32, }
struct OtherPosition { x: f32, y: f32, }
on start {
  let player: Player = Player {
    score: 10,
    position: Position { y: 2.0, x: 1.0, },
  };
  extern UnityEngine.Debug.Log(player.position.x);
}");

            Assert.That(program, Is.Not.Null);
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var declaration = program.Events[0].Body.Statements[0]
                as BoundVariableDeclarationStatement;
            Assert.That(declaration, Is.Not.Null);
            Assert.That(declaration.Variable.Type.Name, Is.EqualTo("Player"));
            Assert.That(declaration.Initializer, Is.TypeOf<BoundStructConstructionExpression>());
        }

        [TestCase("struct A { x: i32, x: bool, }", "SBK2102")]
        [TestCase("struct A {} struct A {}", "SBK2101")]
        [TestCase("enum A { X, } enum A { Y, }", "SBK2101")]
        [TestCase("enum A { X, X, }", "SBK2103")]
        [TestCase("enum A { X { value: i32, value: i32, }, }", "SBK2104")]
        [TestCase("struct A { self_value: A, }", "SBK2105")]
        [TestCase("struct A { b: B, } struct B { a: A, }", "SBK2105")]
        [TestCase("struct A { values: [A], }", "SBK2105")]
        [TestCase("enum A { Next(A), }", "SBK2105")]
        [TestCase("struct A { event: B, } enum B { Value(A), }", "SBK2105")]
        [TestCase("struct A { x: i32, } on start { let a = A { y: 1, x: 2, }; }", "SBK2106")]
        [TestCase("struct A { x: i32, y: i32, } on start { let a = A { x: 1, }; }", "SBK2107")]
        [TestCase("struct A { x: i32, } on start { let a = A { x: 1, x: 2, }; }", "SBK2108")]
        [TestCase("struct A { x: i32, } on start { let a = A { x: true, }; }", "SBK2109")]
        [TestCase("on start { let a = i32 {}; }", "SBK2110")]
        [TestCase("enum A { X, } on start { let a = A.Missing; }", "SBK2111")]
        [TestCase("enum A { X { value: i32, }, } on start { let a = A.X; }", "SBK2112")]
        [TestCase("enum A { X, } on start { let a = A.X(1); }", "SBK2113")]
        [TestCase("enum A { X(i32, bool), } on start { let a = A.X(1); }", "SBK2114")]
        [TestCase("enum A { X(i32), } on start { let a = A.X(true); }", "SBK2115")]
        [TestCase("enum A { X { value: i32, }, } on start { let a = A.X { missing: 1, value: 2, }; }", "SBK2106")]
        [TestCase("enum A { X { value: i32, other: bool, }, } on start { let a = A.X { value: 1, }; }", "SBK2107")]
        [TestCase("enum A { X { value: i32, }, } on start { let a = A.X { value: 1, value: 2, }; }", "SBK2108")]
        [TestCase("enum A { X { value: i32, }, } on start { let a = A.X { value: true, }; }", "SBK2109")]
        [TestCase("struct A { value: u0, }", "SBK2015")]
        [TestCase("struct A { values: [i32], } on start { let items = [A { values: [1], }]; }", "SBK2117")]
        [TestCase("struct A { value: object, } sync state value = A { value: 1, };", "SBK2118")]
        [TestCase("struct A { value: i32, } on start { let a = A { value: 1, }; extern UnityEngine.Debug.Log(a); }", "SBK2119")]
        public void Compiler_ReportsAggregateDiagnostics(string source, string expectedCode)
        {
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, expectedCode), Is.True,
                result.ErrorText);
        }

        [Test]
        public void Compiler_ReportsLogicalFieldPathForUnsupportedSyncLeaf()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Inner { value: object, }
struct Outer { inner: Inner, }
sync state outer = Outer {
  inner: Inner { value: 1, },
};
on start {}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK2118"), Is.True,
                result.ErrorText);
            Assert.That(result.ErrorText, Does.Contain("inner.value"));
        }


    }
}
