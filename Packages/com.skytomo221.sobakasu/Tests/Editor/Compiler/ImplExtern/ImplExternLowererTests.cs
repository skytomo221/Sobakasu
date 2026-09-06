using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Desugar;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Optimizer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;
using UnityEditor;
using UnityEngine;

using static Skytomo221.Sobakasu.Tests.Editor.ImplExternTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class ImplExternLowererTests
    {

        private const string MaybeDefinition = @"
lang ""maybe""
enum Maybe<T> {
  Nothing,
  Just(T),
}
";
        private const string ProjectedMixedSignature =
            "TestApi.__Mixed__SystemInt32Ref_TestOwnerRef_SystemStringRef__SystemInt32";
        private const string ProjectedValiditySignature =
            "VRCSDKBaseUtilities.__IsValid__TestOwner__SystemBoolean";
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

        private void RegisterForCleanup(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
                _cleanupAssetPaths.Add(assetPath);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Lowerer_CapturesOperatorReceiverBeforeRightHandSideMutation(bool aggregate, bool compound)
        {
            var declaration = aggregate
                ? "struct Holder { value: i32, } state holder = Holder { value: 10 };"
                : "state value = 10;";
            var target = aggregate ? "holder.value" : "value";
            var expression = compound ? $"{target} += replace()" : $"{target} + replace()";
            var (Program, Ir, Uasm) = CompileWithEnvironment($@"
impl i32 {{ pub fn +(rhs: Self) -> Self = extern self + rhs }}
{declaration}
fn replace() -> i32 {{ {target} = 20; 1 }}
on start {{ {expression}; }}",
                new SobakasuCompilationEnvironment(SobakasuBuiltInEnvironment.Default.ExternCatalog));

            var blocks = Ir.Modules[0].Blocks.ToDictionary(block => block.Label);
            var current = Ir.Modules[0].Blocks[0];
            var visited = new HashSet<string>();
            var copies = new List<IrCopyInstruction>();
            while (current != null)
            {
                Assert.That(visited.Add(current.Label), Is.True);
                copies.AddRange(current.Instructions.OfType<IrCopyInstruction>());
                current = current.Terminator is IrJumpTerminator jump ? blocks[jump.TargetLabel] : null;
            }
            var read = copies.FindIndex(copy => copy.Source is IrStateStorage);
            var write = copies.FindIndex(copy => copy.Target is IrStateStorage);
            Assert.That(read, Is.GreaterThanOrEqualTo(0));
            Assert.That(write, Is.GreaterThan(read));
        }

        [Test]
        public void Lowerer_EvaluatesMethodReceiverOnce()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"pub impl Vector3 = extern UnityEngine.Vector3 {
  pub static fn new(x: f32, y: f32, z: f32) -> Self {
    extern new Self(x, y, z)
  }

  pub fn magnitude -> f32 {
    extern self.magnitude
  }
}

fn create -> Vector3 {
  Vector3.new(1.0f32, 2.0f32, 3.0f32)
}

on interact {
  extern UnityEngine.Debug.Log(create.magnitude);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(
                CountOccurrences(result.Uasm, "UnityEngineVector3.__ctor"),
                Is.EqualTo(1));
        }

        [Test]
        public void Lowerer_EvaluatesExternSetterReceiverAndValueOnce()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"pub impl GameObject = extern UnityEngine.GameObject {}

fn get_target -> GameObject {
  extern UnityEngine.Debug.Log(""receiver"");
  extern UnityEngine.GameObject.Find(""Sobakasu"")
}

fn get_name -> string {
  extern UnityEngine.Debug.Log(""value"");
  ""Sobakasu""
}

on interact {
  extern get_target().name = get_name();
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(
                CountOccurrences(
                    result.Uasm,
                    "UnityEngineDebug.__Log__SystemObject__SystemVoid"),
                Is.EqualTo(2));
            Assert.That(result.Uasm, Does.Contain("UnityEngineObject.__set_name"));
        }

        [Test]
        public void IrLowerer_ProjectsMaybeOutOnceAndPreservesOutputOrder()
        {
            var environment = CreateProjectionEnvironment();
            var (Program, Ir, Uasm) = CompileWithEnvironment(
                MaybeDefinition + @"
fn mixed(value: i32) -> (i32, i32, Maybe<Test.Owner>, string)
  = extern Test.Api.Mixed(
      ref i32 value,
      maybe out Test.Owner owner,
      out string text)
on start {
  let (returned, updated, owner, text) = mixed(1);
}",
                environment);

            var method = FindExternalMethod(Program, "mixed");
            Assert.That(method.ReturnType.TupleElementTypes.Select(type => type.Name),
                Is.EqualTo(new[] { "i32", "i32", "Maybe<Owner>", "string" }));
            Assert.That(method.AbiParameters.Select(parameter => parameter.PassingMode),
                Is.EqualTo(new[]
                {
                    ExternParameterPassingMode.Ref,
                    ExternParameterPassingMode.Out,
                    ExternParameterPassingMode.Out
                }));
            Assert.That(method.AbiParameters.Select(
                    parameter => parameter.LogicalOutputProjection),
                Is.EqualTo(new[]
                {
                    ExternLogicalOutputProjection.Raw,
                    ExternLogicalOutputProjection.Maybe,
                    ExternLogicalOutputProjection.Raw
                }));

            var call = FindExternCall(Ir, ProjectedMixedSignature);
            Assert.That(call.Arguments.Select(argument => argument.Type.Name),
                Is.EqualTo(new[] { "i32", "Owner", "string" }));
            Assert.That(CountExternCalls(Ir, ProjectedMixedSignature),
                Is.EqualTo(1));
            Assert.That(CountExternCalls(Ir, ProjectedValiditySignature),
                Is.EqualTo(1));
            Assert.That(Uasm, Does.Not.Contain("SystemValueTuple"));
        }
    }
}
