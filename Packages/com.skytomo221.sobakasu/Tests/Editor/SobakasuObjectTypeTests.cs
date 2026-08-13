using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuObjectTypeTests
    {
        [Test]
        public void Binder_ResolvesObjectAsBuiltInSystemObject()
        {
            var program = BindProgram("fn accept(value: object) {}");
            var objectType = program.Functions[0].FunctionSymbol.Parameters[0].Type;

            Assert.That(objectType, Is.SameAs(TypeSymbol.Object));
            Assert.That(objectType.Name, Is.EqualTo("object"));
            Assert.That(objectType.RuntimeQualifiedName, Is.EqualTo("System.Object"));
            Assert.That(objectType.IsReferenceType, Is.True);
            Assert.That(objectType.IsBuiltIn, Is.True);

            var catalog = SobakasuBuiltInEnvironment.Default.ExternCatalog;
            Assert.That(catalog.TryGetTypeSymbol(typeof(object), out var clrType), Is.True);
            Assert.That(clrType, Is.SameAs(TypeSymbol.Object));
            Assert.That(catalog.TryGetTypeSymbol("System.Object", out var qualifiedType), Is.True);
            Assert.That(qualifiedType, Is.SameAs(TypeSymbol.Object));
            Assert.That(catalog.TryGetClrType(TypeSymbol.Object, out var systemType), Is.True);
            Assert.That(systemType, Is.EqualTo(typeof(object)));
            Assert.That(catalog.GetRuntimeTypeSymbol(TypeSymbol.Object), Is.SameAs(TypeSymbol.Object));
        }

        [Test]
        public void Compiler_BoxesSupportedLocalValuesToObject()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on Interact {
  let text: object = ""Hello"";
  let integer: object = 123;
  let number: object = 3.14;
  let enabled: object = true;
  let character: object = 'A';
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("%SystemObject"));
            Assert.That(result.Uasm, Does.Contain("COPY"));
        }

        [Test]
        public void Compiler_BoxesUserFunctionArgumentsThroughSystemObjectSlots()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn consume(value: object) {
  extern UnityEngine.Debug.Log(value);
}

on Interact {
  consume(123);
  consume(""Hello"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("%SystemObject"));
            Assert.That(result.Uasm, Does.Contain("COPY"));
            Assert.That(
                result.Uasm,
                Does.Contain("UnityEngineDebug.__Log__SystemObject__SystemVoid"));
        }

        [TestCase("value")]
        [TestCase("return value;")]
        public void Compiler_BoxesFunctionReturnValues(string returnBody)
        {
            var result = SobakasuCompiler.CompileToUasm(
                $@"fn box_integer(value: i32) -> object {{
  {returnBody}
}}

on Interact {{
  let value: object = box_integer(123);
  extern UnityEngine.Debug.Log(value);
}}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("%SystemObject"));
            Assert.That(result.Uasm, Does.Contain("COPY"));
        }

        [Test]
        public void Compiler_BoxesImplArgumentsAndReturnValues()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl GameObject = extern UnityEngine.GameObject {
  pub fn keep(value: object) -> object { value }
}

on Interact {
  let target = extern UnityEngine.GameObject.Find(""Sobakasu"");
  extern UnityEngine.Debug.Log(target.keep(123));
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("%SystemObject"));
            Assert.That(result.Uasm, Does.Contain("COPY"));
        }

        [Test]
        public void Compiler_RejectsImplicitObjectToConcreteConversion()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on Interact {
  let value: object = 123;
  let integer: i32 = value;
}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK2005"), Is.True, result.ErrorText);
            Assert.That(result.ErrorText, Does.Contain("Cannot convert type 'object' to 'i32'"));
        }

        [Test]
        public void Compiler_CompilesMaybeObjectStateAndExplicitJustAssignment()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"state value: Maybe<object> = Maybe.Nothing;

on Interact {
  let boxed: object = 123;
  value = Maybe.Just(boxed);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("%SystemInt32"));
            Assert.That(result.Uasm, Does.Contain("%SystemObject"));
            Assert.That(result.Uasm, Does.Contain("COPY"));
        }

        [Test]
        public void Compiler_RejectsNonNullObjectStateInitializerUntilHeapPatchingSupportsIt()
        {
            var result = SobakasuCompiler.CompileToUasm("state value: object = 123;");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK2090"), Is.True, result.ErrorText);
            Assert.That(result.ErrorText, Does.Contain("does not support a source initializer"));
        }

        [Test]
        public void Compiler_RejectsSynchronizedObjectState()
        {
            var result = SobakasuCompiler.CompileToUasm("sync state value: object = 123;");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK2061"), Is.True, result.ErrorText);
        }

        [TestCase("on Start { let value: string = null; }")]
        [TestCase("use unity.GameObject; state target: GameObject = null;")]
        [TestCase("on Start { let value: object = null; }")]
        [TestCase("use unity.GameObject; on Start { let values: [GameObject] = [null]; }")]
        public void Compiler_RejectsSourceNullInAllFormerValueContexts(string source)
        {
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK0007"), Is.True,
                result.ErrorText);
            Assert.That(result.ErrorText, Does.Contain("Maybe<T>"));
        }

        [Test]
        public void Compiler_DoesNotDynamicallyResolveMembersFromBoxedRuntimeType()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl GameObject = extern UnityEngine.GameObject {
  pub fn SetActive(active: bool) { extern self.SetActive(active); }
}

fn invoke(target: GameObject) {
  let value: object = target;
  value.SetActive(true);
}

on Interact {}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK2003"), Is.True, result.ErrorText);
            Assert.That(result.ErrorText, Does.Contain("'object' does not contain a member"));
        }

        [Test]
        public void Compiler_RejectsU0ToObjectConversion()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn consume(value: object) {}
fn no_value() {}

on Interact {
  consume(no_value());
}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK2005"), Is.True, result.ErrorText);
        }

        [Test]
        public void StandardLibrary_DebugFunctionsAcceptObjectWithoutUse()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on Interact {
  log(""Hello"");
  log(123);
  warning(3.14);
  error(true);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(
                result.Uasm,
                Does.Contain("UnityEngineDebug.__Log__SystemObject__SystemVoid"));
            Assert.That(
                result.Uasm,
                Does.Contain("UnityEngineDebug.__LogWarning__SystemObject__SystemVoid"));
            Assert.That(
                result.Uasm,
                Does.Contain("UnityEngineDebug.__LogError__SystemObject__SystemVoid"));
        }

        private static BoundProgram BindProgram(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));

            var binder = new SobakasuBinder();
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty, Format(binder.Diagnostics.Diagnostics));
            return program;
        }

        private static bool ContainsCode(
            IReadOnlyList<Diagnostic> diagnostics,
            string expectedCode)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == expectedCode)
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
    }
}
