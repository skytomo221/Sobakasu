using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuNetworkEventTests
    {
        private readonly List<string> _cleanupAssetPaths = new();

        [TearDown]
        public void TearDown()
        {
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
                @"receive ping {}
receive pong() {}
receive value(amount: i32) {
  send ping to all;
  send pong() to all;
  send value(10) to others;
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(3));
            var ping = (ReceiveDeclarationSyntax)syntax.Members[0];
            Assert.That(ping.Parameters, Is.Empty);
            Assert.That(ping.OpenParenToken, Is.Null);
            Assert.That(ping.CloseParenToken, Is.Null);

            var pong = (ReceiveDeclarationSyntax)syntax.Members[1];
            Assert.That(pong.Parameters, Is.Empty);
            Assert.That(pong.OpenParenToken, Is.Not.Null);
            Assert.That(pong.CloseParenToken, Is.Not.Null);

            var value = (ReceiveDeclarationSyntax)syntax.Members[2];
            Assert.That(value.Parameters, Has.Count.EqualTo(1));
            Assert.That(value.Body.Statements, Has.Count.EqualTo(3));

            var bareSend = (SendStatementSyntax)value.Body.Statements[0];
            Assert.That(bareSend.ReceiverName.Text, Is.EqualTo("ping"));
            Assert.That(bareSend.Arguments, Is.Empty);
            Assert.That(bareSend.OpenParenToken, Is.Null);
            Assert.That(bareSend.CloseParenToken, Is.Null);
            Assert.That(((NameExpressionSyntax)bareSend.Target).Name, Is.EqualTo("all"));

            var parenthesizedSend = (SendStatementSyntax)value.Body.Statements[1];
            Assert.That(parenthesizedSend.ReceiverName.Text, Is.EqualTo("pong"));
            Assert.That(parenthesizedSend.Arguments, Is.Empty);
            Assert.That(parenthesizedSend.OpenParenToken, Is.Not.Null);
            Assert.That(parenthesizedSend.CloseParenToken, Is.Not.Null);
            Assert.That(((NameExpressionSyntax)parenthesizedSend.Target).Name,
                Is.EqualTo("all"));

            var argumentSend = (SendStatementSyntax)value.Body.Statements[2];
            Assert.That(argumentSend.ReceiverName.Text, Is.EqualTo("value"));
            Assert.That(argumentSend.Arguments, Has.Count.EqualTo(1));
            Assert.That(argumentSend.OpenParenToken, Is.Not.Null);
            Assert.That(argumentSend.CloseParenToken, Is.Not.Null);
            Assert.That(((NameExpressionSyntax)argumentSend.Target).Name,
                Is.EqualTo("others"));
        }

        [Test]
        public void Parser_RejectsUnparenthesizedSendArguments()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"receive damage(value: i32) {}
on interact { send damage 10 to all; }"));

            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
        }

        [Test]
        public void Parser_RejectsUnparenthesizedReceiveParameters()
        {
            var parser = new SobakasuParser(SourceText.From(
                "receive damage value: i32 {}"));

            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Not.Empty);
            Assert.That(parser.Diagnostics.Diagnostics[0].Code, Is.EqualTo("SBK1021"));
        }

        [Test]
        public void Binder_BindsBareAndParenthesizedZeroArgumentSendsIdentically()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"receive ping {}
on interact {
  send ping to all;
  send ping() to all;
}"));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(parser.Diagnostics.Diagnostics));

            var binder = new SobakasuBinder();
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(binder.Diagnostics.Diagnostics));

            var bareSend = program.Events[0].Body.Statements[0]
                as BoundNetworkSendStatement;
            var parenthesizedSend = program.Events[0].Body.Statements[1]
                as BoundNetworkSendStatement;
            Assert.That(bareSend, Is.Not.Null);
            Assert.That(parenthesizedSend, Is.Not.Null);
            Assert.That(bareSend.Receiver,
                Is.SameAs(program.NetworkReceivers[0].ReceiveSymbol));
            Assert.That(parenthesizedSend.Receiver, Is.SameAs(bareSend.Receiver));
            Assert.That(bareSend.Arguments, Is.Empty);
            Assert.That(parenthesizedSend.Arguments, Is.Empty);
        }

        [Test]
        public void Compiler_EmitsNetworkEntrypointSendAbiAndMetadata()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"receive notify(value: i32) { extern UnityEngine.Debug.Log(value); }
on interact { send notify(1) to all; }");

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
        public void Compiler_UsesConcreteUdonBehaviourForNetworkSendThisSlot()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"receive event {
  extern UnityEngine.Debug.Log(""Received event!"");
}
on interact {
  send event to all;
}");

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
        public void Compiler_BareAndParenthesizedZeroArgumentSendsAreEquivalent()
        {
            var bare = SobakasuCompiler.CompileToUasm(
                @"receive ping {}
on interact { send ping to all; }");
            var parenthesized = SobakasuCompiler.CompileToUasm(
                @"receive ping {}
on interact { send ping() to all; }");

            Assert.That(bare.Success, Is.True, bare.ErrorText);
            Assert.That(parenthesized.Success, Is.True, parenthesized.ErrorText);
            Assert.That(bare.Uasm, Is.EqualTo(parenthesized.Uasm));
            Assert.That(bare.NetworkReceivers[0].Name,
                Is.EqualTo(parenthesized.NetworkReceivers[0].Name));
            Assert.That(bare.NetworkReceivers[0].Parameters,
                Has.Count.EqualTo(parenthesized.NetworkReceivers[0].Parameters.Count));
        }

        [Test]
        public void Compiler_ExposesTypedNetworkEventTargetValues()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"receive ping {}
on interact { send ping() to NetworkEventTarget.All; }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.HeapPatches, Has.Some.Matches<HeapPatchEntry>(patch =>
                patch.RuntimeValue is NetworkEventTarget target &&
                target == NetworkEventTarget.All));
        }

        [Test]
        public void Compiler_FlattensStructParametersBeforeSelectingAbi()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"struct Position { x: i32, y: f32 }
struct Packet { position: Position, active: bool }
receive update(packet: Packet) {}
on interact {
  send update(Packet { position: Position { x: 1, y: 2.0f32 }, active: true }) to owner;
}");

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
            var result = SobakasuCompiler.CompileToUasm(
                @"fn argument -> i32 {
  extern UnityEngine.Debug.Log(""argument"");
  1
}
fn target -> NetworkEventTarget {
  extern UnityEngine.Debug.Log(""target"");
  NetworkEventTarget.All
}
receive value(item: i32) {}
on interact { send value(argument()) to target(); }");

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

        [TestCase("receive ping -> i32 {}", "SBK1028")]
        [TestCase("receive ping {} receive ping() {}", "SBK2138")]
        [TestCase("fn ping {} on interact { send ping() to all; }", "SBK2142")]
        [TestCase("on interact { send missing() to all; }", "SBK2141")]
        [TestCase("receive ping(value: i32) {} on interact { send ping() to all; }", "SBK2143")]
        [TestCase("receive ping(value: i32) {} on interact { send ping to all; }", "SBK2143")]
        [TestCase("receive ping(value: i32) {} on interact { send ping(true) to all; }", "SBK2144")]
        [TestCase("receive ping {} on interact { send ping() to 1; }", "SBK2145")]
        [TestCase("receive ping {} on interact { ping(); }", "SBK2002")]
        [TestCase("enum Payload { None, Some(i32) } receive data(value: Payload) {}", "SBK2147")]
        [TestCase(
            "receive too_many(a:i32,b:i32,c:i32,d:i32,e:i32,f:i32,g:i32,h:i32,i:i32) {}",
            "SBK2140")]
        public void Compiler_ReportsNetworkDiagnostics(string source, string code)
        {
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result, code), Is.True, result.ErrorText);
        }

        [Test]
        public void ProgramAsset_PreservesNetworkMetadataAcrossRefresh()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"receive notify(value: i32) {}
on interact { send notify(1) to self; }");
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(
                result.Uasm,
                result.NetworkReceivers,
                out var assemblyError), Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(
                result.HeapPatches,
                out var patchError), Is.True, patchError);
            Assert.That(asset.CommitProgram(
                result.HeapPatches,
                out var commitError), Is.True, commitError);

            RegisterForCleanup(AssetDatabase.GetAssetPath(asset.SerializedProgramAsset));
            AssertNetworkMetadata(asset.SerializedProgramAsset.GetNetworkCallingMetadata());

            asset.RefreshProgram();

            Assert.That(asset.GetRealProgram(), Is.Not.Null);
            AssertNetworkMetadata(asset.SerializedProgramAsset.GetNetworkCallingMetadata());
        }

        private SobakasuProgramAsset CreateProgramAsset()
        {
            var folderGuid = AssetDatabase.CreateFolder(
                "Assets",
                $"SobakasuNetworkEventTests_{Guid.NewGuid():N}");
            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            RegisterForCleanup(folderPath);

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folderPath}/SobakasuProgramAsset.asset");
            var asset = ScriptableObject.CreateInstance<SobakasuProgramAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            RegisterForCleanup(assetPath);
            return AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
        }

        private static void AssertNetworkMetadata(
            NetworkCallingEntrypointMetadata[] metadata)
        {
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata, Has.Length.EqualTo(1));
            Assert.That(metadata[0].Name, Is.EqualTo("notify"));
            Assert.That(metadata[0].MaxEventsPerSecond, Is.EqualTo(5));
            Assert.That(metadata[0].Parameters, Has.Length.EqualTo(1));
            Assert.That(metadata[0].Parameters[0].Name,
                Does.StartWith("__receive_param_"));
            Assert.That(metadata[0].Parameters[0].Type.ToString(),
                Is.EqualTo("UdonInt"));
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

        private void RegisterForCleanup(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
                _cleanupAssetPaths.Add(assetPath);
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
