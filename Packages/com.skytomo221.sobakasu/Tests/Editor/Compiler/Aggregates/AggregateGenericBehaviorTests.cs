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
    public class AggregateGenericBehaviorTests : AggregateTestFixture
    {


        [Test]
        public void Binder_CanonicalizesConstructedGenericTypesAndSubstitutesNestedArrays()
        {
            var definition = TypeSymbol.CreateAggregate(
                "Container",
                "sample.Container",
                UserAggregateKind.Struct,
                isPublic: true,
                declaringModule: "sample");
            var parameter = TypeSymbol.CreateGenericParameter(
                "T", definition, 0, definition.QualifiedName);
            definition.SetGenericParameters(new[] { parameter });
            definition.SetAggregateFields(new[]
            {
                new AggregateFieldSymbol(
                    "values",
                    definition,
                    TypeSymbol.Array(parameter),
                    0,
                    new TextSpan(0, 1))
            });

            var first = definition.Construct(new[] { TypeSymbol.I32 });
            var second = definition.Construct(new[] { TypeSymbol.I32 });
            var other = definition.Construct(new[] { TypeSymbol.String });

            Assert.That(first, Is.SameAs(second));
            Assert.That(first, Is.Not.EqualTo(
                TypeSymbol.Tuple(new[] { TypeSymbol.String, TypeSymbol.I32 })));
            Assert.That(first, Is.Not.SameAs(other));
            Assert.That(first.AggregateFields[0].Type,
                Is.SameAs(TypeSymbol.Array(TypeSymbol.I32)));

            var otherDefinition = TypeSymbol.CreateAggregate(
                "Other", "sample.Other", UserAggregateKind.Struct, true, "sample");
            var otherParameter = TypeSymbol.CreateGenericParameter(
                "T", otherDefinition, 0, otherDefinition.QualifiedName);
            Assert.That(parameter, Is.Not.EqualTo(otherParameter));
        }

        [Test]
        public void Compiler_InfersAndLowersGenericStructsEnumsNestedValuesAndArrays()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Pair<T, U> { first: T, second: U, }
enum Option<T> { None, Some(T), }
enum Event<T> { Empty, Pair(T, T), Named { current: T, previous: T, }, }
struct Container<T> { values: [T], }
struct Wrapper<T> { value: T, }
impl<T> Option<T> {}
fn accept(value: Option<i32>) {}
on start {
  let pair = Pair { second: ""hello"", first: 42, };
  let value = Option::Some(100);
  let explicit = Option<i64>::Some(100i64);
  let none: Option<i32> = Option::None;
  let nested = Option::Some(Option::Some(42));
  let named = Event::Named { previous: 1, current: 2, };
  let tuple = Event::Pair(1, 2);
  let values = [Option::Some(1), Option::Some(2)];
  let container = Container { values: [1, 2], };
  let wrapper = Wrapper { value: Wrapper { value: 1, }, };
  accept(Option::None);
  extern UnityEngine.Debug.Log(pair.first);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Pair"));
            Assert.That(result.Uasm, Does.Not.Contain("%Option"));
            Assert.That(result.Uasm, Does.Not.Contain("%Container"));
            Assert.That(result.Uasm, Does.Not.Contain("%Wrapper"));
            Assert.That(result.Uasm, Does.Contain(IntArrayConstructor));
        }

        [Test]
        public void Binder_InfersGenericArgumentsFromExistingLiteralTypes()
        {
            var (program, diagnostics) = Bind(
                @"enum Option<T> { None, Some(T), }
on start {
  let i32Value = Option::Some(42);
  let i64Value = Option::Some(42i64);
  let f32Value = Option::Some(3.14);
  let f64Value = Option::Some(3.14f64);
  let stringValue = Option::Some(""hello"");
  let boolValue = Option::Some(true);
}");

            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var expected = new[]
            {
                "Option<i32>",
                "Option<i64>",
                "Option<f32>",
                "Option<f64>",
                "Option<string>",
                "Option<bool>"
            };
            for (var index = 0; index < expected.Length; index++)
            {
                var declaration = program.Events[0].Body.Statements[index]
                    as BoundVariableDeclarationStatement;
                Assert.That(declaration.Variable.Type.Name, Is.EqualTo(expected[index]));
            }
        }

        [Test]
        public void Compiler_AppliesExistingPublicAndSyncRulesToConcreteGenericStateLeaves()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Status<T> { value: T, active: bool, }
pub sync state status: Status<i32>;
on start {}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export status__value"));
            Assert.That(result.Uasm, Does.Contain(".export status__active"));
        }

        [Test]
        public void Compiler_SubstitutesGenericImplReceiverFieldsAndReturnType()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Box<T> { value: T, }
impl<T> Box<T> {
  pub fn get(self) -> T { self.value }
}
on start {
  let box = Box { value: 42, };
  let value: i32 = box.get;
  extern UnityEngine.Debug.Log(value);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Box"));
        }

        [Test]
        public void Compiler_PreservesRightShiftBesideNestedGenericClosures()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"enum Option<T> { None, Some(T), }
on start {
  let nested: Option<Option<i32>> = Option::Some(Option::Some(1));
  let shifted = 8 >> 1;
  extern UnityEngine.Debug.Log(shifted);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("op_RightShift"));
        }

        [TestCase("struct Foo<T, T> {} on start {}", "SBK2120")]
        [TestCase("struct Box<T> { value: T, } on start { let x: Box<i32, string> = Box { value: 1, }; }", "SBK2121")]
        [TestCase("struct Box<T> { value: T, } on start { let x = Box<> { value: 1, }; }", "SBK2121")]
        [TestCase("enum Option<T> { None, Some(T), } on start { let x = Option::None; }", "SBK2122")]
        [TestCase("struct Pair<T> { first: T, second: T, } on start { let x = Pair { first: 1, second: \"x\", }; }", "SBK2123")]
        [TestCase("struct Box<T> { value: T, } on start { let x: Box<UnknownType>; }", "SBK2015")]
        [TestCase("struct Box<T> { value: T, } impl Box<i32> {} on start {}", "SBK2125")]
        [TestCase("struct Node<T> { next: Node<T>, } on start {}", "SBK2105")]
        public void Compiler_ReportsGenericDiagnostics(string source, string expectedCode)
        {
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, expectedCode), Is.True,
                result.ErrorText);
        }


    }
}
