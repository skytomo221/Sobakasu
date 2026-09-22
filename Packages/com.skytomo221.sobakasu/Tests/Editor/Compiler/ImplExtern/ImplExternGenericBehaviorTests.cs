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
    public class ImplExternGenericBehaviorTests : ImplExternTestFixture
    {


        [Test]
        public void GenericExtern_LowersHiddenSystemTypeAndKeepsOpenSignature()
        {
            var environment = CreateGenericExternEnvironment();
            var signature = UdonExternSignatureFormatter.GetUdonMethodName(
                typeof(SobakasuGenericExternFixture).GetMethod("Echo"));
            var (_, Ir, Uasm) = CompileWithEnvironment(@"
pub impl GenericApi = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture {
  pub fn echo<T>(self, value: T) -> T = extern self.Echo<T>(value)
}
on start {
  let api = extern new Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture();
  let value = api.echo<string>(""ok"");
}", environment);

            var call = FindExternCall(Ir, signature);
            Assert.That(call.Arguments, Has.Count.EqualTo(3));
            Assert.That(call.Arguments[1], Is.TypeOf<IrConstantValue>());
            Assert.That(((IrConstantValue)call.Arguments[1]).Value,
                Is.EqualTo(typeof(string)));
            Assert.That(Uasm, Does.Contain($"EXTERN, \"{signature}\""));
            Assert.That(signature, Does.Contain("__T"));
        }

        [Test]
        public void GenericExtern_ReportsClrConstraintViolationInBinder()
        {
            var binder = Bind(@"
pub impl GenericApi = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture {
  pub fn echo<T>(self, value: T) -> T = extern self.Echo<T>(value)
}
on start {
  let api = extern new Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture();
  let value = api.echo<i32>(1);
}", CreateGenericExternEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "SBK2126"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }


    }
}
