using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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

using static Skytomo221.Sobakasu.Tests.Editor.AggregateTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class AggregateExternalBehaviorTests : AggregateTestFixture
    {


        [Test]
        public void Binder_BindsExternalAggregatesAsNativeAbiTypes()
        {
            var (_, diagnostics) = Bind(@"
pub struct Vector = extern UnityEngine.Vector3 { x: f32 = extern x, }
pub enum Target = extern VRC.Udon.Common.Interfaces.NetworkEventTarget { All = extern All, }
impl Vector { fn magnitude -> f32 = extern self.magnitude }
fn read(value: Vector) -> f32 { value.x }
fn write(value: Vector, next: f32) { value.x = next; }
fn target -> Target { Target.All }
fn vectors(values: [Vector]) -> [Vector] { values }");
            Assert.That(diagnostics, Is.Empty, Format(diagnostics));
        }

        [TestCase("pub enum Bad = extern UnityEngine.Vector3 { A = extern A, }", "SBK2164")]
        [TestCase("pub struct Bad = extern VRC.Udon.Common.Interfaces.NetworkEventTarget { value: i32 = extern value, }", "SBK2164")]
        [TestCase("pub enum Bad = extern VRC.Udon.Common.Interfaces.NetworkEventTarget { A(i32) = extern All, }", "SBK2167")]
        [TestCase("pub enum Bad = extern VRC.Udon.Common.Interfaces.NetworkEventTarget { A { value: i32, } = extern All, }", "SBK2167")]
        public void Binder_ReportsExternalAggregateDiagnostics(string source, string code)
        {
            var (_, diagnostics) = Bind(source);
            Assert.That(ContainsCode(diagnostics, code), Is.True, Format(diagnostics));
        }

        [Test]
        public void Compiler_PreservesRawExternReferenceReturnEscapeHatch()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on start {
  let raw = extern UnityEngine.GameObject.Find(""Sobakasu"");
  extern UnityEngine.Debug.Log(raw);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(
                "UnityEngineGameObject.__Find__SystemString__UnityEngineGameObject"));
        }

        [Test]
        public void Compiler_LeavesAbiNullInInactiveMaybeReferencePayload()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use unity.GameObject;
state target: Maybe<GameObject> = Maybe.Nothing;
on start {
  let present = match target {
    Maybe.Just(_) => true,
    Maybe.Nothing => false,
  };
  extern UnityEngine.Debug.Log(present);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(FindPatch(result.HeapPatches, "__state_0").RuntimeValue,
                Is.EqualTo(0));
            Assert.That(FindPatch(result.HeapPatches, "__state_1"), Is.Null);
            Assert.That(result.Uasm, Does.Contain("__state_1"));
            Assert.That(result.Uasm, Does.Contain("%UnityEngineGameObject, null"));
        }


    }
}
