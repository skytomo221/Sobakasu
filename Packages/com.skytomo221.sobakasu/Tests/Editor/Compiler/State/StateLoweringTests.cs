using System;
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

using static Skytomo221.Sobakasu.Tests.Editor.StateCompilerTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class StateLoweringTests
    {
        [Test]
        public void CompileToUasm_BindsPublicSourceInitializer()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                "state { public value: i32 = 10; } behavior { on start {} }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export value"));
        }

        [Test]
        public void IrAndUasm_UseConstantsWithoutCreatingDeclaredStateStorage()
        {
            const string source = @"public const INITIAL = 20;
state { score = INITIAL; }
behavior { on interact(state) { state.score = INITIAL + 1; } }";
            var (program, diagnostics) = Bind(source +
                "\nimplementation i32 { public function +(self, rhs: Self) -> Self = extern self + rhs }");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));

            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);
            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));
            Assert.That(ir.States.Count, Is.EqualTo(1));
            Assert.That(ir.States[0].Name, Is.EqualTo("score"));
            Assert.That(ContainsIrConstant(ir, 20), Is.True);

            var result = SobakasuTestEnvironment.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain(".export INITIAL"));
            Assert.That(result.Uasm, Does.Not.Contain(".export score"));
            Assert.That(CountGlobalInitializerPatches(result.HeapPatches), Is.EqualTo(1));
        }

        [Test]
        public void HeapPatches_ExcludeConstantAndEvaluateArrayAndAggregateStateLeaves()
        {
            var constantOnly = SobakasuTestEnvironment.CompileToUasm(
                "public const VALUE = 20; behavior { on interact(state) { extern UnityEngine.Debug.Log(VALUE); } }");
            Assert.That(constantOnly.Success, Is.True, constantOnly.ErrorText);
            Assert.That(CountGlobalInitializerPatches(constantOnly.HeapPatches), Is.Zero);

            var array = SobakasuTestEnvironment.CompileToUasm(
                "const ITEM = 2; state { values = [ITEM, ITEM + 1]; } behavior { on start {} }");
            Assert.That(array.Success, Is.True, array.ErrorText);
            var arrayPatch = FindStatePatch(array.HeapPatches, "__state_0");
            Assert.That(arrayPatch, Is.Not.Null,
                FormatHeapPatches(array.HeapPatches));
            AssertRuntimeArray(
                arrayPatch.RuntimeValue,
                "System.Int32[]",
                2, 3);

            var aggregate = SobakasuTestEnvironment.CompileToUasm(
                @"struct Pair { first: i32, second: i32, }
const ITEM = 2;
state { pair = Pair { first: ITEM, second: ITEM + 1, }; }
behavior { on start {} }");
            Assert.That(aggregate.Success, Is.True, aggregate.ErrorText);
            var firstPatch = FindStatePatch(aggregate.HeapPatches, "__state_0");
            var secondPatch = FindStatePatch(aggregate.HeapPatches, "__state_1");
            Assert.That(firstPatch, Is.Not.Null, FormatHeapPatches(aggregate.HeapPatches));
            Assert.That(secondPatch, Is.Not.Null, FormatHeapPatches(aggregate.HeapPatches));
            Assert.That(firstPatch.RuntimeValue,
                Is.EqualTo(2));
            Assert.That(secondPatch.RuntimeValue,
                Is.EqualTo(3));
        }

        [Test]
        public void IrLowerer_UsesStateStorageForLoadsStoresFunctionsAndEvents()
        {
            var (program, diagnostics) = Bind(
                @"implementation i32 { public function +(self, rhs: Self) -> Self = extern self + rhs }
state { count = 0; }
behavior {
  function increment(state) { state.count += 1; }
  on interact(state) { state.increment(); extern UnityEngine.Debug.Log(state.count); }
  on update(state) { state.count += 2; extern UnityEngine.Debug.Log(state.count); }
}");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));

            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);

            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));
            Assert.That(ir.States.Count, Is.EqualTo(1));
            Assert.That(ir.Modules.Count, Is.EqualTo(2));
            Assert.That(CountStateCopies(ir), Is.GreaterThanOrEqualTo(2));
            Assert.That(CountStateValues(ir), Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void CompileToUasm_EmitsOneStateSlotWithPublicAndSyncMetadata()
        {
            const string source = @"state { public sync(linear) value: f32 = field; }
behavior { on interact(state) { state.value += 1.0; extern UnityEngine.Debug.Log(state.value); } }
behavior { on update(state) { extern UnityEngine.Debug.Log(state.value); } }";

            var result = SobakasuTestEnvironment.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, "value: %SystemSingle"), Is.EqualTo(1));
            Assert.That(result.Uasm, Does.Contain(".export value"));
            Assert.That(result.Uasm, Does.Contain(".sync value, linear"));
            Assert.That(CountOccurrences(result.Uasm, "PUSH, value"), Is.GreaterThanOrEqualTo(3));
            var statePatch = FindStatePatch(result.HeapPatches, "value");
            Assert.That(statePatch, Is.Null, FormatHeapPatches(result.HeapPatches));
        }

        [Test]
        public void CompileToUasm_RejectsUnrepresentableQuotedPublicName()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                "state { public `a-b`: string = field; } behavior { on start {} }");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorText,
                Does.Contain("Public Udon symbol 'a-b' cannot be represented"));
        }

        [Test]
        public void CompileToUasm_GivesUserPublicSymbolsPriorityOverInternalSlots()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"state { public __exit_addr: i32 = field;
public __sbk_q_612D62: i32 = field; }
behavior { on start {} }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export __exit_addr"));
            Assert.That(result.Uasm, Does.Contain(".export __sbk_q_612D62"));
            Assert.That(result.Uasm, Does.Contain("__exit_addr_1: %SystemUInt32"));
        }

        [Test]
        public void CompileToUasm_AcceptsRequiredEndToEndForms()
        {
            var sources = new[]
            {
                "state { count = 0; } behavior { on interact(state) { state.count += 1; extern UnityEngine.Debug.Log(state.count); } }",
                "state { public enabled: bool = field; } behavior { on interact(state) { state.enabled = !state.enabled; } }",
                "state { sync global_status = 0; } behavior { on interact(state) { extern UnityEngine.Debug.Log(state.global_status); } }",
                "state { public sync(linear) synchronized_value: f32 = field; } behavior { on update(state) { extern UnityEngine.Debug.Log(state.synchronized_value); } }",
                "state { target: Maybe<UnityEngine::GameObject> = Maybe::Nothing; } behavior { on interact(state) { let present = match state.target { Maybe::Just(value) => true, Maybe::Nothing => false, }; extern UnityEngine.Debug.Log(present); } }"
            };

            foreach (var source in sources)
            {
                var result = SobakasuTestEnvironment.CompileToUasm(source);
                Assert.That(result.Success, Is.True, result.ErrorText);
            }
        }
    }
}
