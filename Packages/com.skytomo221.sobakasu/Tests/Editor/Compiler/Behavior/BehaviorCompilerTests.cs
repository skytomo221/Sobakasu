using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

using static Skytomo221.Sobakasu.Tests.Editor.StateCompilerTestSupport;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class BehaviorCompilerTests
    {
        [Test]
        public void Parser_SeparatesStateCapabilityFromRuntimeParameters()
        {
            var parser = new SobakasuParser(SourceText.From(
                "state { count: i32 = 0; } behavior { fn set(state, value: i32) { state.count = value; } }"));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));
            var state = (StateBlockDeclarationSyntax)syntax.Members[0];
            var behavior = (BehaviorDeclarationSyntax)syntax.Members[1];
            var function = (FunctionDeclarationSyntax)behavior.Members[0];
            Assert.That(state.Members.Count, Is.EqualTo(1));
            Assert.That(function.StateCapability, Is.Not.Null);
            Assert.That(function.Parameters.Count, Is.EqualTo(1));
        }

        [Test]
        public void Binder_UsesExistingStateSymbolAndNoRuntimeCapabilityParameter()
        {
            var (program, diagnostics) = Bind(@"
state { count: i32 = 0; }
behavior {
    fn set(state, value: i32) { state.count = value; }
    on interact(state) { set(state, 1); state.count = 2; }
}");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            Assert.That(program.States.Count, Is.EqualTo(1));
            var function = program.Functions.Single(f => f.FunctionSymbol.Name == "set");
            Assert.That(function.FunctionSymbol.RequiresStateCapability, Is.True);
            Assert.That(function.FunctionSymbol.Parameters.Count, Is.EqualTo(1));
        }

        [Test]
        public void Binder_RejectsStateAccessWithoutCapability()
        {
            var (_, diagnostics) = Bind(@"
state { count: i32 = 0; }
behavior { fn reset() { state.count = 0; } }");
            Assert.That(diagnostics.Any(d => d.Code == "SBK2303"), Is.True, Format(diagnostics));
        }

        [Test]
        public void Binder_RejectsFieldWithoutPublic()
        {
            var (_, diagnostics) = Bind("state { target: i32 = field; }");
            Assert.That(diagnostics.Any(d => d.Code == "SBK2301"), Is.True, Format(diagnostics));
        }

        [Test]
        public void Binder_UsesFieldAndPublicSourceInitializer()
        {
            var (program, diagnostics) = Bind("state { pub source: i32 = 10; pub target: i32 = field; }");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            Assert.That(program.States[0].StateSymbol.IsPublic, Is.True);
            Assert.That(program.States[0].StateSymbol.InitialValue, Is.EqualTo(10));
            Assert.That(program.States[1].StateSymbol.IsPublic, Is.True);
            Assert.That(program.States[1].StateSymbol.InitialValue, Is.Null);
            Assert.That(program.States[1].Initializer, Is.Null);
        }

        [Test]
        public void Binder_RejectsMissingInitializerAndFieldType()
        {
            var (_, missingInitializer) = Bind("state { value: i32; }");
            Assert.That(missingInitializer.Any(d => d.Code == "SBK1017"), Is.True, Format(missingInitializer));
            var (_, missingType) = Bind("state { pub target = field; }");
            Assert.That(missingType.Any(d => d.Code == "SBK2302"), Is.True, Format(missingType));
        }

        [Test]
        public void Binder_RejectsMissingOrExtraCapabilityAtCall()
        {
            var (_, diagnostics) = Bind(@"
behavior {
    fn needs(state) {}
    fn plain() {}
    fn caller(state) { needs(); plain(state); }
}");
            Assert.That(diagnostics.Count(d => d.Code == "SBK2305"), Is.EqualTo(2), Format(diagnostics));
        }

        [Test]
        public void Binder_SeparatesReceivePayloadFromCapability()
        {
            var (program, diagnostics) = Bind(@"
state { count: i32 = 0; }
behavior { receive changed(state, value: i32) { state.count = value; } }");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var receiver = program.NetworkReceivers.Single().ReceiveSymbol;
            Assert.That(receiver.RequiresStateCapability, Is.True);
            Assert.That(receiver.Parameters.Count, Is.EqualTo(1));
            Assert.That(receiver.PhysicalParameters.Count, Is.EqualTo(1));
        }

        [Test]
        public void Ir_UsesStateStorageWithoutCapabilityParameter()
        {
            var (program, diagnostics) = Bind(@"
state { count: i32 = 0; }
behavior {
    fn set(state, value: i32) { state.count = value; }
    on interact(state) { set(state, 1); }
}");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);
            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty, Format(lowerer.Diagnostics.Diagnostics));
            Assert.That(ir.States.Count, Is.EqualTo(1));
            Assert.That(ir.Modules.SelectMany(m => m.Blocks).SelectMany(b => b.Instructions)
                .OfType<IrCopyInstruction>().Any(i => i.Target is IrStateStorage), Is.True);
        }

        [Test]
        public void Uasm_ExportsFieldAndOmitsCapabilityStorage()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(@"
state { pub target: i32 = field; count: i32 = 0; }
behavior {
    fn reset(state) { state.count = 0; }
    on interact(state) { reset(state); }
}");
            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export target"));
            Assert.That(result.Uasm, Does.Not.Contain("__state_capability"));
        }

        [Test]
        public void Behavior_SendAndReceiveShareRuntimePayloadOnly()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(@"
state { count: i32 = 0; }
behavior {
    receive changed(state, value: i32) { state.count = value; }
    on interact(state) { send changed(state.count) to others; }
}");
            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Binder_RejectsSendInModuleAndImplBodies()
        {
            var (_, diagnostics) = Bind(@"
struct Counter { value: i32, }
impl Counter { fn send_it(self) { send ping to all; } }
fn send_it() { send ping to all; }
behavior { receive ping() {} }");
            Assert.That(diagnostics.Count(d => d.Code == "SBK2306"), Is.EqualTo(2), Format(diagnostics));
        }

        [Test]
        public void Binder_KeepsBehaviorFunctionsLocalToBehavior()
        {
            var (_, diagnostics) = Bind(@"
fn module_caller() { local(); }
behavior {
    fn local() {}
    fn caller() { local(); module_caller(); }
    on interact() { caller(); }
}");
            Assert.That(diagnostics.Any(d => d.Message.Contains("local")), Is.True, Format(diagnostics));
        }

        [Test]
        public void Binder_RejectsCapabilityOnNonBehaviorCall()
        {
            var (_, diagnostics) = Bind(@"
behavior {
    fn caller(state) { ""text"".length(state); }
}");
            Assert.That(diagnostics.Any(d => d.Code == "SBK2305"), Is.True, Format(diagnostics));
        }

        [Test]
        public void Binder_RejectsDuplicateStateBlocksAndBareStateNames()
        {
            var (_, diagnostics) = Bind(@"
state { count: i32 = 0; }
state { other: i32 = 1; }
behavior { on interact(state) { count = 2; } }");
            Assert.That(diagnostics.Any(d => d.Code == "SBK2300"), Is.True, Format(diagnostics));
            Assert.That(diagnostics.Any(d => d.Code == "SBK2002"), Is.True, Format(diagnostics));
        }

        [Test]
        public void Uasm_DistinguishesBehaviorAndModuleFunctionsWithSameSignature()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(@"
fn same() -> i32 { 1 }
behavior {
    fn same() -> i32 { 2 }
    on interact() { extern UnityEngine.Debug.Log(same()); }
}");
            Assert.That(result.Success, Is.True, result.ErrorText);
        }
    }
}
