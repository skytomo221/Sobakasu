using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuEventCatalogTests
    {
        [Test]
        public void Parser_ParsesNoArgumentEvent()
        {
            var eventDeclaration = ParseSingleEvent(
                @"on Interact() {
  Debug.Log(""Hello"");
}");

            Assert.That(eventDeclaration.Identifier.Text, Is.EqualTo("Interact"));
            Assert.That(eventDeclaration.Parameters, Is.Empty);
            Assert.That(eventDeclaration.ReturnTypeAnnotation, Is.Null);
        }

        [Test]
        public void Parser_ParsesSingleParameterEvent()
        {
            var eventDeclaration = ParseSingleEvent(
                @"on OnPlayerJoined(player: VRCPlayerApi) {
  Debug.Log(""joined"");
}");

            Assert.That(eventDeclaration.Identifier.Text, Is.EqualTo("OnPlayerJoined"));
            Assert.That(eventDeclaration.Parameters.Count, Is.EqualTo(1));
            Assert.That(eventDeclaration.Parameters[0].Identifier.Text, Is.EqualTo("player"));
            Assert.That(eventDeclaration.Parameters[0].Type.GetText(), Is.EqualTo("VRCPlayerApi"));
        }

        [Test]
        public void Parser_ParsesQualifiedParameterType()
        {
            var eventDeclaration = ParseSingleEvent(
                @"on InputMoveHorizontal(value: f32, args: VRC.Udon.Common.UdonInputEventArgs) {
  Debug.Log(""move"");
}");

            Assert.That(eventDeclaration.Parameters.Count, Is.EqualTo(2));
            Assert.That(eventDeclaration.Parameters[0].Type.GetText(), Is.EqualTo("f32"));
            Assert.That(eventDeclaration.Parameters[1].Type.GetText(), Is.EqualTo("VRC.Udon.Common.UdonInputEventArgs"));
        }

        [Test]
        public void Parser_ParsesReturnTypeAndReturnStatement()
        {
            var eventDeclaration = ParseSingleEvent(
                @"on OnOwnershipRequest(requester: VRCPlayerApi, newOwner: VRCPlayerApi): bool {
  return true;
}");

            Assert.That(eventDeclaration.ReturnTypeAnnotation, Is.Not.Null);
            Assert.That(eventDeclaration.ReturnTypeAnnotation.Type.GetText(), Is.EqualTo("bool"));
            Assert.That(eventDeclaration.Body.Statements[0], Is.TypeOf<ReturnStatementSyntax>());
        }

        [Test]
        public void Binder_BindsInteractAsU0Event()
        {
            var program = BindProgram(@"on Interact() {
}");

            Assert.That(program.Events[0].EventSymbol.SourceName, Is.EqualTo("Interact"));
            Assert.That(program.Events[0].EventSymbol.UdonName, Is.EqualTo("_interact"));
            Assert.That(program.Events[0].EventSymbol.ReturnType, Is.EqualTo(TypeSymbol.U0));
        }

        [Test]
        public void Binder_AddsEventParameterToBodyScope()
        {
            var program = BindProgram(@"on OnPlayerJoined(player: VRCPlayerApi) {
  player;
}");

            var statement = program.Events[0].Body.Statements[0] as BoundExpressionStatement;
            Assert.That(statement, Is.Not.Null);
            var nameExpression = statement.Expression as BoundNameExpression;
            Assert.That(nameExpression, Is.Not.Null);
            Assert.That(nameExpression.Symbol, Is.TypeOf<ParameterSymbol>());
            Assert.That(((ParameterSymbol)nameExpression.Symbol).UdonStorageName, Is.EqualTo("onPlayerJoinedPlayer"));
        }

        [TestCase(@"on InputJump(value: bool, args: VRC.Udon.Common.UdonInputEventArgs) {
}")]
        [TestCase(@"on InputMoveHorizontal(value: f32, args: VRC.Udon.Common.UdonInputEventArgs) {
}")]
        [TestCase(@"on MidiNoteOn(channel: i32, number: i32, velocity: i32) {
}")]
        [TestCase(@"on OnOwnershipRequest(requester: VRCPlayerApi, newOwner: VRCPlayerApi): bool {
  return true;
}")]
        public void Binder_BindsSupportedUdonEvents(string source)
        {
            _ = BindProgram(source);
        }

        [TestCase(@"on Intract() {
}", "SBK2031")]
        [TestCase(@"on interact() {
}", "SBK2031")]
        [TestCase(@"on Interact(value: bool) {
}", "SBK2034")]
        [TestCase(@"on InputJump(value: f32, args: VRC.Udon.Common.UdonInputEventArgs) {
}", "SBK2035")]
        [TestCase(@"on OnOwnershipRequest(requester: VRCPlayerApi, newOwner: VRCPlayerApi) {
  return true;
}", "SBK2036")]
        [TestCase(@"on OnOwnershipRequest(requester: VRCPlayerApi, newOwner: VRCPlayerApi): bool {
}", "SBK2038")]
        [TestCase(@"on Interact(): bool {
  return true;
}", "SBK2037")]
        [TestCase(@"on Interact() {
}
on Interact() {
}", "SBK2032")]
        [TestCase(@"on OnTriggerEnter() {
}", "SBK2033")]
        public void Binder_ReportsEventDiagnostics(string source, string expectedCode)
        {
            var binder = CreateBinder(source);

            Assert.That(
                ContainsDiagnosticCode(binder.Diagnostics.Diagnostics, expectedCode),
                Is.True,
                BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void EventCatalog_ContainsSupportedAndPendingEvents()
        {
            Assert.That(EventCatalog.TryGet("Interact", out var interact), Is.True);
            Assert.That(interact.SupportLevel, Is.EqualTo(EventSupportLevel.Supported));

            Assert.That(EventCatalog.TryGet("OnTriggerEnter", out var onTriggerEnter), Is.True);
            Assert.That(onTriggerEnter.SupportLevel, Is.EqualTo(EventSupportLevel.PendingSignature));
        }

        [Test]
        public void CompileToUasm_InteractKeepsExistingExport()
        {
            var result = SobakasuCompiler.CompileToUasm(@"on Interact() {
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export _interact"));
            Assert.That(result.Uasm, Does.Not.Contain("_invalid_event"));
        }

        [Test]
        public void CompileToUasm_StartAndUpdateEmitEntryPoints()
        {
            var result = SobakasuCompiler.CompileToUasm(@"on Start() {
}

on Update() {
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export _start"));
            Assert.That(result.Uasm, Does.Contain(".export _update"));
        }

        [Test]
        public void CompileToUasm_OnPlayerJoinedEmitsParameterSlot()
        {
            var result = SobakasuCompiler.CompileToUasm(@"on OnPlayerJoined(player: VRCPlayerApi) {
  player;
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export _onPlayerJoined"));
            Assert.That(result.Uasm, Does.Contain("onPlayerJoinedPlayer: %VRCSDKBaseVRCPlayerApi, null"));
        }

        [Test]
        public void CompileToUasm_InputJumpEmitsInputParameterSlots()
        {
            var result = SobakasuCompiler.CompileToUasm(@"on InputJump(value: bool, args: VRC.Udon.Common.UdonInputEventArgs) {
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("inputJumpBoolValue: %SystemBoolean, null"));
            Assert.That(result.Uasm, Does.Contain("inputJumpArgs: %VRCUdonCommonUdonInputEventArgs, null"));
        }

        [Test]
        public void CompileToUasm_OnOwnershipRequestEmitsReturnValueSlot()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on OnOwnershipRequest(requester: VRCPlayerApi, newOwner: VRCPlayerApi): bool {
  return true;
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export _onOwnershipRequest"));
            Assert.That(result.Uasm, Does.Contain("__returnValue: %SystemObject, null"));
            Assert.That(result.Uasm, Does.Contain("PUSH, __returnValue"));
            Assert.That(result.Uasm, Does.Contain("COPY"));
        }

        private static EventDeclarationSyntax ParseSingleEvent(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);
            Assert.That(syntax.Members.Count, Is.EqualTo(1));

            var eventDeclaration = syntax.Members[0] as EventDeclarationSyntax;
            Assert.That(eventDeclaration, Is.Not.Null);
            return eventDeclaration;
        }

        private static SobakasuBinder CreateBinder(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);

            var binder = new SobakasuBinder();
            binder.BindProgram(syntax);
            return binder;
        }

        private static BoundProgram BindProgram(string source)
        {
            var binder = CreateBinder(source);
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);

            binder = new SobakasuBinder();
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.HasErrors, Is.False, BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));
            return program;
        }

        private static bool ContainsDiagnosticCode(
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

        private static string BuildDiagnosticMessage(IReadOnlyList<Diagnostic> diagnostics)
        {
            var lines = new List<string>();
            foreach (var diagnostic in diagnostics)
                lines.Add($"{diagnostic.Code}: {diagnostic.Message}");

            return string.Join("\n", lines);
        }
    }
}
