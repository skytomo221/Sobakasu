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
    public class AggregateGenericTests
    {

        private const string IntArrayConstructor =
            "SystemInt32Array.__ctor__SystemInt32__SystemInt32Array";
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
        public void Parser_ParsesGenericDeclarationsExplicitTypesAndNestedGreaterTokens()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"struct Pair<T, U> { first: T, second: U, }
enum Option<T> { None, Some(T), }
impl<T> Option<T> {}
on start {
  let explicit: Pair<i32, string> = Pair<i32, string> { first: 1, second: ""x"", };
  let nested: Option<Option<i32>> = Option.Some(Option.Some(1));
  let shifted = 8 >> 1;
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var pair = syntax.Members[0] as StructDeclarationSyntax;
            var option = syntax.Members[1] as EnumDeclarationSyntax;
            var impl = syntax.Members[2] as ImplDeclarationSyntax;
            Assert.That(pair.GenericParameters.Parameters.Count, Is.EqualTo(2));
            Assert.That(option.GenericParameters.Parameters.Count, Is.EqualTo(1));
            Assert.That(impl.GenericParameters.Parameters.Count, Is.EqualTo(1));
            Assert.That(impl.TargetType.GetText(), Is.EqualTo("Option<T>"));
        }

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
  let value = Option.Some(100);
  let explicit = Option<i64>.Some(100i64);
  let none: Option<i32> = Option.None;
  let nested = Option.Some(Option.Some(42));
  let named = Event.Named { previous: 1, current: 2, };
  let tuple = Event.Pair(1, 2);
  let values = [Option.Some(1), Option.Some(2)];
  let container = Container { values: [1, 2], };
  let wrapper = Wrapper { value: Wrapper { value: 1, }, };
  accept(Option.None);
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
        public void Compiler_LowersGameObjectFindSafeWrapperThroughUtilitiesIsValid()
        {
            const string findSignature =
                "UnityEngineGameObject.__Find__SystemString__UnityEngineGameObject";
            const string isValidSignature =
                "VRCSDKBaseUtilities.__IsValid__SystemObject__SystemBoolean";
            var result = SobakasuCompiler.CompileToUasm(
                @"use unity.GameObject;
on start {
  let found = GameObject.find(""Sobakasu"");
  let present = match found {
    Maybe.Just(_) => true,
    Maybe.Nothing => false,
  };
  extern UnityEngine.Debug.Log(present);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, findSignature), Is.EqualTo(1));
            Assert.That(CountOccurrences(result.Uasm, isValidSignature), Is.EqualTo(1));
            Assert.That(result.Uasm, Does.Not.Contain("MATCH,"));
        }

        [Test]
        public void Compiler_PreservesRawExternReferenceReturnEscapeHatch()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on start {
  let raw = extern UnityEngine.GameObject.Find(""Sobakasu"");
  extern UnityEngine.Debug.Log(raw);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(
                "UnityEngineGameObject.__Find__SystemString__UnityEngineGameObject"));
        }

        [Test]
        public void Compiler_LeavesAbiNullInInactiveMaybeReferencePayload()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use unity.GameObject;
state target: Maybe<GameObject> = Maybe.Nothing;
on start {
  let present = match target {
    Maybe.Just(_) => true,
    Maybe.Nothing => false,
  };
  extern UnityEngine.Debug.Log(present);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(FindPatch(result.HeapPatches, "__state_0").RuntimeValue,
                Is.EqualTo(0));
            Assert.That(FindPatch(result.HeapPatches, "__state_1"), Is.Null);
            Assert.That(result.Uasm, Does.Contain("__state_1"));
            Assert.That(result.Uasm, Does.Contain("%UnityEngineGameObject, null"));
        }

        [Test]
        public void Binder_InfersGenericArgumentsFromExistingLiteralTypes()
        {
            var (program, diagnostics) = Bind(
                @"enum Option<T> { None, Some(T), }
on start {
  let i32Value = Option.Some(42);
  let i64Value = Option.Some(42i64);
  let f32Value = Option.Some(3.14);
  let f64Value = Option.Some(3.14f64);
  let stringValue = Option.Some(""hello"");
  let boolValue = Option.Some(true);
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
  pub fn get -> T { self.value }
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
  let nested: Option<Option<i32>> = Option.Some(Option.Some(1));
  let shifted = 8 >> 1;
  extern UnityEngine.Debug.Log(shifted);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("op_RightShift"));
        }

        [TestCase("struct Foo<T, T> {} on start {}", "SBK2120")]
        [TestCase("struct Box<T> { value: T, } on start { let x: Box<i32, string> = Box { value: 1, }; }", "SBK2121")]
        [TestCase("struct Box<T> { value: T, } on start { let x = Box<> { value: 1, }; }", "SBK2121")]
        [TestCase("enum Option<T> { None, Some(T), } on start { let x = Option.None; }", "SBK2122")]
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
    }
}
