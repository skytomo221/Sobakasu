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
    public class ImplExternOperatorBehaviorTests : ImplExternTestFixture
    {
        public void Binder_ReportsImplAndOperatorDiagnostics(
            string source,
            string expectedCode)
        {
            var binder = Bind(source);

            Assert.That(ContainsCode(binder.Diagnostics.Diagnostics, expectedCode), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_AllowsCanonicalPrimitiveExternalBinding()
        {
            var binder = Bind("pub impl i32 = extern System.Int32 {}");

            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Compiler_ResolvesPrimitiveDeclarativeOperatorsFromImpl()
        {
            var signatures = new[]
            {
                "SystemInt32.__op_Addition__SystemInt32_SystemInt32__SystemInt32",
                "SystemInt32.__op_UnaryNegation__SystemInt32__SystemInt32",
                "SystemInt32.__op_OnesComplement__SystemInt32__SystemInt32"
            };
            var catalog = new ReflectionExternCatalogBuilder(new UdonExposedNodeCache(signatures))
                .BuildCatalog(new[] { "System" });
            var (_, _, Uasm) = CompileWithEnvironment(@"
pub impl i32 = extern System.Int32 {
  pub fn +(rhs: Self) -> Self = extern self + rhs
  pub fn @- -> Self = extern -self
  pub fn @~ -> Self = extern ~self
}
on interact {
  let sum = 1 + 2;
  let negative = -sum;
  let complement = ~negative;
  complement;
}", new SobakasuCompilationEnvironment(catalog));

            Assert.That(Uasm, Does.Contain("SystemInt32.__op_Addition"));
            Assert.That(Uasm,
                Does.Contain("SystemInt32.__op_UnaryNegation")
                    .Or.Contain("SystemInt32.__op_UnaryMinus"));
            Assert.That(Uasm,
                Does.Contain("SystemInt32.__op_OnesComplement")
                    .Or.Contain("SystemInt32.__op_BitwiseNot"));
        }

        [TestCase("1 + 2", "SBK2027")]
        [TestCase("+1", "SBK2026")]
        [TestCase("~1", "SBK2026")]
        public void Compiler_RequiresImplDeclarationForPrimitiveSourceOperator(string expression, string expectedCode)
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                $"on interact {{ let value = {expression}; }}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, expectedCode), Is.True,
                result.ErrorText);
        }

        [TestCase("let mut value = 1; value += 2;", "SBK2005")]
        [TestCase("let values = [1]; values[0] += 2;", "SBK2098")]
        [TestCase("let mut holder = Holder { value: 1 }; holder.value += 2;", "SBK2005")]
        public void Binder_ReportsIncompatibleCompoundOperatorResult(string statement, string expectedCode)
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary($@"
impl i32 {{ pub fn +(rhs: Self) -> bool {{ true }} }}
struct Holder {{ value: i32, }}
on start {{ {statement} }}");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, expectedCode), Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_UsesCompoundOperatorParameterTypeForArrayLiteralOperand()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(@"
impl i32 { pub fn +(rhs: [i32]) -> Self { rhs[0] } }
on start { let values = [1]; values[0] += [2]; }");

            Assert.That(result.Success, Is.True, result.ErrorText);
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
        public void Compiler_UsesImplOperatorForEveryCompoundAssignmentTarget()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(@"
pub impl i32 = extern System.Int32 {
  pub fn +(rhs: Self) -> Self = extern self + rhs
}
struct Holder { value: i32, }
state state_value = 1;
on interact {
  let mut local = 1;
  let mut values = [1];
  let mut holder = Holder { value: 1 };
  local += 1;
  state_value += 1;
  values[0] += 1;
  holder.value += 1;
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(
                result.Uasm,
                "SystemInt32.__op_Addition"), Is.EqualTo(4));
        }

        [Test]
        public void Compiler_ResolvesPrimitiveExternalInstanceAndStaticMethods()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(@"
pub impl i32 = extern System.Int32 {
  pub fn compare_to(value: i32) -> i32
    = extern self.CompareTo(value)
  pub static fn parse(value: string) -> i32
    = extern System.Int32.Parse(value)
}
on interact {
  let comparison = 1.compare_to(2);
  let parsed = i32.parse(""42"");
  extern UnityEngine.Debug.Log(comparison);
  extern UnityEngine.Debug.Log(parsed);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("SystemInt32.__CompareTo"));
            Assert.That(result.Uasm, Does.Contain("SystemInt32.__Parse"));
        }

        [Test]
        public void Compiler_CompilesVector3ConstructorMethodsAndOperators()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
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

impl f32 {
  pub fn *(rhs: Vector3) -> Vector3 {
    extern self * rhs
  }
}

on interact {
  let mut value = Vector3.new(1.0f32, 2.0f32, 3.0f32);
  value.set_x(4.0f32);
  let sum = value + Vector3.zero;
  let inverse = -sum;
  let scaled = 2.0f32 * inverse;
  extern UnityEngine.Debug.Log(inverse.magnitude);
  extern UnityEngine.Debug.Log(scaled.magnitude);
  extern UnityEngine.Debug.Log(value.x);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__ctor"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__op_Addition"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__op_UnaryNegation"));
            Assert.That(result.Uasm, Does.Contain(
                "UnityEngineVector3.__op_Multiply__SystemSingle_UnityEngineVector3"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__get_magnitude"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__get_x"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineVector3.__set_x"));
        }

        [Test]
        public void Compiler_CompilesPrimitiveImplAndRuntimeTypeMapping()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(
                @"impl i32 {
  pub fn %(rhs: Self) -> Self {
    extern self % rhs
  }

  pub fn ==(rhs: Self) -> bool {
    extern self == rhs
  }

  pub fn @- -> Self {
    extern -self
  }

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


    }
}
