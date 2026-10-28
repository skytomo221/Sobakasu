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

using static Skytomo221.Sobakasu.Tests.Editor.ImplExternTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class ImplExternPipelineBehaviorTests : ImplExternTestFixture
    {
        [Test]
        public void Compiler_GeneratedValueReceiversLowerMaybeArrayResults()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(@"
use system::DateTime;
use system::Guid;
behavior { on interact {
  let mutable date = DateTime::new(2026, 10, 27);
  let formats = date.get_date_time_formats();
  let mutable id = Guid::new(""00000000-0000-0000-0000-000000000000"");
  let bytes = id.to_byte_array();
} }");
            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("SystemDateTime.__GetDateTimeFormats"));
            Assert.That(result.Uasm, Does.Contain("SystemGuid.__ToByteArray"));
            Assert.That(result.Uasm, Does.Contain("VRCSDKBaseUtilities.__IsValid__SystemObject__SystemBoolean"));
        }

        [TestCase("struct", "UnityEngine.ParticleSystem+Burst")]
        [TestCase("type", "TMPro.TMP_Dropdown+OptionData")]
        [TestCase("enum", "VRC.SDK3.Persistence.PlayerData+State")]
        public void Compiler_NestedRuntimeIdentityBindsForClassesStructsAndEnums(string kind, string runtime)
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                "public " + kind + " Nested = extern " + runtime + (kind == "type" ? ";\n" : " {}\n") +
                "function identity(value: Nested) -> Nested { value }\nbehavior { on interact {} }");
            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [TestCase("UnityEngine::UI::Dropdown::DropdownEvent")]
        [TestCase("VRC::Dynamics::ContactReceiverProxy")]
        public void Compiler_ResolvesObjectApisThroughConstructedGenericBase(string type)
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(MaybeDefinition +
                "function type_of(value: " + type + ") -> Maybe<System::Type>\n" +
                "  = maybe extern value.GetType()\nbehavior { on interact {} }");
            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_ParsesAndBindsEntireGeneratedStandardLibrary()
        {
            var root = SobakasuTestEnvironment.StandardLibraryRoot;
            var imports = new List<string>();
            foreach (var path in System.IO.Directory.GetFiles(root, "*.library.sobakasu", System.IO.SearchOption.AllDirectories))
            {
                var source = System.IO.File.ReadAllText(path);
                var parser = new SobakasuParser(SourceText.From(source));
                parser.ParseCompilationUnit();
                Assert.That(parser.Diagnostics.HasErrors, Is.False,
                    path + "\n" + string.Join("\n", parser.Diagnostics.Diagnostics.Select(diagnostic =>
                        diagnostic.Code + ": " + diagnostic.Message + " at " + diagnostic.Span.Start)));
                if ((source.Contains("implementation ") || source.Contains(" = extern ")) &&
                    !source.Replace("use maybe::Maybe;", "").Contains("use "))
                {
                    var diagnostics = SobakasuCompiler.ValidateDeclarations(
                        source.Replace("use maybe::Maybe;", MaybeDefinition), SobakasuTestEnvironment.Default);
                    Assert.That(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                        Is.Empty, path + "\n" + string.Join("\n", diagnostics.Select(diagnostic =>
                            diagnostic.Code + ": " + diagnostic.Message)));
                }
                var relative = System.IO.Path.GetRelativePath(root, path);
                var module = relative.Substring(0, relative.Length - ".library.sobakasu".Length)
                    .Replace("/", "::").Replace("\\", "::");
                if (module != "prelude" && !module.Contains("::")) imports.Add("use " + module + ";");
            }
            var result = SobakasuTestEnvironment.CompileToUasm(
                string.Join("\n", imports) + "\nbehavior { on interact {} }");
            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_MaybeArrayResultUsesObjectValidityConversion()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(MaybeDefinition + @"
function bytes(value: i32) -> Maybe<[u8]>
  = maybe extern System.BitConverter.GetBytes(value)
behavior { on interact { let result = bytes(42); } }");
            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("SystemBitConverter.__GetBytes__SystemInt32__SystemByteArray"));
            Assert.That(result.Uasm, Does.Contain("VRCSDKBaseUtilities.__IsValid__SystemObject__SystemBoolean"));
        }

        [Test]
        public void Compiler_MaybeArrayOutUsesObjectValidityConversion()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(MaybeDefinition + @"
function bytes(player: VRC::SDKBase::VRCPlayerApi, key: string) -> (bool, Maybe<[u8]>)
  = extern VRC.SDK3.Persistence.PlayerData.TryGetBytes(
      VRC::SDKBase::VRCPlayerApi player, string key, maybe out [u8] value)
behavior { on interact {} }");
            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_ConstructedGenericReferenceCanBeMaybePayload()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(MaybeDefinition + @"
function enumerate(list: VRC::Dynamics::VRCConstraintSourceKeyableList)
    -> Maybe<System::Collections::Generic::IEnumerator<VRC::Dynamics::VRCConstraintSource>>
  = maybe extern list.GetEnumerator()
behavior { on interact {
  let list = extern new VRC.Dynamics.VRCConstraintSourceKeyableList(0);
  let iterator = enumerate(list);
} }");
            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("VRCConstraintSourceKeyableList.__GetEnumerator"));
        }

        [Test]
        public void Compiler_ObjectConversionRejectsFlattenedArray()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(@"
struct Pair { x: i32, y: i32 }
behavior { on interact {
  let values = [Pair { x: 1, y: 2 }];
  extern UnityEngine.Debug.Log(values);
} }");
            Assert.That(result.Success, Is.False, "SoA arrays must not be passed as a single CLR object.");
            Assert.That(result.ErrorText, Does.Contain("SBK2119"));
        }

        [Test]
        public void Compiler_ObjectConversionAcceptsRuntimeArray()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(@"
function log(values: [i32]) { extern UnityEngine.Debug.Log(values); }
behavior { on interact { log([1, 2]); } }");
            Assert.That(result.Success, Is.True, result.ErrorText);
        }


        [Test]
        public void Compiler_ResolvesAssociatedFunctionOverloads()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"public implementation GameObject = extern UnityEngine.GameObject {
  function create(value: i32) -> i32 { 10 }
  function create(value: string) -> i32 { 20 }
}
behavior { on interact(state) {
  extern UnityEngine.Debug.Log(GameObject::create(1));
  extern UnityEngine.Debug.Log(GameObject::create(""value""));
} }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("UnityEngineDebug.__Log"));
        }

        [Test]
        public void Compiler_CompilesExternalGameObjectBindingAndPropertyAccess()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"public implementation GameObject = extern UnityEngine.GameObject {
  public function set_active(self, active: bool) {
    extern self.SetActive(active);
  }

  public function active?(self) -> bool {
    extern self.activeSelf
  }

  public function set_name(self, value: string) {
    extern self.name = value;
  }
}

behavior { on interact(state) {
  let target = extern UnityEngine.GameObject.Find(""Sobakasu"");
  target.set_active(true);
  target.set_name(""Sobakasu"");
  extern UnityEngine.Debug.Log(target.active?);
} }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("UnityEngineGameObject.__SetActive"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineGameObject.__get_activeSelf"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineGameObject.__set_name"));
        }

        [Test]
        public void Compiler_LowersMaybeExternOnceThroughExistingValidityPolicy()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"use unity::GameObject;

public function find_one(name: string) -> Maybe<GameObject>
  = maybe extern UnityEngine.GameObject.Find(name)

behavior { on interact(state) {
  let found = find_one(""Sobakasu"");
} }");

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
                Does.Contain("maybe.Maybe<unity.game_object.GameObject>"));
            Assert.That(metadata.ReturnMode,
                Is.EqualTo(ExternalBindingReturnMode.Maybe));

        }

        [Test]
        public void StandardLibrary_UsesDeclarativeStaticInstanceAndMaybeBindings()
        {
            var result = SobakasuTestEnvironment.CompileToUasm(
                @"use system::math;
use unity::GameObject;

behavior { on interact(state) {
  extern UnityEngine.Debug.Log(math::sqrt(9.0f64));
  let optional = GameObject::find(""Sobakasu"");
  let target = extern UnityEngine.GameObject.Find(""Sobakasu"");
  target.set_active(true);
} }");

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
                binding.DeclaringModule == "system.math" &&
                binding.SobakasuName == "sqrt"), Is.True);
            Assert.That(result.ExternalBindings.Any(binding =>
                binding.DeclaringModule == "unity.game_object" &&
                binding.SobakasuName == "GameObject::find"), Is.True);
            Assert.That(result.ExternalBindings.Any(binding =>
                binding.DeclaringModule == "unity.game_object" &&
                binding.SobakasuName == "GameObject::set_active"), Is.True);
        }

        [Test]
        public void UdonAssembler_AcceptsResolvedImplAndExternProgram()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"public implementation GameObject = extern UnityEngine.GameObject {
  public function set_name(self, value: string) { extern self.name = value; }
}

public implementation Vector3 = extern UnityEngine.Vector3 {
  public function new(x: f32, y: f32, z: f32) -> Self {
    extern new Self(x, y, z)
  }

  public function +(self, rhs: Self) -> Self { extern self + rhs }
  public function x(self) -> f32 { extern self.x }
  public function set_x(self, value: f32) { extern self.x = value; }
}

behavior { on interact(state) {
  let target = extern UnityEngine.GameObject.Find(""Sobakasu"");
  target.set_name(""Sobakasu"");
  let mutable value = Vector3::new(1.0f32, 2.0f32, 3.0f32);
  value.set_x(4.0f32);
  let sum = value + value;
  extern UnityEngine.Debug.Log(sum.x);
} }");

            Assert.That(result.Success, Is.True, result.ErrorText);
        }
    }
}
