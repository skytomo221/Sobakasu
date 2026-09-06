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
    public class AggregateStateTests
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
        private SobakasuProgramAsset CreateProgramAsset()
        {
            return SobakasuTestAssetFactory.CreateImportedProgramAsset(
                "SobakasuAggregateTests",
                _cleanupAssetPaths.Add);
        }

        [Test]
        public void Compiler_FlattensPublicTupleStateToLeafSlots()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub state value: ((i32, string), bool);
on start {
  extern UnityEngine.Debug.Log(value.0.0);
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export value__0__0"));
            Assert.That(result.Uasm, Does.Contain(".export value__0__1"));
            Assert.That(result.Uasm, Does.Contain(".export value__1"));
            Assert.That(result.HeapPatches, Is.Empty);
        }

        [Test]
        public void Compiler_AppliesExistingPublicAndSyncRulesToConcreteGenericStateLeaves()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Status<T> { value: T, active: bool, }
pub sync state status: Status<i32>;
on start {}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export status__value"));
            Assert.That(result.Uasm, Does.Contain(".export status__active"));
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
state current = Event.None;
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

            AssertHeapValue(asset, "__state_0", 10);
            AssertHeapValue(asset, "__state_1", 20);
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
    }
}
