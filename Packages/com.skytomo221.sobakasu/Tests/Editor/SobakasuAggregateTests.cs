using System;
using System.Collections.Generic;
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

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuAggregateTests
    {
        private const string IntArrayConstructor =
            "SystemInt32Array.__ctor__SystemInt32__SystemInt32Array";
        private const string IntArrayGetter =
            "SystemInt32Array.__Get__SystemInt32__SystemInt32";
        private const string IntArraySetter =
            "SystemInt32Array.__Set__SystemInt32_SystemInt32__SystemVoid";
        private const string BoolArrayConstructor =
            "SystemBooleanArray.__ctor__SystemInt32__SystemBooleanArray";
        private const string BoolArrayGetter =
            "SystemBooleanArray.__Get__SystemInt32__SystemBoolean";
        private const string BoolArraySetter =
            "SystemBooleanArray.__Set__SystemInt32_SystemBoolean__SystemVoid";

        private readonly List<string> _cleanupAssetPaths = new();

        [TearDown]
        public void TearDown()
        {
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
        public void Lexer_RecognizesStructAndEnumKeywords()
        {
            var tokens = LexAll("struct Point {} enum State {}");

            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.StructKeyword));
            Assert.That(tokens[4].Kind, Is.EqualTo(SyntaxKind.EnumKeyword));
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
}" );

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
pub state target: Maybe<GameObject> = Maybe.Nothing;
on start {
  let present = match target {
    Maybe.Just(_) => true,
    Maybe.Nothing => false,
  };
  extern UnityEngine.Debug.Log(present);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(FindPatch(result.HeapPatches, "target__tag").RuntimeValue,
                Is.EqualTo(0));
            Assert.That(FindPatch(result.HeapPatches, "target__Just__0"), Is.Null);
            Assert.That(result.Uasm, Does.Contain("target__Just__0"));
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
}" );

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
pub sync state status: Status<i32> = Status { value: 1, active: true, };
on start {}" );

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
}" );

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
}" );

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
        public void Parser_ParsesStructsMixedEnumsAndConstructionExpressions()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"struct Point { x: i32, y: i32, }
enum Event {
  None,
  Key(char),
  Pair(i32, string),
  Click { point: Point, button: i32, },
}
on start {
  let point = Point { y: 20, x: 10, };
  let none = Event.None;
  let pair = Event.Pair(1, ""two"");
  let click = Event.Click { button: 1, point: point, };
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members[0], Is.TypeOf<StructDeclarationSyntax>());
            var enumDeclaration = syntax.Members[1] as EnumDeclarationSyntax;
            Assert.That(enumDeclaration, Is.Not.Null);
            Assert.That(enumDeclaration.Variants.Count, Is.EqualTo(4));
            Assert.That(enumDeclaration.Variants[0].VariantKind,
                Is.EqualTo(EnumVariantSyntaxKind.Unit));
            Assert.That(enumDeclaration.Variants[1].TuplePayloadTypes.Count, Is.EqualTo(1));
            Assert.That(enumDeclaration.Variants[2].TuplePayloadTypes.Count, Is.EqualTo(2));
            Assert.That(enumDeclaration.Variants[3].VariantKind,
                Is.EqualTo(EnumVariantSyntaxKind.Struct));
            Assert.That(enumDeclaration.Variants[3].NamedPayloadFields.Count, Is.EqualTo(2));
            Assert.That(syntax.Members[2], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_RecoversFromMalformedAggregateBeforeFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"struct Broken { value i32,
fn after() -> i32 { 1 }
enum AlsoBroken { Pair(i32, bool, }
on start {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members, Has.Some.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members, Has.Some.TypeOf<EventDeclarationSyntax>());
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
}" );

            Assert.That(program, Is.Not.Null);
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var declaration = program.Events[0].Body.Statements[0]
                as BoundVariableDeclarationStatement;
            Assert.That(declaration, Is.Not.Null);
            Assert.That(declaration.Variable.Type.Name, Is.EqualTo("Player"));
            Assert.That(declaration.Initializer, Is.TypeOf<BoundStructConstructionExpression>());
        }

        [Test]
        public void Binder_UsesNominalIdentityForAggregatesAndTheirArrays()
        {
            var firstSymbol = TypeSymbol.CreateAggregate(
                "Point",
                "sample.Point",
                UserAggregateKind.Struct,
                isPublic: true,
                declaringModule: "sample");
            var secondSymbol = TypeSymbol.CreateAggregate(
                "Point",
                "sample.Point",
                UserAggregateKind.Struct,
                isPublic: true,
                declaringModule: "sample");
            Assert.That(firstSymbol, Is.Not.EqualTo(secondSymbol));
            Assert.That(TypeSymbol.Array(firstSymbol),
                Is.Not.EqualTo(TypeSymbol.Array(secondSymbol)));

            var structs = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i32, }
struct OtherPoint { x: i32, }
on start {
  let other = OtherPoint { x: 1, };
  let point: Point = other;
  let others = [OtherPoint { x: 2, }];
  let points: [Point] = others;
}" );
            Assert.That(structs.Success, Is.False);
            Assert.That(ContainsCode(structs.Diagnostics, "SBK2005"), Is.True,
                structs.ErrorText);

            var enums = SobakasuCompiler.CompileToUasm(
                @"enum First { Value(i32), }
enum Second { Value(i32), }
on start {
  let second = Second.Value(1);
  let first: First = second;
}" );
            Assert.That(enums.Success, Is.False);
            Assert.That(ContainsCode(enums.Diagnostics, "SBK2005"), Is.True,
                enums.ErrorText);
        }

        [Test]
        public void Compiler_CompilesStructFieldsCopiesFunctionsAndImplMethods()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i32, y: i32, }
impl Point {
  fn sum() -> i32 { self.x + self.y }
}
fn moved(point: Point) -> Point {
  Point { x: point.x + 1, y: point.y + 1, }
}
on interact {
  let point = Point { x: 10, y: 20, };
  let mut copy = moved(point);
  copy.x = 30;
  extern UnityEngine.Debug.Log(copy.sum());
}" );

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
                    "pub mod model; pub use model.Point;");
                File.WriteAllText(
                    Path.Combine(root, "api", "model.sobakasu"),
                    "pub struct Point { x: i32, y: i32, }");

                var result = SobakasuCompiler.CompileToUasm(
                    @"use api.Point;
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
  let none = Event.None;
  let key = Event.Key('A');
  let ip = identity(Event.Ip(127u8, 0u8, 0u8, 1u8));
  let at = Event.At(Point { x: 1i64, y: 2i64, });
  let click = Event.Click { y: 20i64, x: 10i64, };
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Event"));
            Assert.That(result.Uasm, Does.Not.Contain("%Point"));
        }

        [Test]
        public void Compiler_EvaluatesAggregateInitializersOnceInSourceOrder()
        {
            const string firstSignature =
                "UnityEngineMathf.__Abs__SystemInt32__SystemInt32";
            const string secondSignature =
                "UnityEngineMathf.__Clamp__SystemInt32_SystemInt32_SystemInt32__SystemInt32";
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i32, y: i32, }
enum Event { Click { x: i32, y: i32, }, }
fn first() -> i32 { extern UnityEngine.Mathf.Abs(-1) }
fn second() -> i32 { extern UnityEngine.Mathf.Clamp(2, 0, 10) }
on start {
  let point = Point { y: first(), x: second(), };
  let event = Event.Click { y: first(), x: second(), };
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, firstSignature), Is.EqualTo(2));
            Assert.That(CountOccurrences(result.Uasm, secondSignature), Is.EqualTo(2));
            Assert.That(result.Uasm.IndexOf(firstSignature, StringComparison.Ordinal),
                Is.LessThan(result.Uasm.IndexOf(secondSignature, StringComparison.Ordinal)));
            Assert.That(result.Uasm.LastIndexOf(firstSignature, StringComparison.Ordinal),
                Is.LessThan(result.Uasm.LastIndexOf(secondSignature, StringComparison.Ordinal)));
        }

        [Test]
        public void IrLowerer_FlattensNestedStateAndStoresEnumPayloadBeforeTag()
        {
            var (program, diagnostics) = Bind(
                @"struct Point { x: i32, y: i32, }
struct Player { score: i32, position: Point, }
enum Event { None, Click { point: Point, button: i32, }, }
pub state player = Player {
  score: 1,
  position: Point { x: 2, y: 3, },
};
pub state current = Event.None;
on interact {
  current = Event.Click {
    point: Point { x: 10, y: 20, },
    button: 1,
  };
}" );
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));

            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);

            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));
            Assert.That(StateNames(ir), Is.EquivalentTo(new[]
            {
                "player__score",
                "player__position__x",
                "player__position__y",
                "current__tag",
                "current__Click__point__x",
                "current__Click__point__y",
                "current__Click__button"
            }));

            var stateWrites = StateWriteNames(ir.Modules[0]);
            var tagIndex = stateWrites.LastIndexOf("current__tag");
            Assert.That(tagIndex, Is.GreaterThan(0));
            Assert.That(stateWrites.IndexOf("current__Click__point__x"), Is.LessThan(tagIndex));
            Assert.That(stateWrites.IndexOf("current__Click__point__y"), Is.LessThan(tagIndex));
            Assert.That(stateWrites.IndexOf("current__Click__button"), Is.LessThan(tagIndex));
        }

        [Test]
        public void Compiler_LowersAggregateArrayToTypedSoAAndDirectFieldAccess()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Foo { score: i32, finished: bool, }
fn next_index() -> i32 { extern UnityEngine.Mathf.Abs(0) }
fn next_score() -> i32 { extern UnityEngine.Mathf.Clamp(10, 0, 100) }
on start {
  let mut foos = [
    Foo { score: 1, finished: false, },
    Foo { score: 2, finished: true, },
  ];
  foos[next_index()].score += next_score();
  let copy = foos[0];
  foos[1] = copy;
  extern UnityEngine.Debug.Log(foos.length);
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(IntArrayConstructor));
            Assert.That(result.Uasm, Does.Contain(BoolArrayConstructor));
            Assert.That(result.Uasm, Does.Contain(IntArrayGetter));
            Assert.That(result.Uasm, Does.Contain(BoolArrayGetter));
            Assert.That(result.Uasm, Does.Contain(IntArraySetter));
            Assert.That(result.Uasm, Does.Contain(BoolArraySetter));
            Assert.That(result.Uasm, Does.Not.Contain("%Foo"));
            Assert.That(CountOccurrences(
                result.Uasm,
                "UnityEngineMathf.__Abs__SystemInt32__SystemInt32"), Is.EqualTo(1));
            Assert.That(CountOccurrences(
                result.Uasm,
                "UnityEngineMathf.__Clamp__SystemInt32_SystemInt32_SystemInt32__SystemInt32"),
                Is.EqualTo(1));
        }

        [Test]
        public void Compiler_RecursivelyLowersNestedAggregateArrayFields()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: f32, y: f32, }
struct Player { position: Point, score: i32, }
on start {
  let mut players = [Player {
    position: Point { x: 1.0, y: 2.0, },
    score: 3,
  }; 2];
  players[0].position.x = 4.0;
  let position = players[1].position;
  extern UnityEngine.Debug.Log(position.y);
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(
                "SystemSingleArray.__Get__SystemInt32__SystemSingle"));
            Assert.That(result.Uasm, Does.Contain(
                "SystemSingleArray.__Set__SystemInt32_SystemSingle__SystemVoid"));
            Assert.That(result.Uasm, Does.Not.Contain("%Point"));
            Assert.That(result.Uasm, Does.Not.Contain("%Player"));
        }

        [Test]
        public void Compiler_EvaluatesAggregateArrayLengthOnceForAllLeafArrays()
        {
            const string lengthSignature =
                "UnityEngineMathf.__Abs__SystemInt32__SystemInt32";
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Foo { score: i32, finished: bool, }
fn length() -> i32 { extern UnityEngine.Mathf.Abs(2) }
on start { let values = [Foo; length()]; }" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, lengthSignature), Is.EqualTo(1));
            Assert.That(CountOccurrences(result.Uasm, IntArrayConstructor), Is.EqualTo(1));
            Assert.That(CountOccurrences(result.Uasm, BoolArrayConstructor), Is.EqualTo(1));
        }

        [Test]
        public void Compiler_LowersEnumArrayPayloadBeforeTag()
        {
            const string longSetter =
                "SystemInt64Array.__Set__SystemInt32_SystemInt64__SystemVoid";
            var result = SobakasuCompiler.CompileToUasm(
                @"enum Event { None, Click { x: i64, y: i64, }, }
on start {
  let mut events = [Event.None; 2];
  events[0] = Event.Click { x: 10i64, y: 20i64, };
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Event"));
            Assert.That(result.Uasm, Does.Contain(longSetter));
            Assert.That(result.Uasm.LastIndexOf(longSetter, StringComparison.Ordinal),
                Is.LessThan(result.Uasm.LastIndexOf(IntArraySetter, StringComparison.Ordinal)));
        }

        [Test]
        public void Compiler_FlattensPublicSynchronizedStatesAndHeapPatches()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i32, y: i32, }
struct Player { score: i32, position: Point, active: bool, }
enum State { Idle, Count(i32), }
pub sync state player = Player {
  active: true,
  position: Point { y: 3, x: 2, },
  score: 1,
};
state current_state = State.Count(7);
state players = [Player {
  score: 4,
  position: Point { x: 5, y: 6, },
  active: false,
}; 2];
on start {}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export player__score"));
            Assert.That(result.Uasm, Does.Contain(".export player__position__x"));
            Assert.That(result.Uasm, Does.Contain(".export player__position__y"));
            Assert.That(result.Uasm, Does.Contain(".export player__active"));
            Assert.That(result.Uasm, Does.Contain(".sync player__score, none"));
            Assert.That(result.Uasm, Does.Contain(".sync player__active, none"));
            Assert.That(result.HeapPatches.Count, Is.EqualTo(10));
            Assert.That(FindPatch(result.HeapPatches, "player__score").RuntimeValue,
                Is.EqualTo(1));
            Assert.That(FindPatch(result.HeapPatches, "player__position__x").RuntimeValue,
                Is.EqualTo(2));
            Assert.That(FindPatch(result.HeapPatches, "player__active").RuntimeValue,
                Is.EqualTo(true));
        }

        [Test]
        public void RefreshProgram_RestoresFlattenedAggregateInitialValues()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i32, y: i32, }
pub sync state point = Point { x: 10, y: 20, };
on start {}" );
            Assert.That(result.Success, Is.True, result.ErrorText);
            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError),
                Is.True, commitError);

            asset.RefreshProgram();

            AssertHeapValue(asset, "point__x", 10);
            AssertHeapValue(asset, "point__y", 20);
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
        [TestCase("struct A { value: u0, }", "SBK2116")]
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
on start {}" );

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK2118"), Is.True,
                result.ErrorText);
            Assert.That(result.ErrorText, Does.Contain("inner.value"));
        }

        [Test]
        public void Compiler_RejectsImmutableAggregateFieldReplacementButAllowsNestedArrayMutation()
        {
            var rejected = SobakasuCompiler.CompileToUasm(
                @"struct A { values: [i32], }
on start {
  let value = A { values: [1, 2], };
  value.values = [3, 4];
}" );
            Assert.That(rejected.Success, Is.False);
            Assert.That(ContainsCode(rejected.Diagnostics, "SBK2016"), Is.True,
                rejected.ErrorText);

            var accepted = SobakasuCompiler.CompileToUasm(
                @"struct A { values: [i32], }
on start {
  let value = A { values: [1, 2], };
  value.values[0] = 3;
}" );
            Assert.That(accepted.Success, Is.True, accepted.ErrorText);
        }

        [Test]
        public void Lowerer_AvoidsAggregateStateNameCollisions()
        {
            var (program, diagnostics) = Bind(
                @"struct Point { x: i32, }
state foo__x = 1;
state foo = Point { x: 2, };
on start {}" );
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var ir = new SobakasuIrLowerer().Lower(program);

            Assert.That(StateNames(ir), Is.EquivalentTo(new[]
            {
                "foo__x",
                "foo__x__aggregate_1"
            }));
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
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("op_Equality"));
            Assert.That(result.Uasm, Does.Not.Contain("MATCH,"));
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
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Event"));
            Assert.That(result.Uasm, Does.Not.Contain("%Point"));
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
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, signature), Is.EqualTo(1));
        }

        [Test]
        public void IrLowerer_UsesNormalCfgTagComparisonPayloadCopyAndMergeStorage()
        {
            var (program, diagnostics) = Bind(
                @"enum Option { None, Some(i32), }
on start {
  let option = Option.Some(10);
  let result = match option {
    Option.None => 0,
    Option.Some(value) => value,
  };
  extern UnityEngine.Debug.Log(result);
}" );
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);

            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));
            var sawTest = false;
            var sawArm = false;
            var sawMerge = false;
            var sawEquality = false;
            var sawPayloadBinding = false;
            foreach (var block in ir.Modules[0].Blocks)
            {
                sawTest |= block.Label.Contains("match_test");
                sawArm |= block.Label.Contains("match_arm");
                sawMerge |= block.Label.Contains("match_merge");
                foreach (var instruction in block.Instructions)
                {
                    if (instruction is IrExternCallInstruction call &&
                        call.ExternSignature.Contains("op_Equality"))
                    {
                        sawEquality = true;
                    }
                    if (instruction is IrCopyInstruction copy &&
                        copy.Target is IrLocalStorage local &&
                        local.Variable.Name == "value")
                    {
                        sawPayloadBinding = true;
                    }
                }
            }

            Assert.That(sawTest && sawArm && sawMerge, Is.True);
            Assert.That(sawEquality, Is.True);
            Assert.That(sawPayloadBinding, Is.True);
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
}" );
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

        [Test]
        public void IrLowerer_CopiesStructVariantPayloadFieldsIntoBindings()
        {
            var (program, diagnostics) = Bind(
                @"enum Event { Click { x: i32, y: i32, }, }
on start {
  let event = Event.Click { x: 1, y: 2, };
  let result = match event {
    Event.Click { y, x } => x + y,
  };
  extern UnityEngine.Debug.Log(result);
}" );
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);

            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));
            var copiedBindings = new HashSet<string>();
            foreach (var block in ir.Modules[0].Blocks)
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCopyInstruction copy &&
                    copy.Target is IrLocalStorage local &&
                    (local.Variable.Name == "x" || local.Variable.Name == "y"))
                {
                    copiedBindings.Add(local.Variable.Name);
                }
            }

            Assert.That(copiedBindings, Is.EquivalentTo(new[] { "x", "y" }));
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

            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty,
                Format(lexer.Diagnostics.Diagnostics));
            return tokens;
        }

        private static (BoundProgram Program, IReadOnlyList<Diagnostic> Diagnostics) Bind(
            string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            if (parser.Diagnostics.HasErrors)
                return (null, parser.Diagnostics.Diagnostics);

            var binder = new SobakasuBinder();
            var program = binder.BindProgram(syntax);
            return (program, binder.Diagnostics.Diagnostics);
        }

        private static List<string> StateNames(IrProgram program)
        {
            var result = new List<string>();
            foreach (var state in program.States)
                result.Add(state.Name);
            return result;
        }

        private static List<string> StateWriteNames(IrModule module)
        {
            var result = new List<string>();
            foreach (var block in module.Blocks)
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCopyInstruction copy &&
                    copy.Target is IrStateStorage state)
                {
                    result.Add(state.State.Name);
                }
            }
            return result;
        }

        private static HeapPatchEntry FindPatch(
            IReadOnlyList<HeapPatchEntry> patches,
            string symbol)
        {
            foreach (var patch in patches)
            {
                if (patch.SymbolName == symbol)
                    return patch;
            }
            return null;
        }

        private static bool ContainsCode(
            IReadOnlyList<Diagnostic> diagnostics,
            string expectedCode)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == expectedCode)
                    return true;
            }
            return false;
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        private static string Format(IReadOnlyList<Diagnostic> diagnostics)
        {
            var lines = new List<string>();
            foreach (var diagnostic in diagnostics)
                lines.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", lines);
        }

        private SobakasuProgramAsset CreateProgramAsset()
        {
            var folderGuid = AssetDatabase.CreateFolder(
                "Assets",
                $"SobakasuAggregateTests_{Guid.NewGuid():N}");
            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            _cleanupAssetPaths.Add(folderPath);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folderPath}/SobakasuProgramAsset.asset");
            var asset = ScriptableObject.CreateInstance<SobakasuProgramAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            _cleanupAssetPaths.Add(assetPath);
            return AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
        }

        private static void AssertHeapValue(
            SobakasuProgramAsset asset,
            string symbol,
            object expected)
        {
            var program = asset.GetRealProgram();
            var address = program.SymbolTable.GetAddressFromSymbol(symbol);
            Assert.That(program.Heap.GetHeapVariable(address), Is.EqualTo(expected));
        }
    }
}
