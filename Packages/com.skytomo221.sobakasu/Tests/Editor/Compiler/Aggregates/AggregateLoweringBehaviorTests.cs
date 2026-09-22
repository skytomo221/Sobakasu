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
    public class AggregateLoweringBehaviorTests : AggregateTestFixture
    {


        [Test]
        public void Compiler_EvaluatesAggregateInitializersOnceInSourceOrder()
        {
            const string firstSignature =
                "UnityEngineMathf.__Abs__SystemInt32__SystemInt32";
            const string secondSignature =
                "UnityEngineMathf.__Clamp__SystemInt32_SystemInt32_SystemInt32__SystemInt32";
            const string source = @"struct Point { x: i32, y: i32, }
enum Event { Click { x: i32, y: i32, }, }
fn first() -> i32 { extern UnityEngine.Mathf.Abs(-1) }
fn second() -> i32 { extern UnityEngine.Mathf.Clamp(2, 0, 10) }
on start {
  let point = Point { y: first(), x: second(), };
  let event = Event::Click { y: first(), x: second(), };
}";
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, firstSignature), Is.EqualTo(2));
            Assert.That(CountOccurrences(result.Uasm, secondSignature), Is.EqualTo(2));
            var (program, diagnostics) = Bind(source +
                "\nimpl i32 { pub fn @-(self) -> Self = extern -self }");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);
            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));

            // Inlined operators create blocks whose textual order can differ
            // from execution order. Follow the jumps through the initializer.
            var blocks = ir.Modules[0].Blocks.ToDictionary(block => block.Label);
            var current = ir.Modules[0].Blocks[0];
            var visited = new HashSet<string>();
            var calls = new List<string>();
            while (current != null)
            {
                Assert.That(visited.Add(current.Label), Is.True);
                foreach (var instruction in current.Instructions)
                {
                    if (instruction is IrExternCallInstruction call &&
                        (call.ExternSignature == firstSignature || call.ExternSignature == secondSignature))
                        calls.Add(call.ExternSignature);
                }
                current = current.Terminator is IrJumpTerminator jump
                    ? blocks[jump.TargetLabel]
                    : null;
            }
            Assert.That(calls, Is.EqualTo(new[]
            {
                firstSignature, secondSignature, firstSignature, secondSignature
            }));
        }

        [Test]
        public void IrLowerer_CopiesStructVariantPayloadFieldsIntoBindings()
        {
            var (program, diagnostics) = Bind(
                @"impl i32 { pub fn +(self, rhs: Self) -> Self = extern self + rhs }
enum Event { Click { x: i32, y: i32, }, }
on start {
  let event = Event::Click { x: 1, y: 2, };
  let result = match event {
    Event::Click { y, x } => x + y,
  };
  extern UnityEngine.Debug.Log(result);
}");
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


    }
}
