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
    public class StateConstantTests
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

        [Test]
        public void Binder_BindsTypedInferredAndForwardConstants()
        {
            var (program, diagnostics) = Bind(
                @"impl i32 {
  pub fn +(rhs: Self) -> Self = extern self + rhs
  pub fn *(rhs: Self) -> Self = extern self * rhs
}
const FORWARD = BASE + 1;
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

        [TestCase("10 + 3", -7)]
        [TestCase("(10 + 3) + 2", 9)]
        public void Binder_EvaluatesConstantsUsingTheSelectedDeclarativeOperator(string expression, int expected)
        {
            var (program, diagnostics) = Bind($@"
impl i32 {{ pub fn +(rhs: Self) -> Self = extern rhs - self }}
const RESULT = {expression};");

            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
            Assert.That(program.Constants[0].ConstantSymbol.ConstantValue, Is.EqualTo(expected));
        }

        [TestCase("impl i32 { pub fn +(rhs: Self) -> Self { rhs } } const A = 1 + 2;", "SBK2152")]
        [TestCase("impl i32 { pub fn +(rhs: Self) -> Self = extern System.Math.Abs(rhs) } const A = 1 + 2;", "SBK2152")]
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
    }
}
