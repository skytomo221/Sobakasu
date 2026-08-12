using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuImplExternTests
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
  pub static fn from_null -> Self { null }
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
                $"on Interact {{ {statement} }}"));
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
on Interact {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members[^1], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_RecoversAfterInvalidExternExpression()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"on Interact { extern ; }
on Update {}"));
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
on Interact {
  let receiver = 1;
  extern UnityEngine.Debug.Log(receiver.choose(2));
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Binder_ReportsAmbiguousMethodOverloadForNull()
        {
            var binder = Bind(
                @"pub impl GameObject = extern UnityEngine.GameObject {}
impl i32 {
  fn choose(value: GameObject) -> i32 { 1 }
  fn choose(value: string) -> i32 { 2 }
}
on Interact {
  let receiver = 1;
  receiver.choose(null);
}");

            Assert.That(ContainsCode(binder.Diagnostics.Diagnostics, "SBK2082"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsNoApplicableMethodOverload()
        {
            var binder = Bind(
                @"impl i32 {
  fn choose(value: bool) -> i32 { 1 }
}
on Interact {
  let receiver = 1;
  receiver.choose(2);
}");

            Assert.That(ContainsCode(binder.Diagnostics.Diagnostics, "SBK2081"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsUnsupportedAndUnknownExternExpressions()
        {
            var unsupported = Bind("on Interact { extern 1; }");
            Assert.That(ContainsCode(unsupported.Diagnostics.Diagnostics, "SBK2087"), Is.True,
                Format(unsupported.Diagnostics.Diagnostics));

            var unknown = Bind(
                "on Interact { extern UnityEngine.Debug.MemberThatDoesNotExist; }");
            Assert.That(ContainsCode(unknown.Diagnostics.Diagnostics, "SBK2083"), Is.True,
                Format(unknown.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsExternalExposureAndOverloadDiagnostics()
        {
            var notExposed = Bind(
                "on Interact { extern System.Console.WriteLine(1); }");
            Assert.That(ContainsCode(notExposed.Diagnostics.Diagnostics, "SBK2084"), Is.True,
                Format(notExposed.Diagnostics.Diagnostics));

            var notApplicable = Bind(
                "on Interact { extern UnityEngine.Mathf.Clamp(\"x\", 0, 1); }");
            Assert.That(ContainsCode(notApplicable.Diagnostics.Diagnostics, "SBK2085"), Is.True,
                Format(notApplicable.Diagnostics.Diagnostics));

            var ambiguous = Bind(
                "on Interact { extern Test.Api.Call(1); }",
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

state target: GameObject = null;

on Interact {
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

on Interact {
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

on Interact {
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

on Interact {
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

on Interact {
  let wrapped: GameObject = null;
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

on Interact {
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

state target: GameObject = null;

fn get_target -> GameObject {
  extern UnityEngine.Debug.Log(""receiver"");
  target
}

fn get_name -> string {
  extern UnityEngine.Debug.Log(""value"");
  ""Sobakasu""
}

on Interact {
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

state target: GameObject = null;

on Interact {
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
                TypeSymbol.U0,
                typeof(SobakasuImplExternTests).GetMethod(
                    nameof(AmbiguousExternCandidateA),
                    BindingFlags.Static | BindingFlags.NonPublic),
                "TestApi.__CallA__SystemInt32__SystemVoid"));
            apiType.AddMethod(new ExternMethodSymbol(
                "Call",
                apiType,
                parameters,
                TypeSymbol.U0,
                typeof(SobakasuImplExternTests).GetMethod(
                    nameof(AmbiguousExternCandidateB),
                    BindingFlags.Static | BindingFlags.NonPublic),
                "TestApi.__CallB__SystemInt32__SystemVoid"));
            testNamespace.AddType(apiType);

            var clrTypes = new Dictionary<Type, TypeSymbol>
            {
                [typeof(void)] = TypeSymbol.U0,
                [typeof(int)] = TypeSymbol.I32
            };
            var typesByName = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
            {
                [TypeSymbol.U0.RuntimeQualifiedName] = TypeSymbol.U0,
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
