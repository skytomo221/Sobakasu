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

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuStateVariableTests
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
        public void Lexer_RecognizesStateKeywordsAndKeepsModesContextual()
        {
            var tokens = LexAll("pub sync(none) let mut linear = smooth;");

            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.PubKeyword));
            Assert.That(tokens[1].Kind, Is.EqualTo(SyntaxKind.SyncKeyword));
            Assert.That(tokens[3].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[3].Text, Is.EqualTo("none"));
            Assert.That(tokens[5].Kind, Is.EqualTo(SyntaxKind.LetKeyword));
            Assert.That(tokens[6].Kind, Is.EqualTo(SyntaxKind.MutKeyword));
            Assert.That(tokens[7].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[9].Kind, Is.EqualTo(SyntaxKind.Identifier));
        }

        [Test]
        public void Parser_ParsesPublicSynchronizedStateAndFollowingEvent()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"pub sync(linear) let mut value: f32 = 0.0;
on Interact() { value = 1.0; }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members.Count, Is.EqualTo(2));
            var state = syntax.Members[0] as StateDeclarationSyntax;
            Assert.That(state, Is.Not.Null);
            Assert.That(state.PubKeyword, Is.Not.Null);
            Assert.That(state.MutKeyword, Is.Not.Null);
            Assert.That(state.Identifier.Text, Is.EqualTo("value"));
            Assert.That(state.SynchronizationModifier.Mode,
                Is.EqualTo(SynchronizationModeSyntaxKind.Linear));
            Assert.That(syntax.Members[1], Is.TypeOf<EventDeclarationSyntax>());
        }

        [TestCase("sync pub let mut value = 0;", "SBK1012")]
        [TestCase("pub pub let value = 0;", "SBK1013")]
        [TestCase("sync() let mut value = 0;", "SBK1011")]
        [TestCase("sync(unknown) let mut value = 0;", "SBK1010")]
        [TestCase("sync(linear, smooth) let mut value = 0;", "SBK1011")]
        [TestCase("sync(linear smooth) let mut value = 0;", "SBK1011")]
        [TestCase("on Interact() { pub let value = 0; }", "SBK1014")]
        [TestCase("on Interact() { sync let mut value = 0; }", "SBK1015")]
        [TestCase("pub sync(linear) fn value() {}", "SBK1016")]
        [TestCase("let value;", "SBK1017")]
        public void Parser_ReportsStateSyntaxDiagnostics(string source, string code)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, code), Is.True,
                Format(parser.Diagnostics.Diagnostics));
        }

        [TestCase("sync let mut value = 0;", "None")]
        [TestCase("sync(none) let mut value = 0;", "None")]
        [TestCase("sync(linear) let mut value: f32 = 0.0;", "Linear")]
        [TestCase("sync(smooth) let mut value: f32 = 0.0;", "Smooth")]
        public void Parser_ParsesAllSynchronizationForms(
            string source,
            string expectedMode)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var state = syntax.Members[0] as StateDeclarationSyntax;
            Assert.That(state, Is.Not.Null);
            Assert.That(state.SynchronizationModifier.Mode.ToString(), Is.EqualTo(expectedMode));
        }

        [Test]
        public void Parser_RecoversFromMalformedStateBeforeFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"sync(unknown) let mut value = 0;
fn read() -> i32 { return value; }
on Interact() { Debug.Log(read()); }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1010"), Is.True);
            Assert.That(syntax.Members.Count, Is.EqualTo(3));
            Assert.That(syntax.Members[1], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members[2], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Binder_BindsStateMetadataAndBareSyncAsNone()
        {
            var (program, diagnostics) = Bind(
                @"pub let enabled = true;
sync let mut count: i32 = 0;
pub sync(smooth) let mut value: f32 = -1.0;" );

            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            Assert.That(program.States.Count, Is.EqualTo(3));

            var enabled = program.States[0].StateSymbol;
            Assert.That(enabled.Type, Is.EqualTo(TypeSymbol.Bool));
            Assert.That(enabled.IsMutable, Is.False);
            Assert.That(enabled.IsPublic, Is.True);
            Assert.That(enabled.IsSynchronized, Is.False);
            Assert.That(enabled.InitialValue, Is.EqualTo(true));

            var count = program.States[1].StateSymbol;
            Assert.That(count.SynchronizationMode, Is.EqualTo(StateSynchronizationMode.None));
            Assert.That(count.IsPublic, Is.False);

            var value = program.States[2].StateSymbol;
            Assert.That(value.SynchronizationMode, Is.EqualTo(StateSynchronizationMode.Smooth));
            Assert.That(value.InitialValue, Is.EqualTo(-1.0f));
        }

        [TestCase("sync let value = 0;", "SBK2060")]
        [TestCase("sync(linear) let mut value = \"text\";", "SBK2061")]
        [TestCase("let value = runtime_value(); fn runtime_value() -> i32 { return 1; }", "SBK2062")]
        [TestCase("let value = 0; let value = 1;", "SBK2058")]
        [TestCase("let value = 0; on Interact() { value = 1; }", "SBK2059")]
        public void Binder_ReportsStateSemanticDiagnostics(string source, string code)
        {
            var (_, diagnostics) = Bind(source);

            Assert.That(ContainsCode(diagnostics, code), Is.True, Format(diagnostics));
        }

        [Test]
        public void Binder_ResolvesForwardStateReferenceAndLetsLocalShadowState()
        {
            var (program, diagnostics) = Bind(
                @"on Interact() {
  count = 1;
  let count = 10;
  Debug.Log(count);
}
let mut count = 0;" );

            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var statements = program.Events[0].Body.Statements;
            var assignment = ((BoundExpressionStatement)statements[0]).Expression
                as BoundAssignmentExpression;
            Assert.That(assignment.Variable, Is.TypeOf<StateVariableSymbol>());

            var call = ((BoundExpressionStatement)statements[2]).Expression as BoundCallExpression;
            var argument = call.Arguments[0] as BoundNameExpression;
            Assert.That(argument.Symbol, Is.TypeOf<LocalVariableSymbol>());
        }

        [Test]
        public void IrLowerer_UsesStateStorageForLoadsStoresFunctionsAndEvents()
        {
            var (program, diagnostics) = Bind(
                @"let mut count = 0;
fn increment() { count += 1; }
on Interact() { increment(); Debug.Log(count); }
on Update() { count += 2; Debug.Log(count); }" );
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
            const string source = @"pub sync(linear) let mut value: f32 = 0.0;
on Interact() { value += 1.0; Debug.Log(value); }
on Update() { Debug.Log(value); }";

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, "value: %SystemSingle"), Is.EqualTo(1));
            Assert.That(result.Uasm, Does.Contain(".export value"));
            Assert.That(result.Uasm, Does.Contain(".sync value, linear"));
            Assert.That(CountOccurrences(result.Uasm, "PUSH, value"), Is.GreaterThanOrEqualTo(3));
            var statePatch = FindStatePatch(result.HeapPatches, "value");
            Assert.That(statePatch.Kind, Is.EqualTo(HeapPatchKind.GlobalInitializer));
            Assert.That(statePatch.SymbolName, Is.EqualTo("value"));
            Assert.That(statePatch.RuntimeValue, Is.EqualTo(0.0f));
        }

        [Test]
        public void CompileToUasm_KeepsPrivateSynchronizedStateOutOfSourcePublicApi()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"sync let mut private_status = 0;
pub let mut public_status = 0;
on Interact() { private_status = public_status; }" );

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
                "let mut count = 0; on Interact() { count += 1; Debug.Log(count); }",
                "pub let mut enabled = true; on Interact() { enabled = !enabled; }",
                "sync let mut global_status = 0; on Interact() { Debug.Log(global_status); }",
                "pub sync(linear) let mut synchronized_value: f32 = 0.0; on Update() { Debug.Log(synchronized_value); }",
                "let target: GameObject = null; on Interact() { Debug.Log(target); }"
            };

            foreach (var source in sources)
            {
                var result = SobakasuCompiler.CompileToUasm(source);
                Assert.That(result.Success, Is.True, result.ErrorText);
            }
        }

        [Test]
        public void AssemblePatchCommitAndRefresh_PreservesStateInitialValueAndSyncMetadata()
        {
            const string source = @"pub sync(linear) let mut value: f32 = -2.5;
on Update() { Debug.Log(value); }";
            var result = SobakasuCompiler.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError),
                Is.True, commitError);

            AssertProgramState(asset, "value", -2.5f, "Linear");
            asset.RefreshProgram();
            AssertProgramState(asset, "value", -2.5f, "Linear");
        }

        private static List<SyntaxToken> LexAll(string source)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));
            var tokens = new List<SyntaxToken>();
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                tokens.Add(token);
            }
            while (token.Kind != SyntaxKind.EndOfFile);

            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty);
            return tokens;
        }

        private static (BoundProgram Program, IReadOnlyList<Diagnostic> Diagnostics) Bind(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            if (parser.Diagnostics.HasErrors)
                return (null, parser.Diagnostics.Diagnostics);

            var binder = new SobakasuBinder();
            var program = binder.BindProgram(syntax);
            return (program, binder.Diagnostics.Diagnostics);
        }

        private static int CountStateCopies(IrProgram program)
        {
            var count = 0;
            foreach (var module in program.Modules)
            foreach (var block in module.Blocks)
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCopyInstruction copy && copy.Target is IrStateStorage)
                    count++;
            }

            return count;
        }

        private static int CountStateValues(IrProgram program)
        {
            var count = 0;
            foreach (var module in program.Modules)
            foreach (var block in module.Blocks)
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCopyInstruction copy && copy.Source is IrStateStorage)
                    count++;

                if (instruction is IrExternCallInstruction call)
                foreach (var argument in call.Arguments)
                {
                    if (argument is IrStateStorage)
                        count++;
                }
            }

            return count;
        }

        private static HeapPatchEntry FindStatePatch(
            IReadOnlyList<HeapPatchEntry> patches,
            string symbolPrefix)
        {
            foreach (var patch in patches)
            {
                if (patch.Kind == HeapPatchKind.GlobalInitializer &&
                    patch.SymbolName.StartsWith(symbolPrefix, StringComparison.Ordinal))
                {
                    return patch;
                }
            }

            return null;
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

        private static bool ContainsCode(IReadOnlyList<Diagnostic> diagnostics, string code)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }

        private static string Format(IReadOnlyList<Diagnostic> diagnostics)
        {
            var lines = new List<string>();
            foreach (var diagnostic in diagnostics)
                lines.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", lines);
        }

        private SobakasuProgramAsset CreateProgramAsset()
        {
            var folderGuid = AssetDatabase.CreateFolder(
                "Assets",
                $"SobakasuStateVariableTests_{Guid.NewGuid():N}");
            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            _cleanupAssetPaths.Add(folderPath);

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folderPath}/SobakasuProgramAsset.asset");
            var asset = ScriptableObject.CreateInstance<SobakasuProgramAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            _cleanupAssetPaths.Add(assetPath);
            return AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
        }

        private static void AssertProgramState(
            SobakasuProgramAsset asset,
            string symbol,
            object expectedValue,
            string expectedInterpolation)
        {
            var program = asset.GetRealProgram();
            Assert.That(program, Is.Not.Null);
            Assert.That(program.SymbolTable.HasExportedSymbol(symbol), Is.True);
            var address = program.SymbolTable.GetAddressFromSymbol(symbol);
            Assert.That(program.Heap.GetHeapVariable(address), Is.EqualTo(expectedValue));

            var sync = program.SyncMetadataTable.GetSyncMetadataFromSymbol(symbol);
            Assert.That(sync, Is.Not.Null);
            Assert.That(sync.Properties.Count, Is.GreaterThan(0));
            Assert.That(sync.Properties[0].InterpolationAlgorithm.ToString(),
                Is.EqualTo(expectedInterpolation));
        }
    }
}
