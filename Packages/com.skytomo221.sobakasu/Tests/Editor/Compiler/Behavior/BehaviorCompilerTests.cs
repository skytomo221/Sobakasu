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
        public void Compiler_AllowsStateFreeInteractAndRejectsStateAccessWithoutCapability()
        {
            var allowed = SobakasuTestEnvironment.CompileToUasm(@"
behavior {
    function foo() {}
    on interact { behavior::foo(); }
}");
            Assert.That(allowed.Success, Is.True, allowed.ErrorText);

            var rejected = SobakasuTestEnvironment.CompileToUasm(@"
state { count: i32 = 0; }
behavior { on interact { state.count += 1; } }");
            Assert.That(rejected.Success, Is.False);
            Assert.That(rejected.Diagnostics.Any(d => d.Code == "SBK2303"), Is.True, rejected.ErrorText);
        }

        [Test]
        public void Parser_SeparatesStateCapabilityFromRuntimeParameters()
        {
            var parser = new SobakasuParser(SourceText.From(
                "state { count: i32 = 0; } behavior { function set(state, value: i32) { state.count = value; } }"));
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
    function set(state, value: i32) { state.count = value; }
    on interact(state) { state.set(1); state.count = 2; }
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
behavior { function reset() { state.count = 0; } }");
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
            var (program, diagnostics) = Bind("state { public source: i32 = 10; public target: i32 = field; }");
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
            var (_, missingType) = Bind("state { public target = field; }");
            Assert.That(missingType.Any(d => d.Code == "SBK2302"), Is.True, Format(missingType));
        }

        [Test]
        public void Binder_RejectsMissingOrExtraCapabilityAtCall()
        {
            var (_, diagnostics) = Bind(@"
behavior {
    function needs(state) {}
    function plain() {}
    function caller(state) { behavior::needs(); behavior::plain(state); }
}");
            Assert.That(diagnostics.Any(d => d.Code == "SBK2314"), Is.True, Format(diagnostics));
            Assert.That(diagnostics.Any(d => d.Code == "SBK2312"), Is.True, Format(diagnostics));
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
    function set(state, value: i32) { state.count = value; }
    on interact(state) { behavior::set(state, 1); }
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
state { public target: i32 = field; count: i32 = 0; }
behavior {
    function reset(state) { state.count = 0; }
    on interact(state) { behavior::reset(state); }
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
    on interact(state) { send state.changed(state.count) to others; }
}");
            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Binder_RejectsSendInModuleAndImplBodies()
        {
            var (_, diagnostics) = Bind(@"
struct Counter { value: i32, }
implementation Counter { function send_it(self) { send behavior::ping() to all; } }
function send_it() { send behavior::ping() to all; }
behavior { receive ping() {} }");
            Assert.That(diagnostics.Count(d => d.Code == "SBK2306"), Is.EqualTo(2), Format(diagnostics));
        }

        [Test]
        public void Binder_KeepsBehaviorFunctionsLocalToBehavior()
        {
            const string source = @"
function module_caller() {
    local();
}
behavior {
    function local() {
    }

    function caller() {
        behavior::local();
        module_caller();
    }

    on interact(state) {
        behavior::caller();
    }
}";
            var (program, diagnostics) = Bind(source);
            Assert.That(diagnostics, Has.Count.EqualTo(1), Format(diagnostics));
            var visibilityDiagnostics = diagnostics.Where(d => d.Code == "SBK2002").ToArray();
            Assert.That(visibilityDiagnostics, Has.Length.EqualTo(1), Format(diagnostics));
            Assert.That(visibilityDiagnostics[0].Message, Is.EqualTo("Undefined name 'local'."));
            Assert.That(source.Substring(visibilityDiagnostics[0].Span.Start, visibilityDiagnostics[0].Span.Length), Is.EqualTo("local"));

            var behaviorCalls = program.Functions.Single(f => f.FunctionSymbol.Name == "caller").Body.Statements
                .OfType<BoundExpressionStatement>()
                .Select(statement => statement.Expression)
                .OfType<BoundUserFunctionCallExpression>()
                .ToArray();
            Assert.That(behaviorCalls.Select(call => call.Function.Name), Is.EquivalentTo(new[] { "local", "module_caller" }));
            Assert.That(behaviorCalls.Single(call => call.Function.Name == "local").Function.IsBehaviorFunction, Is.True);
            Assert.That(behaviorCalls.Single(call => call.Function.Name == "module_caller").Function.IsBehaviorFunction, Is.False);
            var eventCall = program.Events.Single().Body.Statements
                .OfType<BoundExpressionStatement>()
                .Select(statement => statement.Expression)
                .OfType<BoundUserFunctionCallExpression>()
                .Single();
            Assert.That(eventCall.Function.Name, Is.EqualTo("caller"));
            Assert.That(eventCall.Function.IsBehaviorFunction, Is.True);
        }

        [Test]
        public void Binder_RejectsCapabilityOnNonBehaviorCall()
        {
            var (_, diagnostics) = Bind(@"
behavior {
    function caller(state) { ""text"".length(state); }
}");
            Assert.That(diagnostics.Any(d => d.Code == "SBK2312"), Is.True, Format(diagnostics));
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
function same() -> i32 { 1 }
behavior {
    function same() -> i32 { 2 }
    on interact(state) {
        extern UnityEngine.Debug.Log(same());
        extern UnityEngine.Debug.Log(behavior::same());
    }
}");
            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Binder_ResolvesBareAndBehaviorQualifiedSameNameToDifferentFunctions()
        {
            var (program, diagnostics) = Bind(@"
function same() -> i32 { 1 }
behavior {
    function same() -> i32 { 2 }
    on interact(state) {
        same();
        behavior::same();
    }
}");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));

            var calls = program.Events.Single().Body.Statements
                .OfType<BoundExpressionStatement>()
                .Select(statement => statement.Expression)
                .OfType<BoundUserFunctionCallExpression>()
                .ToArray();
            Assert.That(calls, Has.Length.EqualTo(2));
            Assert.That(calls[0].Function.IsBehaviorFunction, Is.False);
            Assert.That(calls[1].Function.IsBehaviorFunction, Is.True);
            Assert.That(calls[0].Function, Is.Not.SameAs(calls[1].Function));
        }

        [Test]
        public void Binder_ResolvesAllBehaviorReceiverCallFormsToTheirFunctionSymbols()
        {
            var (program, diagnostics) = Bind(@"
function global {
}
state { value: i32 = 0; }
behavior {
    function associated {
    }
    function method(state) { state.value = 1; }
    on interact(state) {
        global;
        global();
        behavior::associated;
        behavior::associated();
        state.method;
        state.method();
        behavior::method(state);
    }
}");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));

            var calls = program.Events.Single().Body.Statements
                .OfType<BoundExpressionStatement>()
                .Select(statement => statement.Expression)
                .OfType<BoundUserFunctionCallExpression>()
                .ToArray();
            Assert.That(calls.Length, Is.EqualTo(7));
            Assert.That(calls[0].Function, Is.SameAs(calls[1].Function));
            Assert.That(calls[2].Function, Is.SameAs(calls[3].Function));
            Assert.That(calls[4].Function, Is.SameAs(calls[5].Function));
            Assert.That(calls[5].Function, Is.SameAs(calls[6].Function));
            Assert.That(calls[4].Arguments, Has.Count.EqualTo(0));
            Assert.That(calls[6].Arguments, Has.Count.EqualTo(0));
        }

        [Test]
        public void Binder_DistinguishesAssociatedAndStateReceiverFunctionsWithSameSignature()
        {
            var (program, diagnostics) = Bind(@"
behavior {
    function run {
    }
    function run(state) {
    }
    on interact(state) {
        behavior::run;
        behavior::run();
        state.run;
        state.run();
        behavior::run(state);
    }
}");

            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var calls = program.Events.Single().Body.Statements
                .OfType<BoundExpressionStatement>()
                .Select(statement => statement.Expression)
                .OfType<BoundUserFunctionCallExpression>()
                .ToArray();
            Assert.That(calls, Has.Length.EqualTo(5));
            Assert.That(calls[0].Function, Is.SameAs(calls[1].Function));
            Assert.That(calls[2].Function, Is.SameAs(calls[3].Function));
            Assert.That(calls[3].Function, Is.SameAs(calls[4].Function));
            Assert.That(calls[0].Function.RequiresStateCapability, Is.False);
            Assert.That(calls[2].Function.RequiresStateCapability, Is.True);
        }

        [Test]
        public void Binder_ExplicitAndDotReceiverCallsPassOnlyRuntimeArguments()
        {
            var (program, diagnostics) = Bind(@"
state { value: i32 = 0; }
behavior {
    function set(state, value: i32) { state.value = value; }
    on interact(state) {
        state.set(1);
        behavior::set(state, 2);
    }
}");

            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var calls = program.Events.Single().Body.Statements
                .OfType<BoundExpressionStatement>()
                .Select(statement => statement.Expression)
                .OfType<BoundUserFunctionCallExpression>()
                .ToArray();
            Assert.That(calls, Has.Length.EqualTo(2));
            Assert.That(calls[0].Function, Is.SameAs(calls[1].Function));
            Assert.That(calls[0].Arguments, Has.Count.EqualTo(1));
            Assert.That(calls[1].Arguments, Has.Count.EqualTo(1));
            Assert.That(calls[0].Arguments[0].Type, Is.EqualTo(TypeSymbol.I32));
            Assert.That(calls[1].Arguments[0].Type, Is.EqualTo(TypeSymbol.I32));
        }

        [Test]
        public void Binder_DistinguishesStateMemberCallFromUnknownStateMember()
        {
            var (_, memberDiagnostics) = Bind(@"
state { value: i32 = 0; }
behavior { on interact(state) { state.value(); } }");
            Assert.That(memberDiagnostics.Any(d => d.Code == "SBK2317" && d.Message == "State member `value` is not callable."), Is.True, Format(memberDiagnostics));
            Assert.That(memberDiagnostics.Any(d => d.Code == "SBK2304"), Is.False, Format(memberDiagnostics));

            var (_, unknownDiagnostics) = Bind(@"
state { value: i32 = 0; }
behavior { on interact(state) { state.missing(); } }");
            Assert.That(unknownDiagnostics.Any(d => d.Code == "SBK2304" && d.Message.Contains("missing")), Is.True, Format(unknownDiagnostics));
            Assert.That(unknownDiagnostics.Any(d => d.Code == "SBK2317"), Is.False, Format(unknownDiagnostics));
        }

        [TestCase("behavior { function associated() {} on interact(state) { associated(); } }", "SBK2002")]
        [TestCase("behavior { function method(state) {} on interact(state) { method(state); } }", "SBK2312")]
        [TestCase("behavior { function associated() {} on interact(state) { state.associated(); } }", "SBK2315")]
        [TestCase("behavior { function method(state) {} on interact(state) { behavior::method(); } }", "SBK2314")]
        [TestCase("behavior { function method(state) {} function caller() { state.method(); } }", "SBK2303")]
        [TestCase("enum Choice { Some(i32), } function test(state) { Choice::Some(state, 123); }", "SBK2312")]
        public void Binder_RejectsInvalidBehaviorReceiverCallForms(string source, string expectedCode)
        {
            var (_, diagnostics) = Bind(source);
            Assert.That(diagnostics.Any(d => d.Code == expectedCode), Is.True, Format(diagnostics));
        }
    }
}
