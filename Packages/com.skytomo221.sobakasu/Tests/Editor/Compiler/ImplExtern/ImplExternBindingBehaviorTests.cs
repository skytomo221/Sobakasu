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
    public class ImplExternBindingBehaviorTests : ImplExternTestFixture
    {


        [Test]
        public void Binder_ResolvesExactMethodOverload()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"impl i32 {
  fn choose(value: i32) -> i32 { value }
  fn choose(value: i64) -> i64 { value }
}
on interact {
  let receiver = 1;
  extern UnityEngine.Debug.Log(receiver.choose(2));
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_RejectsRemovedNullLiteralBeforeOverloadResolution()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl GameObject = extern UnityEngine.GameObject {}
impl i32 {
  fn choose(value: GameObject) -> i32 { 1 }
  fn choose(value: string) -> i32 { 2 }
}
on interact {
  let receiver = 1;
  receiver.choose(null);
}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK0007"), Is.True,
                result.ErrorText);
        }

        [Test]
        public void Binder_ReportsNoApplicableMethodOverload()
        {
            var binder = Bind(
                @"impl i32 {
  fn choose(value: bool) -> i32 { 1 }
}
on interact {
  let receiver = 1;
  receiver.choose(2);
}");

            Assert.That(ContainsCode(binder.Diagnostics.Diagnostics, "SBK2081"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsUnsupportedAndUnknownExternExpressions()
        {
            var unsupported = Bind("on interact { extern 1; }");
            Assert.That(ContainsCode(unsupported.Diagnostics.Diagnostics, "SBK2087"), Is.True,
                Format(unsupported.Diagnostics.Diagnostics));

            var unknown = Bind(
                "on interact { extern UnityEngine.Debug.MemberThatDoesNotExist; }");
            Assert.That(ContainsCode(unknown.Diagnostics.Diagnostics, "SBK2083"), Is.True,
                Format(unknown.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsExternalExposureAndOverloadDiagnostics()
        {
            var notExposed = Bind(
                "on interact { extern System.Console.WriteLine(1); }");
            Assert.That(ContainsCode(notExposed.Diagnostics.Diagnostics, "SBK2084"), Is.True,
                Format(notExposed.Diagnostics.Diagnostics));

            var notApplicable = Bind(
                "on interact { extern UnityEngine.Mathf.Clamp(\"x\", 0, 1); }");
            Assert.That(ContainsCode(notApplicable.Diagnostics.Diagnostics, "SBK2085"), Is.True,
                Format(notApplicable.Diagnostics.Diagnostics));

            var ambiguous = Bind(
                "on interact { extern Test.Api.Call(1); }",
                CreateAmbiguousExternEnvironment());
            Assert.That(ContainsCode(ambiguous.Diagnostics.Diagnostics, "SBK2086"), Is.True,
                Format(ambiguous.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_KeepsExternalBindingDistinctOutsideExternCalls()
        {
            var binder = Bind(
                @"pub impl GameObject = extern UnityEngine.GameObject {}

fn accepts_runtime(value: UnityEngine.GameObject) {}

on interact {
  let wrapped: GameObject = extern UnityEngine.GameObject.Find(""Sobakasu"");
  accepts_runtime(wrapped);
}");

            Assert.That(binder.Diagnostics.HasErrors, Is.True);
        }

        [Test]
        public void Compiler_InfersRawBindingReturnsAndPublishesResolvedMetadata()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"impl i32 { pub fn @- -> Self = extern -self }
pub fn abs(value: i32)
  = extern System.Math.Abs(value)

pub impl GameObject = extern UnityEngine.GameObject {
  pub fn set_active(active: bool)
    = extern self.SetActive(active)

  pub fn name
    = extern self.name

  pub fn set_name(value: string)
    = extern self.name = value
}

on interact {
  extern UnityEngine.Debug.Log(abs(-2));
  let target = extern UnityEngine.GameObject.Find(""Sobakasu"");
  target.set_active(true);
  target.set_name(""Sobakasu"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            var abs = result.ExternalBindings.Single(binding =>
                binding.SobakasuName == "abs");
            Assert.That(abs.SobakasuSymbol, Is.Not.Empty);
            Assert.That(abs.SobakasuParameterTypes,
                Is.EqualTo(new[] { "i32" }));
            Assert.That(abs.SobakasuReturnType, Is.EqualTo("i32"));
            Assert.That(abs.ExternalDeclaringType, Is.EqualTo("System.Math"));
            Assert.That(abs.ExternalMemberName, Is.EqualTo("Abs"));
            Assert.That(abs.ExternalParameterTypes, Is.EqualTo(new[] { "System.Int32" }));
            Assert.That(abs.ExternalReturnType, Is.EqualTo("System.Int32"));
            Assert.That(abs.ResolvedExternalSignature, Does.Contain("SystemInt32"));
            Assert.That(abs.InvocationKind, Is.EqualTo(ExternalBindingInvocationKind.Static));
            Assert.That(abs.MemberKind, Is.EqualTo(ExternalBindingMemberKind.Method));
            Assert.That(abs.ReturnMode, Is.EqualTo(ExternalBindingReturnMode.Raw));

            var instance = result.ExternalBindings.Single(binding =>
                binding.SobakasuName == "GameObject.set_active");
            Assert.That(instance.InvocationKind,
                Is.EqualTo(ExternalBindingInvocationKind.Instance));
            Assert.That(instance.ExternalParameterTypes,
                Is.EqualTo(new[] { "System.Boolean" }));
            Assert.That(instance.SobakasuReturnType, Is.EqualTo("()"));

            var getter = result.ExternalBindings.Single(binding =>
                binding.SobakasuName == "GameObject.name");
            Assert.That(getter.MemberKind,
                Is.EqualTo(ExternalBindingMemberKind.Getter));
            Assert.That(getter.SobakasuReturnType, Is.EqualTo("string"));

            var setter = result.ExternalBindings.Single(binding =>
                binding.SobakasuName == "GameObject.set_name");
            Assert.That(setter.MemberKind,
                Is.EqualTo(ExternalBindingMemberKind.Setter));
            Assert.That(setter.ExternalParameterTypes,
                Is.EqualTo(new[] { "System.String" }));
        }

        [Test]
        public void Compiler_ValidatesExplicitDeclarativeBindingReturnType()
        {
            var valid = SobakasuCompiler.CompileToUasm(
                @"pub fn abs(value: i32) -> i32
  = extern System.Math.Abs(value)");
            var invalid = SobakasuCompiler.CompileToUasm(
                @"pub fn abs(value: i32) -> string
  = extern System.Math.Abs(value)");
            var invalidVoid = SobakasuCompiler.CompileToUasm(
                @"pub fn log(value: object) -> i32
  = extern UnityEngine.Debug.Log(value)");
            var noOverload = SobakasuCompiler.CompileToUasm(
                @"pub fn abs(value: string)
  = extern System.Math.Abs(value)");

            Assert.That(valid.Success, Is.True, valid.ErrorText);
            Assert.That(invalid.Success, Is.False);
            Assert.That(ContainsCode(invalid.Diagnostics, "SBK2159"), Is.True,
                invalid.ErrorText);
            Assert.That(invalidVoid.Success, Is.False);
            Assert.That(ContainsCode(invalidVoid.Diagnostics, "SBK2159"), Is.True,
                invalidVoid.ErrorText);
            Assert.That(noOverload.Success, Is.False);
            Assert.That(ContainsCode(noOverload.Diagnostics, "SBK2085"), Is.True,
                noOverload.ErrorText);
        }


    }
}
