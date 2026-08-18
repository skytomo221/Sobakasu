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

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public static class SobakasuExternAbiFixture
    {
        public static void RefOnly(ref int value)
        {
            value++;
        }

        public static void OutOnly(out int value)
        {
            value = 42;
        }

        public static bool ReturnAndOut(out int value)
        {
            value = 42;
            return true;
        }

        public static int Mixed(
            int normal,
            ref int value,
            out string text,
            ref bool flag)
        {
            value += normal;
            text = value.ToString();
            flag = !flag;
            return normal;
        }
    }

    public class SobakasuImplExternTests
    {
        private const string MaybeDefinition = @"
enum Maybe<T> {
  Nothing,
  Just(T),
}
";
        private const string ProjectedTryGetSignature =
            "TestApi.__TryGet__TestOwnerRef__SystemBoolean";
        private const string ProjectedMixedSignature =
            "TestApi.__Mixed__SystemInt32Ref_TestOwnerRef_SystemStringRef__SystemInt32";
        private const string ProjectedValiditySignature =
            "VRCSDKBaseUtilities.__IsValid__TestOwner__SystemBoolean";
        private const string ProjectedConstructorMaybeSignature =
            "TestFoo.__ctor__TestOwnerRef__TestFoo";
        private const string ExternAbiBindingsSource = @"
fn ref_only(value: i32) -> i32
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.RefOnly(
      ref i32 value);
fn out_only() -> i32
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.OutOnly(
      out i32 value);
fn return_and_out() -> (bool, i32)
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.ReturnAndOut(
      out i32 value);
fn mixed(normal: i32, value: i32, flag: bool)
    -> (i32, i32, string, bool)
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.Mixed(
      i32 normal, ref i32 value, out string text, ref bool flag);
";

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
        public void Lexer_RecognizesImplExternSelfStaticAndOperatorNameTokens()
        {
            var tokens = LexAll("impl extern self Self static @+ @- @! @~");

            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.ImplKeyword));
            Assert.That(tokens[1].Kind, Is.EqualTo(SyntaxKind.ExternKeyword));
            Assert.That(tokens[2].Kind, Is.EqualTo(SyntaxKind.SelfKeyword));
            Assert.That(tokens[3].Kind, Is.EqualTo(SyntaxKind.SelfTypeKeyword));
            Assert.That(tokens[4].Kind, Is.EqualTo(SyntaxKind.StaticKeyword));
            Assert.That(tokens[5].Kind, Is.EqualTo(SyntaxKind.AtToken));
            Assert.That(tokens[6].Kind, Is.EqualTo(SyntaxKind.PlusToken));
            Assert.That(tokens[7].Kind, Is.EqualTo(SyntaxKind.AtToken));
            Assert.That(tokens[8].Kind, Is.EqualTo(SyntaxKind.MinusToken));
            Assert.That(tokens[9].Kind, Is.EqualTo(SyntaxKind.AtToken));
            Assert.That(tokens[10].Kind, Is.EqualTo(SyntaxKind.BangToken));
            Assert.That(tokens[11].Kind, Is.EqualTo(SyntaxKind.AtToken));
            Assert.That(tokens[12].Kind, Is.EqualTo(SyntaxKind.TildeToken));
        }

        [Test]
        public void Parser_ParsesExternalAndAdditionalImplMethods()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"pub impl GameObject = extern UnityEngine.GameObject {
  pub fn set_active(active: bool) { extern self.SetActive(active); }
  pub fn active? -> bool { extern self.activeSelf }
  pub static fn find(name: string) -> Self { extern UnityEngine.GameObject.Find(name) }
}
impl GameObject {
  pub fn @- -> Self { extern -self }
  pub fn +(rhs: Self) -> Self { extern self + rhs }
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(2));

            var external = syntax.Members[0] as ImplDeclarationSyntax;
            Assert.That(external, Is.Not.Null);
            Assert.That(external.PubKeyword, Is.Not.Null);
            Assert.That(external.IsExternalBinding, Is.True);
            Assert.That(external.TargetType.GetText(), Is.EqualTo("GameObject"));
            Assert.That(external.ExternalTypeName.GetText(),
                Is.EqualTo("UnityEngine.GameObject"));
            Assert.That(external.Methods, Has.Count.EqualTo(3));
            Assert.That(external.Methods[1].Name, Is.EqualTo("active?"));
            Assert.That(external.Methods[1].OpenParenToken, Is.Null);
            Assert.That(external.Methods[2].StaticKeyword, Is.Not.Null);

            var additional = syntax.Members[1] as ImplDeclarationSyntax;
            Assert.That(additional, Is.Not.Null);
            Assert.That(additional.IsExternalBinding, Is.False);
            Assert.That(additional.Methods[0].Name, Is.EqualTo("@-"));
            Assert.That(additional.Methods[0].Parameters, Is.Empty);
            Assert.That(additional.Methods[1].Name, Is.EqualTo("+"));
            Assert.That(additional.Methods[1].Parameters, Has.Count.EqualTo(1));
        }

        [TestCase("extern UnityEngine.Debug.Log(\"hello\");")]
        [TestCase("extern target.SetActive(true);")]
        [TestCase("extern target.activeSelf;")]
        [TestCase("extern target.name = \"name\";")]
        [TestCase("extern new UnityEngine.Vector3(1.0f32, 2.0f32, 3.0f32);")]
        [TestCase("extern -value;")]
        [TestCase("extern value + value;")]
        [TestCase("let next = (extern target.layer) + 1;")]
        public void Parser_ParsesSupportedExternExpressionShapes(string statement)
        {
            var parser = new SobakasuParser(SourceText.From(
                $"on interact {{ {statement} }}"));
            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
        }

        [Test]
        public void Parser_RecoversAfterInvalidAtOperatorName()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"impl i32 {
  fn @invalid -> i32 { 0 }
  fn valid -> i32 { 1 }
}
on interact {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members[^1], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_RecoversAfterInvalidExternExpression()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"on interact { extern ; }
on update {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members, Has.Count.EqualTo(2));
            Assert.That(syntax.Members[1], Is.TypeOf<EventDeclarationSyntax>());
        }

        [TestCase("impl Missing {}", "SBK2065")]
        [TestCase("pub impl Missing = extern Does.Not.Exist {}", "SBK2066")]
        [TestCase("pub impl Console = extern System.Console {}", "SBK2067")]
        [TestCase("pub impl i32 = extern System.Int32 {}", "SBK2070")]
        [TestCase("pub impl Integer = extern System.Int32 {}", "SBK2070")]
        [TestCase("pub impl sample.Item = extern UnityEngine.GameObject {}", "SBK2089")]
        [TestCase(
            "pub impl Item = extern UnityEngine.GameObject {} pub impl Item = extern UnityEngine.GameObject {}",
            "SBK2068")]
        [TestCase(
            "pub impl First = extern UnityEngine.GameObject {} pub impl Second = extern UnityEngine.GameObject {}",
            "SBK2069")]
        [TestCase("pub impl i32 {}", "SBK2088")]
        [TestCase(
            "impl i32 { fn custom(value: i64) {} fn custom(value: i64) {} }",
            "SBK2071")]
        [TestCase("impl i32 { fn bad(self: Self) {} }", "SBK2072")]
        [TestCase("impl i32 { static fn bad { self; } }", "SBK2073")]
        [TestCase("impl i32 { static fn +(rhs: i64) -> i64 { rhs } }", "SBK2075")]
        [TestCase("impl i32 { fn +(rhs: i32) -> i32 { rhs } }", "SBK2080")]
        [TestCase("impl i32 { fn <(rhs: i64) -> i32 { 0 } }", "SBK2079")]
        [TestCase("impl i32 { fn &&(rhs: bool) -> bool { rhs } }", "SBK2076")]
        [TestCase("impl i32 { fn @-(value: i32) -> i32 { value } }", "SBK2077")]
        [TestCase(
            "impl i32 { fn +(first: i64, second: i64) -> i64 { first } }",
            "SBK2078")]
        public void Binder_ReportsImplAndOperatorDiagnostics(
            string source,
            string expectedCode)
        {
            var binder = Bind(source);

            Assert.That(ContainsCode(binder.Diagnostics.Diagnostics, expectedCode), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

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
        public void Compiler_ResolvesStaticFunctionOverloads()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl GameObject = extern UnityEngine.GameObject {
  static fn create(value: i32) -> i32 { 10 }
  static fn create(value: string) -> i32 { 20 }
}
on interact {
  extern UnityEngine.Debug.Log(GameObject.create(1));
  extern UnityEngine.Debug.Log(GameObject.create(""value""));
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("UnityEngineDebug.__Log"));
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
        public void Compiler_CompilesExternalGameObjectBindingAndPropertyAccess()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl GameObject = extern UnityEngine.GameObject {
  pub fn set_active(active: bool) {
    extern self.SetActive(active);
  }

  pub fn active? -> bool {
    extern self.activeSelf
  }

  pub fn set_name(value: string) {
    extern self.name = value;
  }
}

on interact {
  let target = extern UnityEngine.GameObject.Find(""Sobakasu"");
  target.set_active(true);
  target.set_name(""Sobakasu"");
  extern UnityEngine.Debug.Log(target.active?);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("UnityEngineGameObject.__SetActive"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineGameObject.__get_activeSelf"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineObject.__set_name"));
        }

        [Test]
        public void Compiler_CompilesVector3ConstructorMethodsAndOperators()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl Vector3 = extern UnityEngine.Vector3 {
  pub static fn new(x: f32, y: f32, z: f32) -> Self {
    extern new Self(x, y, z)
  }

  pub static fn zero -> Self {
    extern Self.zero
  }

  pub fn +(rhs: Self) -> Self {
    extern self + rhs
  }

  pub fn @- -> Self {
    extern -self
  }

  pub fn magnitude -> f32 {
    extern self.magnitude
  }

  pub fn x -> f32 {
    extern self.x
  }

  pub fn set_x(value: f32) {
    extern self.x = value;
  }
}

on interact {
  let mut value = Vector3.new(1.0f32, 2.0f32, 3.0f32);
  value.set_x(4.0f32);
  let sum = value + Vector3.zero;
  let inverse = -sum;
  extern UnityEngine.Debug.Log(inverse.magnitude);
  extern UnityEngine.Debug.Log(value.x);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__ctor"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__op_Addition"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__op_UnaryNegation"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__get_magnitude"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__get_x"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__set_x"));
        }

        [Test]
        public void Compiler_CompilesPrimitiveImplAndRuntimeTypeMapping()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"impl i32 {
  pub fn abs -> Self {
    extern System.Math.Abs(self)
  }

  pub fn to_f32 -> f32 {
    extern System.Convert.ToSingle(self)
  }

  pub fn even? -> bool {
    self % 2 == 0
  }
}

on interact {
  let number = (-10).abs;
  let converted = number.to_f32;
  extern UnityEngine.Debug.Log(number.even?);
  extern UnityEngine.Debug.Log(converted);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("SystemMath.__Abs__SystemInt32"));
            Assert.That(result.Uasm, Does.Contain("SystemConvert.__ToSingle__SystemInt32"));
        }

        [Test]
        public void Compiler_AllowsNonBuiltInOperatorSignatureOnBuiltInType()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"impl bool {
  pub fn <(rhs: bool) -> bool {
    !self && rhs
  }

  pub fn @- -> bool {
    !self
  }
}

on interact {
  extern UnityEngine.Debug.Log(false < true);
  extern UnityEngine.Debug.Log(-false);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
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
        public void Lowerer_EvaluatesMethodReceiverOnce()
        {
            var result = SobakasuCompiler.CompileToUasm(
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
            var result = SobakasuCompiler.CompileToUasm(
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

        [TestCase("pub fn foo = extern Foo.Bar()", false, false)]
        [TestCase("pub fn foo -> SomeType = extern Foo.Bar()", true, false)]
        [TestCase("pub fn foo(value: i32) = extern Foo.Bar(value)", false, false)]
        [TestCase("pub fn foo(value: i32) -> SomeType = extern Foo.Bar(value)", true, false)]
        [TestCase("pub fn foo(value: string) = maybe extern Foo.Find(value)", false, true)]
        public void Parser_ParsesDeclarativeExternBindings(
            string source,
            bool hasReturnType,
            bool isMaybe)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = syntax.Members[0] as FunctionDeclarationSyntax;
            Assert.That(function, Is.Not.Null);
            Assert.That(function.Body, Is.Null);
            Assert.That(function.ExternalBinding, Is.Not.Null);
            Assert.That(function.ExternalBinding.IsMaybe, Is.EqualTo(isMaybe));
            Assert.That(function.ReturnTypeAnnotation != null, Is.EqualTo(hasReturnType));
        }

        [Test]
        public void Parser_RejectsGeneralExpressionBodiedFunctionAndRecovers()
        {
            var parser = new SobakasuParser(SourceText.From(
                "pub fn bad = 123 pub fn good { }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1038"), Is.True,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(2));
            Assert.That(((FunctionDeclarationSyntax)syntax.Members[1]).Name,
                Is.EqualTo("good"));
        }

        [Test]
        public void Compiler_InfersRawBindingReturnsAndPublishesResolvedMetadata()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub fn abs(value: i32)
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

        [Test]
        public void LexerAndParser_ReserveRefOutOnlyForExplicitExternAbiSignatures()
        {
            var tokens = LexAll("ref out");
            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.RefKeyword));
            Assert.That(tokens[1].Kind, Is.EqualTo(SyntaxKind.OutKeyword));

            var parser = new SobakasuParser(SourceText.From(
                @"fn mixed(normal: i32, value: i32, flag: bool)
    -> (i32, i32, string, bool)
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.Mixed(
      i32 normal, ref i32 value, out string text, ref bool flag);"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = syntax.Members[0] as FunctionDeclarationSyntax;
            Assert.That(function.ExternalBinding.AbiSignature, Is.Not.Null);
            Assert.That(function.ExternalBinding.AbiSignature.Parameters,
                Has.Count.EqualTo(4));
            Assert.That(function.ExternalBinding.AbiSignature.Parameters[1].Modifier.Kind,
                Is.EqualTo(SyntaxKind.RefKeyword));
            Assert.That(function.ExternalBinding.AbiSignature.Parameters[2].Modifier.Kind,
                Is.EqualTo(SyntaxKind.OutKeyword));

            var ordinary = new SobakasuParser(SourceText.From(
                "fn invalid(ref value: i32) {}"));
            ordinary.ParseCompilationUnit();
            Assert.That(ordinary.Diagnostics.HasErrors, Is.True);
        }

        [Test]
        public void Parser_ParsesMaybeOutForMethodAndConstructorAbiSignatures()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"fn single() -> Maybe<Test.Owner>
  = extern Test.Api.TryGet(maybe out Test.Owner owner)
fn pair() -> (bool, Maybe<Test.Owner>)
  = extern Test.Api.TryGet(maybe out Test.Owner owner)
pub impl Foo = extern Test.Foo {
  pub static fn create() -> (Self, Maybe<Test.Owner>)
    = extern new Self(maybe out Test.Owner owner)
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var single = (FunctionDeclarationSyntax)syntax.Members[0];
            Assert.That(single.ExternalBinding.AbiSignature.Parameters[0].IsMaybe,
                Is.True);
            Assert.That(
                single.ExternalBinding.AbiSignature.Parameters[0].Modifier.Kind,
                Is.EqualTo(SyntaxKind.OutKeyword));
            var pair = (FunctionDeclarationSyntax)syntax.Members[1];
            Assert.That(pair.ReturnTypeAnnotation.Type.GetText(),
                Is.EqualTo("(bool, Maybe<Test.Owner>)"));

            var impl = (ImplDeclarationSyntax)syntax.Members[2];
            var constructor = impl.Methods[0].ExternalBinding.AbiSignature;
            Assert.That(constructor.IsConstructor, Is.True);
            Assert.That(constructor.ConstructorType.GetText(), Is.EqualTo("Self"));
            Assert.That(constructor.Parameters[0].IsMaybe, Is.True);
        }

        [TestCase("maybe ref Test.Owner owner")]
        [TestCase("maybe Test.Owner owner")]
        public void Parser_RejectsMaybeOnNonOutAbiParameters(string parameter)
        {
            var parser = new SobakasuParser(SourceText.From(
                $"fn invalid() = extern Test.Api.TryGet({parameter})"));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1039"),
                Is.True, Format(parser.Diagnostics.Diagnostics));
        }

        [Test]
        public void Parser_DoesNotIntroduceMaybeOutForOrdinaryFunctionParameters()
        {
            var parser = new SobakasuParser(SourceText.From(
                "fn invalid(maybe out Test.Owner owner) {}"));
            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
        }

        [Test]
        public void ExternCatalog_AdaptsRefOutToLogicalInputsAndTupleOutputs()
        {
            var environment = CreateExternAbiEnvironment();
            var source = ExternAbiBindingsSource + @"
on start {
  let ref_value = ref_only(1);
  let out_value = out_only();
  let (returned, updated, text, flag) = mixed(2, 3, true);
  let (success, returned_out) = return_and_out();
}";
            var compilation = CompileWithEnvironment(source, environment);

            var refOnly = FindExternalMethod(compilation.Program, "ref_only");
            Assert.That(refOnly.Parameters.Select(parameter => parameter.Type),
                Is.EqualTo(new[] { TypeSymbol.I32 }));
            Assert.That(refOnly.ReturnType, Is.SameAs(TypeSymbol.I32));
            Assert.That(refOnly.AbiReturnType, Is.SameAs(TypeSymbol.Unit));
            Assert.That(refOnly.AbiParameters.Select(parameter => parameter.PassingMode),
                Is.EqualTo(new[] { ExternParameterPassingMode.Ref }));

            var outOnly = FindExternalMethod(compilation.Program, "out_only");
            Assert.That(outOnly.Parameters, Is.Empty);
            Assert.That(outOnly.ReturnType, Is.SameAs(TypeSymbol.I32));
            Assert.That(outOnly.AbiParameters[0].LogicalInputOrdinal, Is.EqualTo(-1));
            Assert.That(outOnly.AbiParameters[0].PassingMode,
                Is.EqualTo(ExternParameterPassingMode.Out));

            var returnAndOut = FindExternalMethod(
                compilation.Program,
                "return_and_out");
            Assert.That(returnAndOut.ReturnType.TupleElementTypes,
                Is.EqualTo(new[] { TypeSymbol.Bool, TypeSymbol.I32 }));

            var mixed = FindExternalMethod(compilation.Program, "mixed");
            Assert.That(mixed.Parameters.Select(parameter => parameter.Type),
                Is.EqualTo(new[] { TypeSymbol.I32, TypeSymbol.I32, TypeSymbol.Bool }));
            Assert.That(mixed.ReturnType.TupleElementTypes,
                Is.EqualTo(new[]
                {
                    TypeSymbol.I32,
                    TypeSymbol.I32,
                    TypeSymbol.String,
                    TypeSymbol.Bool
                }));
            Assert.That(mixed.AbiParameters.Select(parameter => parameter.PassingMode),
                Is.EqualTo(new[]
                {
                    ExternParameterPassingMode.Normal,
                    ExternParameterPassingMode.Ref,
                    ExternParameterPassingMode.Out,
                    ExternParameterPassingMode.Ref
                }));

            var mixedCall = FindExternCall(compilation.Ir, mixed.ExternSignature);
            Assert.That(mixedCall.Arguments.Select(argument => argument.Type),
                Is.EqualTo(new[]
                {
                    TypeSymbol.I32,
                    TypeSymbol.I32,
                    TypeSymbol.String,
                    TypeSymbol.Bool
                }));
            Assert.That(mixedCall.Result.Type, Is.SameAs(TypeSymbol.I32));
            Assert.That(HasCopyBeforeCall(
                compilation.Ir,
                mixedCall,
                mixedCall.Arguments[1]), Is.True,
                "ref input must be copied into its physical ABI slot");
            Assert.That(HasCopyBeforeCall(
                compilation.Ir,
                mixedCall,
                mixedCall.Arguments[2]), Is.False,
                "out output must not be initialized before the extern call");
            Assert.That(HasCopyBeforeCall(
                compilation.Ir,
                mixedCall,
                mixedCall.Arguments[3]), Is.True,
                "ref input must be copied into its physical ABI slot");

            Assert.That(compilation.Uasm, Does.Contain(mixed.ExternSignature));
            Assert.That(compilation.Uasm, Does.Not.Contain("SystemValueTuple"));
        }

        [Test]
        public void Binder_ValidatesExplicitExternAbiModesAndLogicalReturnType()
        {
            var environment = CreateExternAbiEnvironment();
            var wrongMode = Bind(
                @"fn value(value: i32) -> i32
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.RefOnly(
      i32 value);",
                environment);
            var wrongReturn = Bind(
                @"fn value(value: i32) -> string
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.RefOnly(
      ref i32 value);",
                environment);
            var outRequiredAsInput = Bind(
                @"fn value(value: i32) -> i32
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.OutOnly(
      out i32 output);",
                environment);
            var wrongPhysicalType = Bind(
                @"fn value(value: string) -> string
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.RefOnly(
      ref string value);",
                environment);
            var wrongOutputOrder = Bind(
                @"fn value(normal: i32, value: i32, flag: bool)
    -> (i32, string, i32, bool)
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.Mixed(
      i32 normal, ref i32 value, out string text, ref bool flag);",
                environment);

            Assert.That(wrongMode.Diagnostics.HasErrors, Is.True);
            Assert.That(ContainsCode(wrongMode.Diagnostics.Diagnostics, "SBK2085"),
                Is.True, Format(wrongMode.Diagnostics.Diagnostics));
            Assert.That(ContainsCode(wrongReturn.Diagnostics.Diagnostics, "SBK2159"),
                Is.True, Format(wrongReturn.Diagnostics.Diagnostics));
            Assert.That(ContainsCode(
                    outRequiredAsInput.Diagnostics.Diagnostics,
                    "SBK2085"),
                Is.True, Format(outRequiredAsInput.Diagnostics.Diagnostics));
            Assert.That(ContainsCode(
                    wrongPhysicalType.Diagnostics.Diagnostics,
                    "SBK2085"),
                Is.True, Format(wrongPhysicalType.Diagnostics.Diagnostics));
            Assert.That(ContainsCode(
                    wrongOutputOrder.Diagnostics.Diagnostics,
                    "SBK2159"),
                Is.True, Format(wrongOutputOrder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_SeparatesMaybeOutPhysicalAndLogicalSignatures()
        {
            var environment = CreateProjectionEnvironment();
            var compilation = CompileWithEnvironment(
                MaybeDefinition + @"
fn raw() -> (bool, Test.Owner)
  = extern Test.Api.TryGet(out Test.Owner owner)
fn projected()
  = extern Test.Api.TryGet(maybe out Test.Owner owner)
on start {
  let raw_value = raw();
  let projected_value = projected();
}",
                environment);

            var raw = FindExternalMethod(compilation.Program, "raw");
            var projected = FindExternalMethod(compilation.Program, "projected");
            Assert.That(projected.ExternSignature, Is.EqualTo(raw.ExternSignature));
            Assert.That(projected.AbiParameters[0].PassingMode,
                Is.EqualTo(ExternParameterPassingMode.Out));
            Assert.That(projected.AbiParameters[0].Type,
                Is.SameAs(raw.AbiParameters[0].Type));
            Assert.That(raw.AbiParameters[0].LogicalOutputProjection,
                Is.EqualTo(ExternLogicalOutputProjection.Raw));
            Assert.That(projected.AbiParameters[0].LogicalOutputProjection,
                Is.EqualTo(ExternLogicalOutputProjection.Maybe));
            Assert.That(projected.ReturnType.TupleElementTypes.Select(type => type.Name),
                Is.EqualTo(new[] { "bool", "Maybe<Owner>" }));
            Assert.That(CountExternCalls(
                    compilation.Ir,
                    ProjectedTryGetSignature),
                Is.EqualTo(2),
                "Each of the two wrapper invocations must call the same physical overload once.");
            Assert.That(CountExternCalls(
                    compilation.Ir,
                    ProjectedValiditySignature),
                Is.EqualTo(1));
            Assert.That(compilation.Uasm, Does.Not.Contain("SystemValueTuple"));
        }

        [Test]
        public void Binder_RejectsMaybeOutForValueTypesAndReturnMismatches()
        {
            var environment = CreateProjectionEnvironment();
            var invalidType = Bind(
                MaybeDefinition + @"
fn invalid() -> Maybe<i32>
  = extern Test.Api.OutInt(maybe out i32 value)",
                environment);
            var invalidReturn = Bind(
                MaybeDefinition + @"
fn invalid() -> Test.Owner
  = extern Test.Api.TryGet(maybe out Test.Owner owner)",
                environment);

            Assert.That(ContainsCode(
                    invalidType.Diagnostics.Diagnostics,
                    "SBK2164"),
                Is.True, Format(invalidType.Diagnostics.Diagnostics));
            Assert.That(ContainsCode(
                    invalidReturn.Diagnostics.Diagnostics,
                    "SBK2159"),
                Is.True, Format(invalidReturn.Diagnostics.Diagnostics));
        }

        [Test]
        public void IrLowerer_ProjectsMaybeOutOnceAndPreservesOutputOrder()
        {
            var environment = CreateProjectionEnvironment();
            var compilation = CompileWithEnvironment(
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

            var method = FindExternalMethod(compilation.Program, "mixed");
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

            var call = FindExternCall(compilation.Ir, ProjectedMixedSignature);
            Assert.That(call.Arguments.Select(argument => argument.Type.Name),
                Is.EqualTo(new[] { "i32", "Owner", "string" }));
            Assert.That(CountExternCalls(compilation.Ir, ProjectedMixedSignature),
                Is.EqualTo(1));
            Assert.That(CountExternCalls(compilation.Ir, ProjectedValiditySignature),
                Is.EqualTo(1));
            Assert.That(compilation.Uasm, Does.Not.Contain("SystemValueTuple"));
        }

        [Test]
        public void ConstructorBindings_UseSelfThenRefOutProjectionOrder()
        {
            var environment = CreateProjectionEnvironment();
            var compilation = CompileWithEnvironment(
                MaybeDefinition + @"
pub impl Foo = extern Test.Foo {
  pub static fn normal(value: i32) -> Self
    = extern new Self(i32 value)
  pub static fn by_ref(value: i32) -> (Self, i32)
    = extern new Self(ref i32 value)
  pub static fn by_out() -> (Self, string)
    = extern new Self(out string name)
  pub static fn mixed(value: i32, weight: f32)
      -> (Self, i32, string, f32)
    = extern new Self(ref i32 value, out string name, ref f32 weight)
  pub static fn optional_owner() -> (Self, Maybe<Test.Owner>)
    = extern new Self(maybe out Test.Owner owner)
}
on start {
  let normal = Foo.normal(1);
  let (by_ref, value) = Foo.by_ref(1);
  let (by_out, name) = Foo.by_out();
  let (mixed, next_value, next_name, next_weight) = Foo.mixed(1, 2.0f32);
  let (optional_owner, owner) = Foo.optional_owner();
}",
                environment);

            var normal = FindExternalMethod(compilation.Program, "normal");
            var byRef = FindExternalMethod(compilation.Program, "by_ref");
            var byOut = FindExternalMethod(compilation.Program, "by_out");
            var mixed = FindExternalMethod(compilation.Program, "mixed");
            var optional = FindExternalMethod(compilation.Program, "optional_owner");

            Assert.That(normal.ReturnType.Name, Is.EqualTo("Foo"));
            Assert.That(byRef.Parameters.Select(parameter => parameter.Type.Name),
                Is.EqualTo(new[] { "i32" }));
            Assert.That(byRef.ReturnType.TupleElementTypes.Select(type => type.Name),
                Is.EqualTo(new[] { "Foo", "i32" }));
            Assert.That(byOut.Parameters, Is.Empty);
            Assert.That(byOut.ReturnType.TupleElementTypes.Select(type => type.Name),
                Is.EqualTo(new[] { "Foo", "string" }));
            Assert.That(mixed.Parameters.Select(parameter => parameter.Type.Name),
                Is.EqualTo(new[] { "i32", "f32" }));
            Assert.That(mixed.ReturnType.TupleElementTypes.Select(type => type.Name),
                Is.EqualTo(new[] { "Foo", "i32", "string", "f32" }));
            Assert.That(optional.ReturnType.TupleElementTypes.Select(type => type.Name),
                Is.EqualTo(new[] { "Foo", "Maybe<Owner>" }));
            Assert.That(optional.AbiParameters[0].LogicalOutputProjection,
                Is.EqualTo(ExternLogicalOutputProjection.Maybe));
            Assert.That(CountExternCalls(
                    compilation.Ir,
                    ProjectedConstructorMaybeSignature),
                Is.EqualTo(1));
            Assert.That(CountExternCalls(
                    compilation.Ir,
                    ProjectedValiditySignature),
                Is.EqualTo(1));
            Assert.That(compilation.Uasm, Does.Not.Contain("SystemValueTuple"));
        }

        [Test]
        public void Compiler_LowersMaybeExternOnceThroughExistingValidityPolicy()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use unity.GameObject;

pub fn find_one(name: string) -> Maybe<GameObject>
  = maybe extern UnityEngine.GameObject.Find(name)

on interact {
  let found = find_one(""Sobakasu"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(
                CountOccurrences(result.Uasm, "UnityEngineGameObject.__Find"),
                Is.EqualTo(1));
            Assert.That(
                CountOccurrences(result.Uasm, "VRCSDKBaseUtilities.__IsValid"),
                Is.EqualTo(1));
            var metadata = result.ExternalBindings.Single(binding =>
                binding.SobakasuName == "find_one");
            Assert.That(metadata.SobakasuReturnType,
                Does.Contain("Maybe<unity.GameObject>"));
            Assert.That(metadata.ReturnMode,
                Is.EqualTo(ExternalBindingReturnMode.Maybe));

            var asset = CreateProgramAsset();
            Assert.That(
                asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True,
                assemblyError);
        }

        [Test]
        public void Compiler_DistinguishesRawAndMaybeBindings()
        {
            var raw = SobakasuCompiler.CompileToUasm(
                @"use unity.GameObject;
pub fn find_raw(name: string)
  = extern UnityEngine.GameObject.Find(name)");
            var unsupportedMaybe = SobakasuCompiler.CompileToUasm(
                @"pub fn abs(value: i32)
  = maybe extern System.Math.Abs(value)");
            var mismatchedMaybe = SobakasuCompiler.CompileToUasm(
                @"use unity.GameObject;
pub fn find_bad(name: string) -> Maybe<i32>
  = maybe extern UnityEngine.GameObject.Find(name)");

            Assert.That(raw.Success, Is.True, raw.ErrorText);
            var rawMetadata = raw.ExternalBindings.Single(binding =>
                binding.SobakasuName == "find_raw");
            Assert.That(rawMetadata.ReturnMode,
                Is.EqualTo(ExternalBindingReturnMode.Raw));
            Assert.That(rawMetadata.SobakasuReturnType,
                Does.Not.Contain("Maybe"));

            Assert.That(unsupportedMaybe.Success, Is.False);
            Assert.That(ContainsCode(unsupportedMaybe.Diagnostics, "SBK2158"), Is.True,
                unsupportedMaybe.ErrorText);
            Assert.That(mismatchedMaybe.Success, Is.False);
            Assert.That(ContainsCode(mismatchedMaybe.Diagnostics, "SBK2160"), Is.True,
                mismatchedMaybe.ErrorText);
        }

        [Test]
        public void Compiler_BlockAndDeclarativeExternWrappersSelectSameUdonSignature()
        {
            var block = SobakasuCompiler.CompileToUasm(
                @"fn abs(value: i32) -> i32 {
  extern System.Math.Abs(value)
}
on interact { extern UnityEngine.Debug.Log(abs(-1)); }");
            var binding = SobakasuCompiler.CompileToUasm(
                @"fn abs(value: i32) -> i32
  = extern System.Math.Abs(value)
on interact { extern UnityEngine.Debug.Log(abs(-1)); }");

            Assert.That(block.Success, Is.True, block.ErrorText);
            Assert.That(binding.Success, Is.True, binding.ErrorText);
            const string signature =
                "SystemMath.__Abs__SystemInt32__SystemInt32";
            Assert.That(block.Uasm, Does.Contain(signature));
            Assert.That(binding.Uasm, Does.Contain(signature));
        }

        [Test]
        public void StandardLibrary_UsesDeclarativeStaticInstanceAndMaybeBindings()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use math.sqrt;
use unity.GameObject;

on interact {
  extern UnityEngine.Debug.Log(sqrt(9.0f64));
  let optional = GameObject.find(""Sobakasu"");
  let target = extern UnityEngine.GameObject.Find(""Sobakasu"");
  target.set_active(true);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm,
                Does.Contain("SystemMath.__Sqrt__SystemDouble__SystemDouble"));
            Assert.That(result.Uasm,
                Does.Contain("UnityEngineGameObject.__Find__SystemString__UnityEngineGameObject"));
            Assert.That(result.Uasm,
                Does.Contain("VRCSDKBaseUtilities.__IsValid__SystemObject__SystemBoolean"));
            Assert.That(result.Uasm,
                Does.Contain("UnityEngineGameObject.__SetActive__SystemBoolean__SystemVoid"));

            Assert.That(result.ExternalBindings.Any(binding =>
                binding.DeclaringModule == "math" &&
                binding.SobakasuName == "sqrt"), Is.True);
            Assert.That(result.ExternalBindings.Any(binding =>
                binding.DeclaringModule == "unity" &&
                binding.SobakasuName == "GameObject.find"), Is.True);
            Assert.That(result.ExternalBindings.Any(binding =>
                binding.DeclaringModule == "unity" &&
                binding.SobakasuName == "GameObject.set_active"), Is.True);
        }

        [Test]
        public void StandardLibrary_AdaptsVector3SmoothDampRefOutput()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use unity.Vector3;

on start {
  let current = extern new UnityEngine.Vector3(0.0f32, 0.0f32, 0.0f32);
  let target = extern new UnityEngine.Vector3(1.0f32, 2.0f32, 3.0f32);
  let velocity = extern new UnityEngine.Vector3(0.0f32, 0.0f32, 0.0f32);
  let (position, next_velocity) = Vector3.smooth_damp(
      current, target, velocity, 0.25f32, 100.0f32, 0.016f32);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            var metadata = result.ExternalBindings.Single(binding =>
                binding.DeclaringModule == "unity" &&
                binding.SobakasuName == "Vector3.smooth_damp");
            Assert.That(metadata.SobakasuParameterTypes.Count, Is.EqualTo(6));
            Assert.That(metadata.SobakasuReturnType,
                Does.Contain("Vector3").And.Contain(","));
            Assert.That(metadata.ExternalParameterModes,
                Is.EqualTo(new[]
                {
                    ExternalParameterPassingMode.Normal,
                    ExternalParameterPassingMode.Normal,
                    ExternalParameterPassingMode.Ref,
                    ExternalParameterPassingMode.Normal,
                    ExternalParameterPassingMode.Normal,
                    ExternalParameterPassingMode.Normal
                }));
            Assert.That(result.Uasm, Does.Contain(".__SmoothDamp"));
            Assert.That(result.Uasm, Does.Not.Contain("SystemValueTuple"));
        }

        [Test]
        public void UdonAssembler_AcceptsResolvedImplAndExternProgram()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl GameObject = extern UnityEngine.GameObject {
  pub fn set_name(value: string) { extern self.name = value; }
}

pub impl Vector3 = extern UnityEngine.Vector3 {
  pub static fn new(x: f32, y: f32, z: f32) -> Self {
    extern new Self(x, y, z)
  }

  pub fn +(rhs: Self) -> Self { extern self + rhs }
  pub fn x -> f32 { extern self.x }
  pub fn set_x(value: f32) { extern self.x = value; }
}

on interact {
  let target = extern UnityEngine.GameObject.Find(""Sobakasu"");
  target.set_name(""Sobakasu"");
  let mut value = Vector3.new(1.0f32, 2.0f32, 3.0f32);
  value.set_x(4.0f32);
  let sum = value + value;
  extern UnityEngine.Debug.Log(sum.x);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            var asset = CreateProgramAsset();
            Assert.That(
                asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True,
                assemblyError);
        }

        private static SobakasuCompilationEnvironment CreateExternAbiEnvironment()
        {
            var signatures = typeof(SobakasuExternAbiFixture)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.DeclaringType == typeof(SobakasuExternAbiFixture))
                .Select(UdonExternSignatureFormatter.GetUdonMethodName)
                .ToArray();
            var catalog = new ReflectionExternCatalogBuilder(
                new UdonExposedNodeCache(signatures))
                .BuildCatalog(new[] { typeof(SobakasuExternAbiFixture).Namespace });
            return new SobakasuCompilationEnvironment(catalog);
        }

        private static SobakasuCompilationEnvironment CreateProjectionEnvironment()
        {
            var globalNamespace = new NamespaceSymbol("<global>", string.Empty);
            var testNamespace = globalNamespace.GetOrAddNamespace("Test");
            var vrcNamespace = globalNamespace.GetOrAddNamespace("VRC");
            var sdkBaseNamespace = vrcNamespace.GetOrAddNamespace("SDKBase");

            var ownerType = TypeSymbol.CreateNamed("Owner", "Test.Owner");
            var apiType = TypeSymbol.CreateNamed("Api", "Test.Api");
            var fooType = TypeSymbol.CreateNamed("Foo", "Test.Foo");
            var utilitiesType = TypeSymbol.CreateNamed(
                "Utilities",
                "VRC.SDKBase.Utilities");
            testNamespace.AddType(ownerType);
            testNamespace.AddType(apiType);
            testNamespace.AddType(fooType);
            sdkBaseNamespace.AddType(utilitiesType);

            apiType.AddMethod(new ExternMethodSymbol(
                "TryGet",
                apiType,
                Array.Empty<ParameterSymbol>(),
                TypeSymbol.Tuple(new[] { TypeSymbol.Bool, ownerType }),
                null,
                ProjectedTryGetSignature,
                isStatic: true,
                memberKind: ExternMemberKind.Method,
                abiParameters: new[]
                {
                    new ExternParameterSymbol(
                        "owner",
                        ownerType,
                        ExternParameterPassingMode.Out,
                        -1)
                },
                abiReturnType: TypeSymbol.Bool));

            apiType.AddMethod(new ExternMethodSymbol(
                "OutInt",
                apiType,
                Array.Empty<ParameterSymbol>(),
                TypeSymbol.I32,
                null,
                "TestApi.__OutInt__SystemInt32Ref__SystemVoid",
                isStatic: true,
                memberKind: ExternMemberKind.Method,
                abiParameters: new[]
                {
                    new ExternParameterSymbol(
                        "value",
                        TypeSymbol.I32,
                        ExternParameterPassingMode.Out,
                        -1)
                },
                abiReturnType: TypeSymbol.Unit));

            var mixedAbiParameters = new[]
            {
                new ExternParameterSymbol(
                    "value",
                    TypeSymbol.I32,
                    ExternParameterPassingMode.Ref,
                    0),
                new ExternParameterSymbol(
                    "owner",
                    ownerType,
                    ExternParameterPassingMode.Out,
                    -1),
                new ExternParameterSymbol(
                    "text",
                    TypeSymbol.String,
                    ExternParameterPassingMode.Out,
                    -1)
            };
            apiType.AddMethod(new ExternMethodSymbol(
                "Mixed",
                apiType,
                new[] { new ParameterSymbol("value", TypeSymbol.I32, 0) },
                TypeSymbol.Tuple(new[]
                {
                    TypeSymbol.I32,
                    TypeSymbol.I32,
                    ownerType,
                    TypeSymbol.String
                }),
                null,
                ProjectedMixedSignature,
                isStatic: true,
                memberKind: ExternMemberKind.Method,
                abiParameters: mixedAbiParameters,
                abiReturnType: TypeSymbol.I32));

            AddFakeConstructor(
                fooType,
                "TestFoo.__ctor__SystemInt32__TestFoo",
                new[] { new ParameterSymbol("value", TypeSymbol.I32, 0) },
                new[]
                {
                    new ExternParameterSymbol(
                        "value",
                        TypeSymbol.I32,
                        ExternParameterPassingMode.Normal,
                        0)
                },
                fooType);
            AddFakeConstructor(
                fooType,
                "TestFoo.__ctor__SystemInt32Ref__TestFoo",
                new[] { new ParameterSymbol("value", TypeSymbol.I32, 0) },
                new[]
                {
                    new ExternParameterSymbol(
                        "value",
                        TypeSymbol.I32,
                        ExternParameterPassingMode.Ref,
                        0)
                },
                TypeSymbol.Tuple(new[] { fooType, TypeSymbol.I32 }));
            AddFakeConstructor(
                fooType,
                "TestFoo.__ctor__SystemStringRef__TestFoo",
                Array.Empty<ParameterSymbol>(),
                new[]
                {
                    new ExternParameterSymbol(
                        "name",
                        TypeSymbol.String,
                        ExternParameterPassingMode.Out,
                        -1)
                },
                TypeSymbol.Tuple(new[] { fooType, TypeSymbol.String }));
            AddFakeConstructor(
                fooType,
                "TestFoo.__ctor__SystemInt32Ref_SystemStringRef_SystemSingleRef__TestFoo",
                new[]
                {
                    new ParameterSymbol("value", TypeSymbol.I32, 0),
                    new ParameterSymbol("weight", TypeSymbol.F32, 1)
                },
                new[]
                {
                    new ExternParameterSymbol(
                        "value",
                        TypeSymbol.I32,
                        ExternParameterPassingMode.Ref,
                        0),
                    new ExternParameterSymbol(
                        "name",
                        TypeSymbol.String,
                        ExternParameterPassingMode.Out,
                        -1),
                    new ExternParameterSymbol(
                        "weight",
                        TypeSymbol.F32,
                        ExternParameterPassingMode.Ref,
                        1)
                },
                TypeSymbol.Tuple(new[]
                {
                    fooType,
                    TypeSymbol.I32,
                    TypeSymbol.String,
                    TypeSymbol.F32
                }));
            AddFakeConstructor(
                fooType,
                ProjectedConstructorMaybeSignature,
                Array.Empty<ParameterSymbol>(),
                new[]
                {
                    new ExternParameterSymbol(
                        "owner",
                        ownerType,
                        ExternParameterPassingMode.Out,
                        -1)
                },
                TypeSymbol.Tuple(new[] { fooType, ownerType }));

            utilitiesType.AddMethod(new ExternMethodSymbol(
                "IsValid",
                utilitiesType,
                new[] { new ParameterSymbol("value", ownerType, 0) },
                TypeSymbol.Bool,
                null,
                ProjectedValiditySignature,
                isStatic: true));

            var typesByName = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
            {
                [ownerType.QualifiedName] = ownerType,
                [apiType.QualifiedName] = apiType,
                [fooType.QualifiedName] = fooType,
                [utilitiesType.QualifiedName] = utilitiesType
            };
            var catalog = new ExternCatalog(
                globalNamespace,
                new Dictionary<Type, TypeSymbol>
                {
                    [typeof(void)] = TypeSymbol.Unit,
                    [typeof(bool)] = TypeSymbol.Bool,
                    [typeof(int)] = TypeSymbol.I32,
                    [typeof(float)] = TypeSymbol.F32,
                    [typeof(string)] = TypeSymbol.String,
                    [typeof(ProjectionOwnerFixture)] = ownerType,
                    [typeof(ProjectionFooFixture)] = fooType
                },
                typesByName);
            return new SobakasuCompilationEnvironment(catalog);
        }

        private static void AddFakeConstructor(
            TypeSymbol containingType,
            string signature,
            IReadOnlyList<ParameterSymbol> parameters,
            IReadOnlyList<ExternParameterSymbol> abiParameters,
            TypeSymbol logicalReturnType)
        {
            containingType.AddMethod(new ExternMethodSymbol(
                "new",
                containingType,
                parameters,
                logicalReturnType,
                null,
                signature,
                isStatic: true,
                memberKind: ExternMemberKind.Constructor,
                abiParameters: abiParameters,
                abiReturnType: containingType));
        }

        private static (
            BoundProgram Program,
            IrProgram Ir,
            string Uasm) CompileWithEnvironment(
                string source,
                SobakasuCompilationEnvironment environment)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));

            var binder = new SobakasuBinder(environment);
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                Format(binder.Diagnostics.Diagnostics));

            var desugarer = new SobakasuDesugarer();
            var desugared = desugarer.Desugar(program);
            Assert.That(desugarer.Diagnostics.Diagnostics, Is.Empty,
                Format(desugarer.Diagnostics.Diagnostics));

            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(desugared);
            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));

            var optimized = new SobakasuOptimizer().Optimize(ir);
            var assembler = new SobakasuUasmAssembler();
            var uasm = assembler.Assemble(optimized);
            Assert.That(assembler.Diagnostics.Diagnostics, Is.Empty,
                Format(assembler.Diagnostics.Diagnostics));
            return (program, ir, uasm);
        }

        private static ExternMethodSymbol FindExternalMethod(
            BoundProgram program,
            string functionName)
        {
            return program.Functions.Single(function =>
                    function.FunctionSymbol.Name == functionName)
                .FunctionSymbol.ExternalBinding.ExternalMethod;
        }

        private static IrExternCallInstruction FindExternCall(
            IrProgram program,
            string signature)
        {
            foreach (var module in program.Modules)
            foreach (var block in module.Blocks)
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrExternCallInstruction call &&
                    call.ExternSignature == signature)
                {
                    return call;
                }
            }

            Assert.Fail($"Extern call '{signature}' was not lowered.");
            return null;
        }

        private static int CountExternCalls(IrProgram program, string signature)
        {
            var count = 0;
            foreach (var module in program.Modules)
            foreach (var block in module.Blocks)
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrExternCallInstruction call &&
                    call.ExternSignature == signature)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasCopyBeforeCall(
            IrProgram program,
            IrExternCallInstruction expectedCall,
            IrValue value)
        {
            foreach (var module in program.Modules)
            foreach (var block in module.Blocks)
            {
                for (var index = 0; index < block.Instructions.Count; index++)
                {
                    if (!ReferenceEquals(block.Instructions[index], expectedCall))
                        continue;

                    for (var copyIndex = 0; copyIndex < index; copyIndex++)
                    {
                        if (block.Instructions[copyIndex] is IrCopyInstruction copy &&
                            ReferenceEquals(copy.Target, value))
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }

            Assert.Fail("The expected extern call was not found in the IR.");
            return false;
        }

        private static SobakasuBinder Bind(
            string source,
            SobakasuCompilationEnvironment environment = null)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var binder = environment == null
                ? new SobakasuBinder()
                : new SobakasuBinder(environment);
            binder.BindProgram(syntax);
            return binder;
        }

        private static SobakasuCompilationEnvironment CreateAmbiguousExternEnvironment()
        {
            var globalNamespace = new NamespaceSymbol("<global>", string.Empty);
            var testNamespace = globalNamespace.GetOrAddNamespace("Test");
            var apiType = TypeSymbol.CreateNamed("Api", "Test.Api");
            var parameters = new[]
            {
                new ParameterSymbol("value", TypeSymbol.I32, 0)
            };
            apiType.AddMethod(new ExternMethodSymbol(
                "Call",
                apiType,
                parameters,
                TypeSymbol.Unit,
                typeof(SobakasuImplExternTests).GetMethod(
                    nameof(AmbiguousExternCandidateA),
                    BindingFlags.Static | BindingFlags.NonPublic),
                "TestApi.__CallA__SystemInt32__SystemVoid"));
            apiType.AddMethod(new ExternMethodSymbol(
                "Call",
                apiType,
                parameters,
                TypeSymbol.Unit,
                typeof(SobakasuImplExternTests).GetMethod(
                    nameof(AmbiguousExternCandidateB),
                    BindingFlags.Static | BindingFlags.NonPublic),
                "TestApi.__CallB__SystemInt32__SystemVoid"));
            testNamespace.AddType(apiType);

            var clrTypes = new Dictionary<Type, TypeSymbol>
            {
                [typeof(void)] = TypeSymbol.Unit,
                [typeof(int)] = TypeSymbol.I32
            };
            var typesByName = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
            {
                [TypeSymbol.Unit.RuntimeQualifiedName] = TypeSymbol.Unit,
                [TypeSymbol.I32.RuntimeQualifiedName] = TypeSymbol.I32,
                [apiType.QualifiedName] = apiType
            };
            var catalog = new ExternCatalog(
                globalNamespace,
                clrTypes,
                typesByName);
            return new SobakasuCompilationEnvironment(catalog);
        }

        private static void AmbiguousExternCandidateA(int value)
        {
        }

        private static void AmbiguousExternCandidateB(int value)
        {
        }

        private sealed class ProjectionOwnerFixture
        {
        }

        private sealed class ProjectionFooFixture
        {
        }

        private SobakasuProgramAsset CreateProgramAsset()
        {
            var folderGuid = AssetDatabase.CreateFolder(
                "Assets",
                $"SobakasuImplExternTests_{Guid.NewGuid():N}");
            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            RegisterForCleanup(folderPath);

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folderPath}/SobakasuProgramAsset.asset");
            var asset = ScriptableObject.CreateInstance<SobakasuProgramAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            RegisterForCleanup(assetPath);
            return AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
        }

        private void RegisterForCleanup(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
                _cleanupAssetPaths.Add(assetPath);
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

            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty,
                Format(lexer.Diagnostics.Diagnostics));
            return tokens;
        }

        private static bool ContainsCode(
            IReadOnlyList<Diagnostic> diagnostics,
            string code)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
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

        private static string Format(IReadOnlyList<Diagnostic> diagnostics)
        {
            var lines = new List<string>();
            foreach (var diagnostic in diagnostics)
                lines.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", lines);
        }
    }
}
