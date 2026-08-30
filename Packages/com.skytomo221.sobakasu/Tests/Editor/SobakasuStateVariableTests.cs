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
            var tokens = LexAll("pub sync(none) state linear = smooth;");

            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.PubKeyword));
            Assert.That(tokens[1].Kind, Is.EqualTo(SyntaxKind.SyncKeyword));
            Assert.That(tokens[3].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[3].Text, Is.EqualTo("none"));
            Assert.That(tokens[5].Kind, Is.EqualTo(SyntaxKind.StateKeyword));
            Assert.That(tokens[6].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[8].Kind, Is.EqualTo(SyntaxKind.Identifier));
        }

        [Test]
        public void Parser_ParsesPublicSynchronizedStateAndFollowingEvent()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"pub sync(linear) state value: f32;
on interact() { value = 1.0; }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members.Count, Is.EqualTo(2));
            var state = syntax.Members[0] as StateDeclarationSyntax;
            Assert.That(state, Is.Not.Null);
            Assert.That(state.PubKeyword, Is.Not.Null);
            Assert.That(state.StateKeyword.Kind, Is.EqualTo(SyntaxKind.StateKeyword));
            Assert.That(state.MutKeyword, Is.Null);
            Assert.That(state.Identifier.Text, Is.EqualTo("value"));
            Assert.That(state.SynchronizationModifier.Mode,
                Is.EqualTo(SynchronizationModeSyntaxKind.Linear));
            Assert.That(state.EqualsToken, Is.Null);
            Assert.That(state.Initializer, Is.Null);
            Assert.That(syntax.Members[1], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_ParsesPrivateAndPublicConstants()
        {
            var parser = new SobakasuParser(SourceText.From(
                "const X = 1; pub const Y: i32 = X + 1;"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(2));
            var privateConstant = syntax.Members[0] as ConstDeclarationSyntax;
            var publicConstant = syntax.Members[1] as ConstDeclarationSyntax;
            Assert.That(privateConstant, Is.Not.Null);
            Assert.That(privateConstant.PubKeyword, Is.Null);
            Assert.That(publicConstant, Is.Not.Null);
            Assert.That(publicConstant.PubKeyword, Is.Not.Null);
            Assert.That(publicConstant.TypeClause, Is.Not.Null);
        }

        [TestCase("sync pub state value: i32;", "SBK1012")]
        [TestCase("pub pub state value: i32;", "SBK1013")]
        [TestCase("sync() state value = 0;", "SBK1011")]
        [TestCase("sync(unknown) state value = 0;", "SBK1010")]
        [TestCase("sync(linear, smooth) state value = 0;", "SBK1011")]
        [TestCase("sync(linear smooth) state value = 0;", "SBK1011")]
        [TestCase("on interact() { pub let value = 0; }", "SBK1014")]
        [TestCase("on interact() { sync let mut value = 0; }", "SBK1015")]
        [TestCase("pub sync(linear) fn value() {}", "SBK1016")]
        [TestCase("state value;", "SBK1017")]
        [TestCase("let value = 0;", "SBK1033")]
        [TestCase("let mut value = 0;", "SBK1033")]
        [TestCase("pub let value = 0;", "SBK1033")]
        [TestCase("sync let mut value = 0;", "SBK1033")]
        [TestCase("state mut value = 0;", "SBK1034")]
        [TestCase("sync const VALUE = 0;", "SBK1035")]
        [TestCase("on interact { const VALUE = 0; }", "SBK1036")]
        [TestCase("on interact { state value = 0; }", "SBK1036")]
        [TestCase("const VALUE;", "SBK1037")]
        public void Parser_ReportsStateSyntaxDiagnostics(string source, string code)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, code), Is.True,
                Format(parser.Diagnostics.Diagnostics));
        }

        [TestCase("pub state value = 1;", "SBK1040")]
        [TestCase("pub state value: i32 = 1;", "SBK1040")]
        [TestCase("pub sync state value: i32 = 1;", "SBK1040")]
        [TestCase("pub sync(linear) state value: f32 = 1.0;", "SBK1040")]
        [TestCase("pub state value;", "SBK1041")]
        public void Parser_ReportsPublicStateOwnershipDiagnostics(string source, string code)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, code), Is.True,
                Format(parser.Diagnostics.Diagnostics));
        }

        [TestCase("sync state value = 0;", "None")]
        [TestCase("sync(none) state value = 0;", "None")]
        [TestCase("sync(linear) state value: f32 = 0.0;", "Linear")]
        [TestCase("sync(smooth) state value: f32 = 0.0;", "Smooth")]
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

        [TestCase("pub state value: i32;")]
        [TestCase("pub sync state value: i32;")]
        [TestCase("pub sync(linear) state value: f32;")]
        [TestCase("state private_value = 1;")]
        [TestCase("sync state synchronized_private = 1;")]
        public void Parser_ParsesRequiredStateForms(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(1));
            Assert.That(syntax.Members[0], Is.TypeOf<StateDeclarationSyntax>());
        }

        [Test]
        public void Parser_RecoversFromMalformedStateBeforeFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"sync(unknown) state value = 0;
fn read() -> i32 { return value; }
on interact() { extern UnityEngine.Debug.Log(read()); }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1010"), Is.True);
            Assert.That(syntax.Members.Count, Is.EqualTo(3));
            Assert.That(syntax.Members[1], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members[2], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_ConsumesForbiddenPublicInitializerAndPreservesFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"pub state value: i32 = unknown_function();
fn read() -> i32 { return value; }
on interact() { extern UnityEngine.Debug.Log(read()); }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Has.Count.EqualTo(1),
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1040"), Is.True);
            Assert.That(syntax.Members, Has.Count.EqualTo(3));
            Assert.That(((StateDeclarationSyntax)syntax.Members[0]).Initializer, Is.Not.Null);
            Assert.That(syntax.Members[1], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members[2], Is.TypeOf<EventDeclarationSyntax>());
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
        public void Binder_BindsStateMetadataAndBareSyncAsNone()
        {
            var (program, diagnostics) = Bind(
                @"pub state enabled: bool;
sync state count: i32 = 0;
pub sync(smooth) state value: f32;" );

            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            Assert.That(program.States.Count, Is.EqualTo(3));

            var enabled = program.States[0].StateSymbol;
            Assert.That(enabled.Type, Is.EqualTo(TypeSymbol.Bool));
            Assert.That(enabled.IsMutable, Is.True);
            Assert.That(enabled.IsPublic, Is.True);
            Assert.That(enabled.IsSynchronized, Is.False);
            Assert.That(enabled.InitialValue, Is.Null);
            Assert.That(program.States[0].Initializer, Is.Null);

            var count = program.States[1].StateSymbol;
            Assert.That(count.SynchronizationMode, Is.EqualTo(StateSynchronizationMode.None));
            Assert.That(count.IsPublic, Is.False);
            Assert.That(count.InitialValue, Is.EqualTo(0));
            Assert.That(program.States[1].Initializer, Is.Not.Null);

            var value = program.States[2].StateSymbol;
            Assert.That(value.SynchronizationMode, Is.EqualTo(StateSynchronizationMode.Smooth));
            Assert.That(value.InitialValue, Is.Null);
            Assert.That(program.States[2].Initializer, Is.Null);
        }

        [Test]
        public void Binder_BindsTypedInferredAndForwardConstants()
        {
            var (program, diagnostics) = Bind(
                @"const FORWARD = BASE + 1;
const BASE = 10;
pub const DOUBLE: i32 = BASE * 2;
on interact { extern UnityEngine.Debug.Log(FORWARD + DOUBLE); }");

            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            Assert.That(program.Constants.Count, Is.EqualTo(3));
            Assert.That(program.Constants[0].ConstantSymbol.Type, Is.EqualTo(TypeSymbol.I32));
            Assert.That(program.Constants[0].ConstantSymbol.ConstantValue, Is.EqualTo(11));
            Assert.That(program.Constants[2].ConstantSymbol.ConstantValue, Is.EqualTo(20));
            Assert.That(program.Constants[2].ConstantSymbol.IsPublic, Is.True);
        }

        [TestCase("const A: i32 = runtime_value(); fn runtime_value() -> i32 { 1 }", "SBK2152")]
        [TestCase("const A: f32 = extern UnityEngine.Mathf.Sqrt(1.0f32);", "SBK2152")]
        [TestCase("state value = 1; const A: i32 = value;", "SBK2152")]
        [TestCase("const A = B; const B = A;", "SBK2153")]
        [TestCase("const VALUES = [1, 2, 3];", "SBK2151")]
        public void Binder_ReportsConstantSemanticDiagnostics(string source, string code)
        {
            var (_, diagnostics) = Bind(source);

            Assert.That(ContainsCode(diagnostics, code), Is.True, Format(diagnostics));
        }

        [Test]
        public void IrAndUasm_UseConstantsWithoutCreatingDeclaredStateStorage()
        {
            const string source = @"pub const INITIAL = 20;
state score = INITIAL;
on interact { score = INITIAL + 1; }";
            var (program, diagnostics) = Bind(source);
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

        [TestCase("sync(linear) state value = \"text\";", "SBK2061")]
        [TestCase("state value = runtime_value(); fn runtime_value() -> i32 { return 1; }", "SBK2062")]
        [TestCase("state value = 0; state value = 1;", "SBK2058")]
        public void Binder_ReportsStateSemanticDiagnostics(string source, string code)
        {
            var (_, diagnostics) = Bind(source);

            Assert.That(ContainsCode(diagnostics, code), Is.True, Format(diagnostics));
        }

        [Test]
        public void Binder_ResolvesForwardStateReferenceAndLetsLocalShadowState()
        {
            var (program, diagnostics) = Bind(
                @"on interact() {
  count = 1;
  let count = 10;
  extern UnityEngine.Debug.Log(count);
}
state count = 0;" );

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
        public void Binder_LetsLocalShadowConstant()
        {
            var (program, diagnostics) = Bind(
                @"const VALUE = 10;
on interact {
  let VALUE = 20;
  extern UnityEngine.Debug.Log(VALUE);
}");

            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            var statements = program.Events[0].Body.Statements;
            var call = ((BoundExpressionStatement)statements[1]).Expression
                as BoundCallExpression;
            var argument = call.Arguments[0] as BoundNameExpression;
            Assert.That(argument.Symbol, Is.TypeOf<LocalVariableSymbol>());
        }

        [Test]
        public void IrLowerer_UsesStateStorageForLoadsStoresFunctionsAndEvents()
        {
            var (program, diagnostics) = Bind(
                @"state count = 0;
fn increment() { count += 1; }
on interact() { increment(); extern UnityEngine.Debug.Log(count); }
on update() { count += 2; extern UnityEngine.Debug.Log(count); }" );
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
on interact() { private_status = public_status; }" );

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

        [Test]
        public void AssemblePatchCommitAndRefresh_PreservesPrivateStateInitialValueAndSyncMetadata()
        {
            const string source = @"sync(linear) state value: f32 = -2.5;
on update() { extern UnityEngine.Debug.Log(value); }";
            var result = SobakasuCompiler.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);
            var statePatch = FindStatePatch(result.HeapPatches, "__state_");
            Assert.That(statePatch, Is.Not.Null, FormatHeapPatches(result.HeapPatches));

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError),
                Is.True, commitError);

            AssertProgramState(asset, statePatch.SymbolName, -2.5f, "Linear");
            asset.RefreshProgram();
            AssertProgramState(asset, statePatch.SymbolName, -2.5f, "Linear");
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

        private static bool ContainsIrConstant(IrProgram program, object value)
        {
            foreach (var module in program.Modules)
            foreach (var block in module.Blocks)
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCopyInstruction copy &&
                    copy.Source is IrConstantValue constant &&
                    Equals(constant.Value, value))
                {
                    return true;
                }

                if (instruction is IrExternCallInstruction call)
                foreach (var argument in call.Arguments)
                {
                    if (argument is IrConstantValue argumentConstant &&
                        Equals(argumentConstant.Value, value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CountGlobalInitializerPatches(
            IReadOnlyList<HeapPatchEntry> patches)
        {
            var count = 0;
            foreach (var patch in patches)
            {
                if (patch.Kind == HeapPatchKind.GlobalInitializer)
                    count++;
            }
            return count;
        }

        private static string FormatHeapPatches(IReadOnlyList<HeapPatchEntry> patches)
        {
            var entries = new List<string>();
            foreach (var patch in patches)
                entries.Add($"{patch.Kind}:{patch.SymbolName}");
            return string.Join(", ", entries);
        }

        private static HeapPatchEntry FindStatePatch(
            IReadOnlyList<HeapPatchEntry> patches,
            string symbolFragment)
        {
            foreach (var patch in patches)
            {
                if (patch.Kind == HeapPatchKind.GlobalInitializer &&
                    patch.SymbolName.IndexOf(symbolFragment, StringComparison.Ordinal) >= 0)
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
            return SobakasuTestAssetFactory.CreateImportedProgramAsset(
                "SobakasuStateVariableTests",
                _cleanupAssetPaths.Add);
        }

        private static void AssertProgramState(
            SobakasuProgramAsset asset,
            string symbol,
            object expectedValue,
            string expectedInterpolation)
        {
            var program = asset.GetRealProgram();
            Assert.That(program, Is.Not.Null);
            Assert.That(program.SymbolTable.HasExportedSymbol(symbol), Is.False);
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
