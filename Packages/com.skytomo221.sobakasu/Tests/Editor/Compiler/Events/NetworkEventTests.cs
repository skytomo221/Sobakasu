using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.Target;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuNetworkEventTests
    {
        [Test]
        public void Lexer_ReservesOnlyReceiveSendAndTo()
        {
            var lexer = new SobakasuLexer(SourceText.From(
                "receive send to all others owner self"));
            var tokens = new List<SyntaxToken>();
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                tokens.Add(token);
            }
            while (token.Kind != SyntaxKind.EndOfFile);

            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.ReceiveKeyword));
            Assert.That(tokens[1].Kind, Is.EqualTo(SyntaxKind.SendKeyword));
            Assert.That(tokens[2].Kind, Is.EqualTo(SyntaxKind.ToKeyword));
            Assert.That(tokens[3].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[4].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[5].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[6].Kind, Is.EqualTo(SyntaxKind.SelfKeyword));
        }

        [Test]
        public void Parser_ParsesReceiverFormsAndSendStatement()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"behavior { receive ping {} }
behavior { receive pong() {} }
behavior { receive value(amount: i32) {} }
behavior { on interact(state) {
  send behavior::ping() to all;
  send behavior::pong() to all;
  send behavior::value(10) to others;
} }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(4));
            var ping = (ReceiveDeclarationSyntax)((BehaviorDeclarationSyntax)syntax.Members[0]).Members[0];
            Assert.That(ping.Parameters, Is.Empty);
            Assert.That(ping.OpenParenToken, Is.Null);
            Assert.That(ping.CloseParenToken, Is.Null);

            var pong = (ReceiveDeclarationSyntax)((BehaviorDeclarationSyntax)syntax.Members[1]).Members[0];
            Assert.That(pong.Parameters, Is.Empty);
            Assert.That(pong.OpenParenToken, Is.Not.Null);
            Assert.That(pong.CloseParenToken, Is.Not.Null);

            var value = (ReceiveDeclarationSyntax)((BehaviorDeclarationSyntax)syntax.Members[2]).Members[0];
            Assert.That(value.Parameters, Has.Count.EqualTo(1));
            var eventBody = (EventDeclarationSyntax)((BehaviorDeclarationSyntax)syntax.Members[3]).Members[0];
            Assert.That(eventBody.Body.Statements, Has.Count.EqualTo(3));

            var firstSend = (SendStatementSyntax)eventBody.Body.Statements[0];
            Assert.That(firstSend.Call.Target, Is.TypeOf<PathExpressionSyntax>());
            Assert.That(firstSend.Call.Arguments, Is.Empty);
            Assert.That(((NameExpressionSyntax)firstSend.Target).Name, Is.EqualTo("all"));

            var parenthesizedSend = (SendStatementSyntax)eventBody.Body.Statements[1];
            Assert.That(((PathExpressionSyntax)parenthesizedSend.Call.Target).MemberName, Is.EqualTo("pong"));
            Assert.That(parenthesizedSend.Call.Arguments, Is.Empty);
            Assert.That(((NameExpressionSyntax)parenthesizedSend.Target).Name,
                Is.EqualTo("all"));

            var argumentSend = (SendStatementSyntax)eventBody.Body.Statements[2];
            Assert.That(((PathExpressionSyntax)argumentSend.Call.Target).MemberName, Is.EqualTo("value"));
            Assert.That(argumentSend.Call.Arguments, Has.Count.EqualTo(1));
            Assert.That(((NameExpressionSyntax)argumentSend.Target).Name,
                Is.EqualTo("others"));
        }

        [Test]
        public void Parser_ParsesPrivateAndPublicReceivers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"behavior { receive private_ping {} }
behavior { public receive public_ping() {} }
behavior { public receive damage(value: i32) {} }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser.Diagnostics.Diagnostics));
            var privatePing = (ReceiveDeclarationSyntax)((BehaviorDeclarationSyntax)syntax.Members[0]).Members[0];
            var publicPing = (ReceiveDeclarationSyntax)((BehaviorDeclarationSyntax)syntax.Members[1]).Members[0];
            var damage = (ReceiveDeclarationSyntax)((BehaviorDeclarationSyntax)syntax.Members[2]).Members[0];
            Assert.That(privatePing.PublicKeyword, Is.Null);
            Assert.That(privatePing.ReceiveKeyword.Kind, Is.EqualTo(SyntaxKind.ReceiveKeyword));
            Assert.That(publicPing.PublicKeyword.Kind, Is.EqualTo(SyntaxKind.PublicKeyword));
            Assert.That(publicPing.ReceiveKeyword.Kind, Is.EqualTo(SyntaxKind.ReceiveKeyword));
            Assert.That(publicPing.Identifier.Text, Is.EqualTo("public_ping"));
            Assert.That(publicPing.Parameters, Is.Empty);
            Assert.That(damage.PublicKeyword.Kind, Is.EqualTo(SyntaxKind.PublicKeyword));
            Assert.That(damage.Parameters, Has.Count.EqualTo(1));
        }

        [TestCase("public behavior { on interact(state) {} }")]
        [TestCase("sync behavior { receive ping {} }")]
        [TestCase("public sync behavior { receive ping {} }")]
        public void Parser_RejectsUnsupportedReceiveAndEventModifiers(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True,
                FormatDiagnostics(parser.Diagnostics.Diagnostics));
        }

        [Test]
        public void Parser_RejectsUnparenthesizedSendArguments()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"behavior { receive damage(value: i32) {} }
behavior { on interact(state) { send damage 10 to all; } }"));

            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(parser.Diagnostics.Diagnostics[0].Code, Is.EqualTo("SBK1057"));
        }

        [Test]
        public void Parser_RejectsUnparenthesizedReceiveParameters()
        {
            var parser = new SobakasuParser(SourceText.From(
                "behavior { receive damage value: i32 {} }"));

            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Not.Empty);
            Assert.That(parser.Diagnostics.Diagnostics[0].Code, Is.EqualTo("SBK1021"));
        }

        [Test]
        public void Binder_BindsQualifiedZeroArgumentSends()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"language item ""network_event_target""
public enum NetTarget = extern VRC.Udon.Common.Interfaces.NetworkEventTarget {
  All = extern All,
}
behavior { receive ping {} }
behavior { on interact(state) {
  send behavior::ping() to all;
  send behavior::ping() to all;
} }"));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser.Diagnostics.Diagnostics));

            var binder = new SobakasuBinder(SobakasuTestEnvironment.Default);
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(binder.Diagnostics.Diagnostics));

            var firstSend = program.Events[0].Body.Statements[0]
                as BoundNetworkSendStatement;
            var secondSend = program.Events[0].Body.Statements[1]
                as BoundNetworkSendStatement;
            Assert.That(firstSend, Is.Not.Null);
            Assert.That(secondSend, Is.Not.Null);
            Assert.That(firstSend.Receiver,
                Is.SameAs(program.NetworkReceivers[0].ReceiveSymbol));
            Assert.That(secondSend.Receiver, Is.SameAs(firstSend.Receiver));
            Assert.That(firstSend.Arguments, Is.Empty);
            Assert.That(secondSend.Arguments, Is.Empty);
        }

        [Test]
        public void Binder_BindsBothStateReceiverFormsWithoutSendingCapability()
        {
            var parser = new SobakasuParser(SourceText.From(@"
language item ""network_event_target""
public enum NetTarget = extern VRC.Udon.Common.Interfaces.NetworkEventTarget {
    All = extern All,
    Others = extern Others,
}
state { count: i32 = 0; }
behavior { receive changed(state, value: i32) { state.count = value; } }
behavior { on interact(state) {
    send state.changed(1) to all;
    send behavior::changed(state, 2) to others;
} }"));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, FormatDiagnostics(parser.Diagnostics.Diagnostics));

            var binder = new SobakasuBinder(SobakasuTestEnvironment.Default);
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty, FormatDiagnostics(binder.Diagnostics.Diagnostics));

            var sends = new[]
            {
                (BoundNetworkSendStatement)program.Events[0].Body.Statements[0],
                (BoundNetworkSendStatement)program.Events[0].Body.Statements[1],
            };
            Assert.That(sends[0].Receiver, Is.SameAs(program.NetworkReceivers[0].ReceiveSymbol));
            Assert.That(sends[1].Receiver, Is.SameAs(sends[0].Receiver));
            Assert.That(sends[0].Arguments, Has.Count.EqualTo(1));
            Assert.That(sends[1].Arguments, Has.Count.EqualTo(1));
            Assert.That(sends[0].Arguments[0].Type, Is.EqualTo(TypeSymbol.I32));
            Assert.That(sends[1].Arguments[0].Type, Is.EqualTo(TypeSymbol.I32));
        }

        [Test]
        public void Binder_TracksReceiveVisibilityAndBindsLocalSends()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"language item ""network_event_target""
public enum NetTarget = extern VRC.Udon.Common.Interfaces.NetworkEventTarget {
  All = extern All,
}
behavior { receive private_ping {} }
behavior { public receive public_ping {} }
behavior { on interact(state) {
  send behavior::private_ping() to all;
  send behavior::public_ping() to all;
} }"));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser.Diagnostics.Diagnostics));

            var binder = new SobakasuBinder(SobakasuTestEnvironment.Default);
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(binder.Diagnostics.Diagnostics));

            var privateReceiver = program.NetworkReceivers[0].ReceiveSymbol;
            var publicReceiver = program.NetworkReceivers[1].ReceiveSymbol;
            Assert.That(privateReceiver.IsPublic, Is.False);
            Assert.That(publicReceiver.IsPublic, Is.True);
            Assert.That(privateReceiver.ExportName, Is.EqualTo("private_ping"));
            Assert.That(publicReceiver.ExportName, Is.EqualTo("public_ping"));
            Assert.That(privateReceiver.PhysicalParameters, Is.Empty);
            Assert.That(publicReceiver.PhysicalParameters, Is.Empty);
            Assert.That(((BoundNetworkSendStatement)program.Events[0].Body.Statements[0]).Receiver,
                Is.SameAs(privateReceiver));
            Assert.That(((BoundNetworkSendStatement)program.Events[0].Body.Statements[1]).Receiver,
                Is.SameAs(publicReceiver));
        }

        [Test]
        public void Compiler_EmitsNetworkEntrypointSendAbiAndMetadata()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"behavior { receive notify(value: i32) { extern UnityEngine.Debug.Log(value); } }
behavior { on interact { send behavior::notify(1) to all; } }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export notify"));
            Assert.That(result.Uasm, Does.Contain(
                "VRCSDK3UdonNetworkCallingNetworkCalling.__SendCustomNetworkEvent__" +
                "VRCUdonCommonInterfacesIUdonEventReceiver_" +
                "VRCUdonCommonInterfacesNetworkEventTarget_SystemString_" +
                "SystemObject__SystemVoid"));
            Assert.That(result.Uasm, Does.Contain(
                "%VRCUdonUdonBehaviour, this"));
            Assert.That(result.Uasm, Does.Not.Contain(
                "%VRCUdonCommonInterfacesIUdonEventReceiver, this"));
            Assert.That(result.NetworkReceivers.Count, Is.EqualTo(1));
            Assert.That(result.NetworkReceivers[0].Name, Is.EqualTo("notify"));
            Assert.That(result.NetworkReceivers[0].Parameters.Count, Is.EqualTo(1));
            Assert.That(result.NetworkReceivers[0].Parameters[0].Type,
                Is.EqualTo(TypeKind.I32));
        }

        [Test]
        public void Compiler_ExportsPrivateAndPublicReceiversWithEquivalentMetadata()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"behavior { receive private_damage(value: i32) {} }
behavior { public receive public_damage(value: i32) {} }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export private_damage"));
            Assert.That(result.Uasm, Does.Contain(".export public_damage"));
            Assert.That(result.NetworkReceivers.Count, Is.EqualTo(2));
            Assert.That(result.NetworkReceivers[0].Name, Is.EqualTo("private_damage"));
            Assert.That(result.NetworkReceivers[1].Name, Is.EqualTo("public_damage"));
            Assert.That(result.NetworkReceivers[0].Parameters, Has.Count.EqualTo(1));
            Assert.That(result.NetworkReceivers[1].Parameters, Has.Count.EqualTo(1));
            Assert.That(result.NetworkReceivers[0].Parameters[0].Type,
                Is.EqualTo(result.NetworkReceivers[1].Parameters[0].Type));
            Assert.That(result.NetworkReceivers[0].Parameters[0].RuntimeTypeName,
                Is.EqualTo(result.NetworkReceivers[1].Parameters[0].RuntimeTypeName));
        }

        [Test]
        public void Compiler_UsesConcreteUdonBehaviourForNetworkSendThisSlot()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"behavior { receive event {
  extern UnityEngine.Debug.Log(""Received event!"");
} }
behavior { on interact(state) {
  send behavior::event() to all;
} }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(
                "%VRCUdonUdonBehaviour, this"));
            Assert.That(result.Uasm, Does.Not.Contain(
                "%VRCUdonCommonInterfacesIUdonEventReceiver, this"));
            Assert.That(result.Uasm, Does.Contain(
                "VRCSDK3UdonNetworkCallingNetworkCalling.__SendCustomNetworkEvent__" +
                "VRCUdonCommonInterfacesIUdonEventReceiver_" +
                "VRCUdonCommonInterfacesNetworkEventTarget_SystemString__SystemVoid"));
        }

        [Test]
        public void Compiler_CompilesQualifiedZeroArgumentSend()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"behavior { receive ping {} }
behavior { on interact { send behavior::ping() to all; } }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.NetworkReceivers[0].Name, Is.EqualTo("ping"));
        }

        [Test]
        public void Compiler_FlattensStructParametersBeforeSelectingAbi()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"struct Position { x: i32, y: f32 }
struct Packet { position: Position, active: bool }
behavior { receive update(packet: Packet) {} }
behavior { on interact(state) {
  send behavior::update(Packet { position: Position { x: 1, y: 2.0f32 }, active: true }) to owner;
} }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.NetworkReceivers[0].Parameters.Count, Is.EqualTo(3));
            Assert.That(result.NetworkReceivers[0].Parameters[0].Type,
                Is.EqualTo(TypeKind.I32));
            Assert.That(result.NetworkReceivers[0].Parameters[1].Type,
                Is.EqualTo(TypeKind.F32));
            Assert.That(result.NetworkReceivers[0].Parameters[2].Type,
                Is.EqualTo(TypeKind.Bool));
            Assert.That(CountOccurrences(result.Uasm, "_SystemObject"),
                Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void Compiler_EvaluatesSendArgumentsThenTargetExactlyOnce()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"function argument -> i32 {
  extern UnityEngine.Debug.Log(""argument"");
  1
}
function target -> NetworkEventTarget {
  extern UnityEngine.Debug.Log(""target"");
  NetworkEventTarget::All
}
behavior { receive value(`item`: i32) {} }
behavior { on interact(state) { send behavior::value(argument()) to target(); } }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            var firstLog = result.Uasm.IndexOf("UnityEngineDebug.__Log", StringComparison.Ordinal);
            var secondLog = result.Uasm.IndexOf(
                "UnityEngineDebug.__Log",
                firstLog + 1,
                StringComparison.Ordinal);
            var thirdLog = result.Uasm.IndexOf(
                "UnityEngineDebug.__Log",
                secondLog + 1,
                StringComparison.Ordinal);
            var send = result.Uasm.IndexOf(
                "VRCSDK3UdonNetworkCallingNetworkCalling.__SendCustomNetworkEvent__",
                StringComparison.Ordinal);

            Assert.That(firstLog, Is.GreaterThanOrEqualTo(0));
            Assert.That(secondLog, Is.GreaterThan(firstLog));
            Assert.That(thirdLog, Is.EqualTo(-1));
            Assert.That(send, Is.GreaterThan(secondLog));
        }

        [TestCase("behavior { receive ping -> i32 {} }", "SBK1028")]
        [TestCase("behavior { public receive ping -> i32 {} }", "SBK1028")]
        [TestCase("behavior { receive ping {} } behavior { receive ping() {} }", "SBK2138")]
        [TestCase("function ping {} behavior { on interact { send behavior::ping() to all; } }", "SBK2142")]
        [TestCase("behavior { on interact { send behavior::missing() to all; } }", "SBK2141")]
        [TestCase("behavior { receive ping(value: i32) {} } behavior { on interact { send behavior::ping() to all; } }", "SBK2143")]
        [TestCase("behavior { receive ping(value: i32) {} } behavior { on interact { send ping() to all; } }", "SBK2318")]
        [TestCase("behavior { receive ping(value: i32) {} } behavior { on interact { send behavior::ping(true) to all; } }", "SBK2144")]
        [TestCase("behavior { receive ping {} } behavior { on interact { send behavior::ping() to 1; } }", "SBK2145")]
        [TestCase("behavior { receive ping {} } behavior { on interact { send ping to all; } }", "SBK1057")]
        [TestCase("behavior { receive changed(state, value: i32) {} } behavior { on start { send state.changed(1) to all; } }", "SBK2303")]
        [TestCase("behavior { receive changed(state, value: i32) {} } behavior { on start { send behavior::changed(state, 1) to all; } }", "SBK2303")]
        [TestCase("behavior { receive ping {} } behavior { on interact(state) { send state.ping() to all; } }", "SBK2320")]
        [TestCase("behavior { receive ping {} } behavior { on interact(state) { send behavior::ping(state) to all; } }", "SBK2320")]
        [TestCase("behavior { receive changed(state, value: i32) {} } behavior { on interact(state) { send behavior::changed(1) to all; } }", "SBK2319")]
        [TestCase("behavior { receive ping {} } behavior { on interact(state) { ping(); } }", "SBK2002")]
        [TestCase("enum Payload { None, Some(i32) } behavior { receive data(value: Payload) {} }", "SBK2147")]
        [TestCase(
            "behavior { receive too_many(a:i32,b:i32,c:i32,d:i32,e:i32,f:i32,g:i32,h:i32,i:i32) {} }",
            "SBK2140")]
        public void Compiler_ReportsNetworkDiagnostics(string source, string code)
        {
            var result = SobakasuTestEnvironment.CompileToUasm(source);

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result, code), Is.True, result.ErrorText);
        }

        private static bool ContainsCode(
            SobakasuCompiler.CompileResult result,
            string code)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.Code == code)
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

        private static string FormatDiagnostics(
            IReadOnlyList<Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic> diagnostics)
        {
            var lines = new List<string>();
            foreach (var diagnostic in diagnostics)
                lines.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", lines);
        }
    }
}
