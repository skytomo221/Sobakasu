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
    public class AggregateStorageBehaviorTests : AggregateTestFixture
    {


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
}");
            Assert.That(structs.Success, Is.False);
            Assert.That(ContainsCode(structs.Diagnostics, "SBK2005"), Is.True,
                structs.ErrorText);

            var enums = SobakasuCompiler.CompileToUasm(
                @"enum First { Value(i32), }
enum Second { Value(i32), }
on start {
  let second = Second::Value(1);
  let first: First = second;
}");
            Assert.That(enums.Success, Is.False);
            Assert.That(ContainsCode(enums.Diagnostics, "SBK2005"), Is.True,
                enums.ErrorText);
        }

        [Test]
        public void IrLowerer_FlattensNestedStateAndStoresEnumPayloadBeforeTag()
        {
            var (program, diagnostics) = Bind(
                @"struct Point { x: i32, y: i32, }
struct Player { score: i32, position: Point, }
enum Event { None, Click { point: Point, button: i32, }, }
state player = Player {
  score: 1,
  position: Point { x: 2, y: 3, },
};
state current = Event::None;
on interact {
  current = Event::Click {
    point: Point { x: 10, y: 20, },
    button: 1,
  };
}");
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
}");

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
}");

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
on start { let values = [Foo; length()]; }");

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
  let mut events = [Event::None; 2];
  events[0] = Event::Click { x: 10i64, y: 20i64, };
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain("%Event"));
            Assert.That(result.Uasm, Does.Contain(longSetter));
            Assert.That(result.Uasm.LastIndexOf(longSetter, StringComparison.Ordinal),
                Is.LessThan(result.Uasm.LastIndexOf(IntArraySetter, StringComparison.Ordinal)));
        }

        [Test]
        public void Compiler_FlattensPublicSynchronizedStatesAndPrivateHeapPatches()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i32, y: i32, }
struct Player { score: i32, position: Point, active: bool, }
enum State { Idle, Count(i32), }
pub sync state player: Player;
state initialized_player = Player {
  active: true,
  position: Point { y: 3, x: 2, },
  score: 1,
};
state current_state = State::Count(7);
state players = [Player {
  score: 4,
  position: Point { x: 5, y: 6, },
  active: false,
}; 2];
on start {}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export player__score"));
            Assert.That(result.Uasm, Does.Contain(".export player__position__x"));
            Assert.That(result.Uasm, Does.Contain(".export player__position__y"));
            Assert.That(result.Uasm, Does.Contain(".export player__active"));
            Assert.That(result.Uasm, Does.Contain(".sync player__score, none"));
            Assert.That(result.Uasm, Does.Contain(".sync player__active, none"));
            Assert.That(result.HeapPatches.Count, Is.EqualTo(10));
            Assert.That(FindPatch(result.HeapPatches, "__state_4").RuntimeValue,
                Is.EqualTo(1));
            Assert.That(FindPatch(result.HeapPatches, "__state_5").RuntimeValue,
                Is.EqualTo(2));
            Assert.That(FindPatch(result.HeapPatches, "__state_7").RuntimeValue,
                Is.EqualTo(true));
        }

        [Test]
        public void RefreshProgram_RestoresFlattenedAggregateInitialValues()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Point { x: i32, y: i32, }
sync state point = Point { x: 10, y: 20, };
on start {}");
            Assert.That(result.Success, Is.True, result.ErrorText);
            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError),
                Is.True, commitError);

            asset.RefreshProgram();

            AssertHeapValue(asset, "__state_0", 10);
            AssertHeapValue(asset, "__state_1", 20);
        }

        [Test]
        public void Compiler_RejectsImmutableAggregateFieldReplacementButAllowsNestedArrayMutation()
        {
            var rejected = SobakasuCompiler.CompileToUasm(
                @"struct A { values: [i32], }
on start {
  let value = A { values: [1, 2], };
  value.values = [3, 4];
}");
            Assert.That(rejected.Success, Is.False);
            Assert.That(ContainsCode(rejected.Diagnostics, "SBK2016"), Is.True,
                rejected.ErrorText);

            var accepted = SobakasuCompiler.CompileToUasm(
                @"struct A { values: [i32], }
on start {
  let value = A { values: [1, 2], };
  value.values[0] = 3;
}");
            Assert.That(accepted.Success, Is.True, accepted.ErrorText);
        }

        [Test]
        public void Lowerer_AvoidsAggregateStateNameCollisions()
        {
            var (program, diagnostics) = Bind(
                @"struct Point { x: i32, }
state foo__x = 1;
state foo = Point { x: 2, };
on start {}");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var ir = new SobakasuIrLowerer().Lower(program);

            Assert.That(StateNames(ir), Is.EquivalentTo(new[]
            {
                "foo__x",
                "foo__x__aggregate_1"
            }));
        }

        [Test]
        public void IrLowerer_UsesNormalCfgTagComparisonPayloadCopyAndMergeStorage()
        {
            var (program, diagnostics) = Bind(
                @"enum Option { None, Some(i32), }
on start {
  let option = Option::Some(10);
  let result = match option {
    Option::None => 0,
    Option::Some(value) => value,
  };
  extern UnityEngine.Debug.Log(result);
}");
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


    }
}
