using System;
using System.Collections.Generic;
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

using static Skytomo221.Sobakasu.Tests.Editor.StateTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class StateLoweringTests
    {

        private readonly List<string> _cleanupAssetPaths = new();

        [TearDown]
        public void TearDown()
        {
            if (_cleanupAssetPaths.Count == 0)
            {
                return;
            }

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
                "SobakasuStateVariableTests",
                _cleanupAssetPaths.Add);
        }

        [Test]
        public void CompileToUasm_DoesNotBindForbiddenPublicInitializer()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "pub state value: i32 = unknown_function(); on start {}");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1), result.ErrorText);
            Assert.That(ContainsCode(result.Diagnostics, "SBK1040"), Is.True,
                result.ErrorText);
        }

        [Test]
        public void IrAndUasm_UseConstantsWithoutCreatingDeclaredStateStorage()
        {
            const string source = @"pub const INITIAL = 20;
state score = INITIAL;
on interact { score = INITIAL + 1; }";
            var (program, diagnostics) = Bind(source +
                "\nimpl i32 { pub fn +(rhs: Self) -> Self = extern self + rhs }");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));

            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);
            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));
            Assert.That(ir.States.Count, Is.EqualTo(1));
            Assert.That(ir.States[0].Name, Is.EqualTo("score"));
            Assert.That(ContainsIrConstant(ir, 20), Is.True);

            var result = SobakasuCompiler.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Not.Contain(".export INITIAL"));
            Assert.That(result.Uasm, Does.Not.Contain(".export score"));
            Assert.That(CountGlobalInitializerPatches(result.HeapPatches), Is.EqualTo(1));
        }

        [Test]
        public void HeapPatches_ExcludeConstantAndEvaluateArrayAndAggregateStateLeaves()
        {
            var constantOnly = SobakasuCompiler.CompileToUasm(
                "pub const VALUE = 20; on interact { extern UnityEngine.Debug.Log(VALUE); }");
            Assert.That(constantOnly.Success, Is.True, constantOnly.ErrorText);
            Assert.That(CountGlobalInitializerPatches(constantOnly.HeapPatches), Is.Zero);

            var array = SobakasuCompiler.CompileToUasm(
                "const ITEM = 2; state values = [ITEM, ITEM + 1]; on start {}");
            Assert.That(array.Success, Is.True, array.ErrorText);
            var arrayPatch = FindStatePatch(array.HeapPatches, "__state_0");
            Assert.That(arrayPatch, Is.Not.Null,
                FormatHeapPatches(array.HeapPatches));
            Assert.That(arrayPatch.RuntimeValue, Is.EqualTo(new[] { 2, 3 }));

            var aggregate = SobakasuCompiler.CompileToUasm(
                @"struct Pair { first: i32, second: i32, }
const ITEM = 2;
state pair = Pair { first: ITEM, second: ITEM + 1, };
on start {}");
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
                @"impl i32 { pub fn +(rhs: Self) -> Self = extern self + rhs }
state count = 0;
fn increment() { count += 1; }
on interact() { increment(); extern UnityEngine.Debug.Log(count); }
on update() { count += 2; extern UnityEngine.Debug.Log(count); }");
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
            const string source = @"pub sync(linear) state value: f32;
on interact() { value += 1.0; extern UnityEngine.Debug.Log(value); }
on update() { extern UnityEngine.Debug.Log(value); }";

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, "value: %SystemSingle"), Is.EqualTo(1));
            Assert.That(result.Uasm, Does.Contain(".export value"));
            Assert.That(result.Uasm, Does.Contain(".sync value, linear"));
            Assert.That(CountOccurrences(result.Uasm, "PUSH, value"), Is.GreaterThanOrEqualTo(3));
            var statePatch = FindStatePatch(result.HeapPatches, "value");
            Assert.That(statePatch, Is.Null, FormatHeapPatches(result.HeapPatches));
        }

        [Test]
        public void CompileToUasm_KeepsPrivateSynchronizedStateOutOfSourcePublicApi()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"sync state private_status = 0;
pub state public_status: i32;
on interact() { private_status = public_status; }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            var privatePatch = FindStatePatch(result.HeapPatches, "__state_");
            Assert.That(privatePatch, Is.Not.Null);
            Assert.That(result.Uasm, Does.Contain($".sync {privatePatch.SymbolName}, none"));
            Assert.That(result.Uasm, Does.Not.Contain($".export {privatePatch.SymbolName}"));
            Assert.That(result.Uasm, Does.Contain(".export public_status"));

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
        }

        [Test]
        public void CompileToUasm_AcceptsRequiredEndToEndForms()
        {
            var sources = new[]
            {
                "state count = 0; on interact() { count += 1; extern UnityEngine.Debug.Log(count); }",
                "pub state enabled: bool; on interact() { enabled = !enabled; }",
                "sync state global_status = 0; on interact() { extern UnityEngine.Debug.Log(global_status); }",
                "pub sync(linear) state synchronized_value: f32; on update() { extern UnityEngine.Debug.Log(synchronized_value); }",
                "state target: Maybe<UnityEngine.GameObject> = Maybe.Nothing; on interact() { let present = match target { Maybe.Just(value) => true, Maybe.Nothing => false, }; extern UnityEngine.Debug.Log(present); }"
            };

            foreach (var source in sources)
            {
                var result = SobakasuCompiler.CompileToUasm(source);
                Assert.That(result.Success, Is.True, result.ErrorText);
            }
        }
    }
}
