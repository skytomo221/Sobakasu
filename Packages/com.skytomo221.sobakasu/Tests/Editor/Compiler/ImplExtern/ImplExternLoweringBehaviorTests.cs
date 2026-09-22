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
    public class ImplExternLoweringBehaviorTests : ImplExternTestFixture
    {


        [Test]
        public void Lowerer_EvaluatesMethodReceiverOnce()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"pub impl Vector3 = extern UnityEngine.Vector3 {
  pub fn new(x: f32, y: f32, z: f32) -> Self {
    extern new Self(x, y, z)
  }

  pub fn magnitude(self) -> f32 {
    extern self.magnitude
  }
}

fn create -> Vector3 {
  Vector3::new(1.0f32, 2.0f32, 3.0f32)
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
fn mixed(value: i32) -> (i32, i32, Maybe<Test::Owner>, string)
  = extern Test.Api.Mixed(
      ref i32 value,
      maybe out Test::Owner owner,
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
