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
    public class StateBindingTests
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
        private SobakasuProgramAsset CreateProgramAsset()
        {
            return SobakasuTestAssetFactory.CreateImportedProgramAsset(
                "SobakasuStateVariableTests",
                _cleanupAssetPaths.Add);
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
    }
}
