using System;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using static Skytomo221.Sobakasu.Tests.Editor.AggregateTestSupport;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class MutableSelfTests
    {
        private const string Counter = @"implementation i32 {
  function +(self, other: i32) -> i32 = extern self + other
}
struct Foo { value: i32, }
implementation Foo {
  function read(self) -> i32 { self.value }
  function change(mutable self) { self.value = 7; }
  function add(mutable self) { self.value += 1; }
  function result(mutable self) -> i32 { self.value = 9; return self.value; }
  function early(mutable self, stop: bool) {
    self.value = 3;
    if stop { return; }
    self.value = 4;
  }
}";

        [Test]
        public void Parser_PreservesReceiverMutability()
        {
            var parser = new SobakasuParser(SourceText.From(
                "implementation Foo { function a(self) {} function b(mutable self) {} }"));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.HasErrors, Is.False);
            var methods = ((ImplementationDeclarationSyntax)syntax.Members[0]).Methods;
            Assert.That(((SelfParameterSyntax)methods[0].Parameters[0]).IsMutable, Is.False);
            var receiver = (SelfParameterSyntax)methods[1].Parameters[0];
            Assert.That(receiver.IsMutable, Is.True);
            Assert.That(receiver.MutableKeyword, Is.Not.Null);
        }

        [TestCase("implementation Foo { function f(value: i32, mutable self) {} }")]
        [TestCase("implementation Foo { function f(mutable self: Foo) {} }")]
        [TestCase("implementation Foo { function f(self, mutable self) {} }")]
        [TestCase("function f(mutable value: i32) {}")]
        [TestCase("function f(mutable self) {}")]
        [TestCase("behavior { function f(mutable self) {} }")]
        [TestCase("behavior { receive f(mutable self) {} }")]
        public void RejectsInvalidReceiverSyntax(string source)
        {
            var result = Bind(source);
            Assert.That(result.Diagnostics.Any(), Is.True);
        }

        [Test]
        public void Symbols_PreserveContractsIncludingConstructedGenericMethods()
        {
            var (program, diagnostics) = Bind(Counter + @"
struct Box<T> { value: T, }
implementation<T> Box<T> {
  function replace(mutable self, value: T) { self.value = value; }
  function create(value: T) -> Box<T> { Box { value: value, } }
}
behavior { on start {
  let mutable box = Box { value: 1, };
  box.replace(2);
} }");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var functions = program.Functions.Select(f => f.FunctionSymbol).ToArray();
            Assert.That(functions.Single(f => f.Name == "read").IsMutableReceiver, Is.False);
            Assert.That(functions.Single(f => f.Name == "change").IsMutableReceiver, Is.True);
            Assert.That(functions.Where(f => f.Name == "replace"), Is.Not.Empty);
            Assert.That(functions.Where(f => f.Name == "replace").All(f => f.IsMutableReceiver), Is.True);
            Assert.That(functions.Where(f => f.IsAssociatedFunction).All(f => !f.IsMutableReceiver), Is.True);
        }

        [TestCase("self.value = 1;")]
        [TestCase("self.value += 1;")]
        [TestCase("self = Foo { value: 1, };")]
        [TestCase("self.change();")]
        [TestCase("self.change;")]
        public void Body_RequiresMutableSelf(string body)
        {
            var source = Counter + " implementation Foo { function outer(RECEIVER) { " + body + " } }";
            Assert.That(Bind(source.Replace("RECEIVER", "self")).Diagnostics.Any(d => d.Code == "SBK2170"), Is.True);
            var accepted = Bind(source.Replace("RECEIVER", "mutable self"));
            Assert.That(accepted.Diagnostics, Is.Empty, Format(accepted.Diagnostics));
        }

        [Test]
        public void Body_TracksNestedSelfFields()
        {
            const string source = @"struct Inner { value: i32, }
struct Outer { inner: Inner, }
implementation Outer { function update(RECEIVER) { self.inner.value = 1; } }";
            Assert.That(Bind(source.Replace("RECEIVER", "self")).Diagnostics.Any(d => d.Code == "SBK2170"), Is.True);
            Assert.That(Bind(source.Replace("RECEIVER", "mutable self")).Diagnostics, Is.Empty);
        }

        [TestCase("let x = Foo { value: 0, }; x.change();")]
        [TestCase("let x = Foo { value: 0, }; x.change;")]
        [TestCase("Foo { value: 0, }.change();")]
        [TestCase("make().change();")]
        public void Call_RejectsImmutableOrTemporaryReceiver(string body)
        {
            var result = Bind(Counter + " function make() -> Foo { Foo { value: 0, } } behavior { on start { " + body + " } }");
            Assert.That(result.Diagnostics.Any(d => d.Code == "SBK2170"), Is.True, Format(result.Diagnostics));
        }

        [Test]
        public void Call_PlainSelfAllowsImmutableReceiverAndOrdinaryParametersStayImmutable()
        {
            var accepted = Bind(Counter + " behavior { on start { let x = Foo { value: 0, }; x.read(); } }");
            Assert.That(accepted.Diagnostics, Is.Empty, Format(accepted.Diagnostics));
            var rejected = Bind(Counter + " function f(x: Foo) { x.value = 1; }");
            Assert.That(rejected.Diagnostics, Is.Not.Empty);
            Assert.That(Bind("function f(x: i32) { x = 1; }").Diagnostics, Is.Not.Empty);
        }

        [Test]
        public void ReceiverMutabilityDoesNotDistinguishOverloads()
        {
            var result = Bind("struct Foo {} implementation Foo { function f(self) {} function f(mutable self) {} }");
            Assert.That(result.Diagnostics, Is.Not.Empty);
        }

        [Test]
        public void Operators_RespectTheSameReceiverContract()
        {
            const string source = @"implementation i32 {
  function +(mutable self, other: i32) -> i32 { self = other; other }
}
behavior { on start { let MUTABILITY value = 1; let result = value + 2; } }";
            var rejected = Bind(source.Replace("MUTABILITY", ""));
            Assert.That(rejected.Diagnostics.Any(d => d.Code == "SBK2170"), Is.True);
            var accepted = Lower(source.Replace("MUTABILITY", "mutable"));
            var writes = accepted.Modules[0].Blocks.SelectMany(b => b.Instructions).OfType<IrCopyInstruction>();
            Assert.That(writes.Count(c => c.Target is IrLocalStorage local && local.Variable.Name == "value"), Is.EqualTo(2));
        }

        private static IrProgram Lower(string source)
        {
            var (program, diagnostics) = Bind(source);
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);
            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty, Format(lowerer.Diagnostics.Diagnostics));
            return ir;
        }

        [TestCase("change()")]
        [TestCase("result()")]
        [TestCase("early(true)")]
        [TestCase("add()")]
        public void Lowering_WritesSelfBackAtNormalEnd(string invocation)
        {
            var ir = Lower(Counter + @"behavior { on start {
  let mutable x = Foo { value: 0, };
  x." + invocation + @";
} }");
            var blocks = ir.Modules[0].Blocks;
            var copies = blocks.SelectMany(b => b.Instructions).OfType<IrCopyInstruction>().ToArray();
            var writeBack = blocks.Last(b => b.Label.Contains("fn_end"))
                .Instructions.OfType<IrCopyInstruction>().Last();
            var writes = copies.Where(c => ReferenceEquals(c.Target, writeBack.Target)).ToArray();
            Assert.That(writes.Length, Is.EqualTo(2), "Initialization and receiver write-back");
            Assert.That(copies.Any(c => ReferenceEquals(c.Source, writeBack.Target)), Is.True,
                "Receiver is read from the same storage that receives write-back");
            Assert.That(writeBack, Is.SameAs(writes[1]));
            Assert.That(writes[1].Source, Is.TypeOf<IrTemporaryStorage>());
        }

        [Test]
        public void Lowering_NestedReceiverAndReturnValueWriteBackToOriginalLeaf()
        {
            var ir = Lower(Counter + @"struct Outer { inner: Foo, untouched: i32, }
state { outer = Outer { inner: Foo { value: 0, }, untouched: 42, }; }
behavior { on start(state) {
  let answer = state.outer.inner.result();
} }");
            var writes = ir.Modules[0].Blocks.SelectMany(b => b.Instructions).OfType<IrCopyInstruction>()
                .Where(c => c.Target is IrStateStorage).ToArray();
            Assert.That(writes.Length, Is.EqualTo(1));
            Assert.That(((IrStateStorage)writes[0].Target).State.Name, Is.EqualTo("outer__inner__value"));
        }

        [Test]
        public void Lowering_NestedMutableLocalPreservesTheOriginalLocation()
        {
            var ir = Lower(Counter + @"struct Outer { inner: Foo, untouched: i32, }
behavior { on start {
  let mutable outer = Outer { inner: Foo { value: 0, }, untouched: 42, };
  let answer = outer.inner.result();
} }");
            var copies = ir.Modules[0].Blocks.SelectMany(b => b.Instructions).OfType<IrCopyInstruction>().ToArray();
            var writeBack = ir.Modules[0].Blocks.Single(b => b.Label.Contains("fn_end"))
                .Instructions.OfType<IrCopyInstruction>().First();
            Assert.That(copies.Count(c => ReferenceEquals(c.Target, writeBack.Target)), Is.EqualTo(2));
            Assert.That(copies.Count(c => ReferenceEquals(c.Source, writeBack.Target)), Is.EqualTo(1));
            Assert.That(copies.Any(c => ReferenceEquals(c.Target, writeBack.Source)), Is.True);
        }

        [Test]
        public void Lowering_StateReceiverUsesStateStorage()
        {
            var ir = Lower(Counter + @"state { counter = Foo { value: 0, }; }
behavior { on start(state) { state.counter.change(); } }");
            Assert.That(StateWriteNames(ir.Modules[0]), Does.Contain("counter__value"));
        }

        [Test]
        public void Lowering_ReplacementAndForwardingPreserveSelfStorage()
        {
            var ir = Lower(Counter + @"implementation Foo {
  function replace(mutable self) { self = Foo { value: 12, }; }
  function outer(mutable self) { self.replace(); }
}
state { counter = Foo { value: 0, }; }
behavior { on start(state) { state.counter.outer(); } }");
            var copies = ir.Modules[0].Blocks.SelectMany(b => b.Instructions).OfType<IrCopyInstruction>().ToArray();
            var writeBack = copies.Single(c => c.Target is IrStateStorage);
            Assert.That(copies.Any(c => ReferenceEquals(c.Target, writeBack.Source)), Is.True);
            Assert.That(ir.Modules[0].Blocks.Count(b => b.Label.Contains("fn_end")), Is.EqualTo(2));
        }

        [Test]
        public void Lowering_NonReturningMethodHasNoWriteBack()
        {
            var ir = Lower(@"struct Foo { value: i32, }
implementation Foo { function forever(mutable self) { loop {} } }
state { counter = Foo { value: 0, }; }
behavior { on start(state) { state.counter.forever(); } }");
            Assert.That(StateWriteNames(ir.Modules[0]), Is.Empty);
            Assert.That(ir.Modules[0].Blocks.Any(b => b.Label.Contains("fn_end")), Is.False);
        }

        [Test]
        public void Lowering_PrimitiveArrayElementUsesCapturedGetterAndSetter()
        {
            var ir = Lower(@"implementation i32 {
  function replace(mutable self) { self = 42; }
}
behavior { on start {
  let values = [1];
  values[0].replace();
} }");
            var calls = ir.Modules[0].Blocks.SelectMany(b => b.Instructions).OfType<IrExternCallInstruction>().ToArray();
            var getter = calls.Single(c => c.ExternSignature.Contains("Array.__Get__"));
            var setter = calls.Last(c => c.ExternSignature.Contains("Array.__Set__"));
            Assert.That(setter.Arguments[0], Is.SameAs(getter.Arguments[0]));
            Assert.That(setter.Arguments[1], Is.SameAs(getter.Arguments[1]));
        }

        [Test]
        public void PlainSelf_ExternReferenceObjectSideEffectsRemainAllowed()
        {
            Lower(@"type Object = extern UnityEngine.GameObject;
implementation Object {
  function set_active(self, active: bool) = extern self.SetActive(active)
}
behavior { on start {
  let value: Object = extern UnityEngine.GameObject.Find(""target"");
  value.set_active(false);
} }");
        }

        [Test]
        public void Lowering_ArrayLocationIsCapturedBeforeArgumentsAndNeverReevaluated()
        {
            var ir = Lower(Counter + @"
function index() -> i32 { extern UnityEngine.Mathf.Abs(0) }
function argument() -> bool { extern UnityEngine.Mathf.Approximately(1.0, 2.0) }
behavior { on start {
  let values = [Foo { value: 0, }];
  values[index()].early(argument());
} }");
            var calls = ir.Modules[0].Blocks.SelectMany(b => b.Instructions).OfType<IrExternCallInstruction>().ToArray();
            var signatures = calls.Select(c => c.ExternSignature).ToArray();
            Assert.That(signatures.Count(s => s.Contains(".__Abs__")), Is.EqualTo(1));
            var index = Array.FindIndex(signatures, s => s.Contains(".__Abs__"));
            var getter = Array.FindIndex(signatures, s => s.Contains("Array.__Get__"));
            var argument = Array.FindIndex(signatures, s => s.Contains(".__Approximately__"));
            var setter = Array.FindLastIndex(signatures, s => s.Contains("Array.__Set__"));
            Assert.That(index, Is.LessThan(getter));
            Assert.That(getter, Is.LessThan(argument));
            Assert.That(argument, Is.LessThan(setter));
            Assert.That(calls[getter].Arguments[1], Is.SameAs(calls[setter].Arguments[1]));
            Assert.That(calls[getter].Arguments[0], Is.SameAs(calls[setter].Arguments[0]));
        }

        [Test]
        public void Lowering_NestedAggregateArrayFieldWritesOnlySelectedLeaves()
        {
            var ir = Lower(Counter + @"struct Outer { inner: Foo, other: bool, }
behavior { on start {
  let values = [Outer { inner: Foo { value: 0, }, other: true, }];
  values[0].inner.change();
} }");
            var calls = ir.Modules[0].Blocks.SelectMany(b => b.Instructions).OfType<IrExternCallInstruction>().ToArray();
            Assert.That(calls.Count(c => c.ExternSignature.Contains("Int32Array.__Get__")), Is.EqualTo(1));
            Assert.That(calls.Count(c => c.ExternSignature.Contains("BooleanArray.__Get__")), Is.Zero);
            Assert.That(calls.Count(c => c.ExternSignature.Contains("Int32Array.__Set__")), Is.EqualTo(2));
            Assert.That(calls.Count(c => c.ExternSignature.Contains("BooleanArray.__Set__")), Is.EqualTo(1));
        }

        [TestCase("self", 1)]
        [TestCase("mutable self", 2)]
        public void ExternBinding_OnlyMutableSelfWritesBack(string receiver, int writes)
        {
            var ir = Lower(@"type Vector = extern UnityEngine.Vector3;
implementation Vector {
  function normalize(" + receiver + @") = extern self.Normalize()
}
behavior { on start {
  let mutable vector: Vector = extern new UnityEngine.Vector3(1.0, 2.0, 3.0);
  vector.normalize();
} }");
            var instructions = ir.Modules[0].Blocks.SelectMany(b => b.Instructions).ToArray();
            Assert.That(instructions.OfType<IrExternCallInstruction>().Any(c => c.ExternSignature.Contains("Normalize")), Is.True);
            Assert.That(instructions.OfType<IrCopyInstruction>().Count(c => c.Target is IrLocalStorage local && local.Variable.Name == "vector"), Is.EqualTo(writes));
            if (receiver == "mutable self")
            {
                var call = instructions.OfType<IrExternCallInstruction>().Single(c => c.ExternSignature.Contains("Normalize"));
                var writeBack = instructions.OfType<IrCopyInstruction>()
                    .Last(c => c.Target is IrLocalStorage local && local.Variable.Name == "vector");
                Assert.That(writeBack.Source, Is.SameAs(call.Arguments[0]));
            }
        }
    }
}
