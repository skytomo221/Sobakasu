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
    public class ImplExternAbiTests
    {

        private const string MaybeDefinition = @"
lang ""maybe""
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
        private SobakasuProgramAsset CreateProgramAsset()
        {
            return SobakasuTestAssetFactory.CreateImportedProgramAsset(
                "SobakasuImplExternTests",
                RegisterForCleanup);
        }
        private void RegisterForCleanup(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
                _cleanupAssetPaths.Add(assetPath);
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
            var (Program, Ir, Uasm) = CompileWithEnvironment(source, environment);

            var refOnly = FindExternalMethod(Program, "ref_only");
            Assert.That(refOnly.Parameters.Select(parameter => parameter.Type),
                Is.EqualTo(new[] { TypeSymbol.I32 }));
            Assert.That(refOnly.ReturnType, Is.SameAs(TypeSymbol.I32));
            Assert.That(refOnly.AbiReturnType, Is.SameAs(TypeSymbol.Unit));
            Assert.That(refOnly.AbiParameters.Select(parameter => parameter.PassingMode),
                Is.EqualTo(new[] { ExternParameterPassingMode.Ref }));

            var outOnly = FindExternalMethod(Program, "out_only");
            Assert.That(outOnly.Parameters, Is.Empty);
            Assert.That(outOnly.ReturnType, Is.SameAs(TypeSymbol.I32));
            Assert.That(outOnly.AbiParameters[0].LogicalInputOrdinal, Is.EqualTo(-1));
            Assert.That(outOnly.AbiParameters[0].PassingMode,
                Is.EqualTo(ExternParameterPassingMode.Out));

            var returnAndOut = FindExternalMethod(
                Program,
                "return_and_out");
            Assert.That(returnAndOut.ReturnType.TupleElementTypes,
                Is.EqualTo(new[] { TypeSymbol.Bool, TypeSymbol.I32 }));

            var mixed = FindExternalMethod(Program, "mixed");
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

            var mixedCall = FindExternCall(Ir, mixed.ExternSignature);
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
                Ir,
                mixedCall,
                mixedCall.Arguments[1]), Is.True,
                "ref input must be copied into its physical ABI slot");
            Assert.That(HasCopyBeforeCall(
                Ir,
                mixedCall,
                mixedCall.Arguments[2]), Is.False,
                "out output must not be initialized before the extern call");
            Assert.That(HasCopyBeforeCall(
                Ir,
                mixedCall,
                mixedCall.Arguments[3]), Is.True,
                "ref input must be copied into its physical ABI slot");

            Assert.That(Uasm, Does.Contain(mixed.ExternSignature));
            Assert.That(Uasm, Does.Not.Contain("SystemValueTuple"));
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
            var (Program, Ir, Uasm) = CompileWithEnvironment(
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

            var raw = FindExternalMethod(Program, "raw");
            var projected = FindExternalMethod(Program, "projected");
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
                    Ir,
                    ProjectedTryGetSignature),
                Is.EqualTo(2),
                "Each of the two wrapper invocations must call the same physical overload once.");
            Assert.That(CountExternCalls(
                    Ir,
                    ProjectedValiditySignature),
                Is.EqualTo(1));
            Assert.That(Uasm, Does.Not.Contain("SystemValueTuple"));
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

        [Test]
        public void ConstructorBindings_UseSelfThenRefOutProjectionOrder()
        {
            var environment = CreateProjectionEnvironment();
            var (Program, Ir, Uasm) = CompileWithEnvironment(
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

            var normal = FindExternalMethod(Program, "normal");
            var byRef = FindExternalMethod(Program, "by_ref");
            var byOut = FindExternalMethod(Program, "by_out");
            var mixed = FindExternalMethod(Program, "mixed");
            var optional = FindExternalMethod(Program, "optional_owner");

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
                    Ir,
                    ProjectedConstructorMaybeSignature),
                Is.EqualTo(1));
            Assert.That(CountExternCalls(
                    Ir,
                    ProjectedValiditySignature),
                Is.EqualTo(1));
            Assert.That(Uasm, Does.Not.Contain("SystemValueTuple"));
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
                Does.Contain("maybe.Maybe<unity.game_object.GameObject>"));
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
        public void StandardLibrary_UsesDeclarativeStaticInstanceAndMaybeBindings()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use system.math;
use unity.GameObject;

on interact {
  extern UnityEngine.Debug.Log(math.sqrt(9.0f64));
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
                binding.DeclaringModule == "system.math" &&
                binding.SobakasuName == "sqrt"), Is.True);
            Assert.That(result.ExternalBindings.Any(binding =>
                binding.DeclaringModule == "unity.game_object" &&
                binding.SobakasuName == "GameObject.find"), Is.True);
            Assert.That(result.ExternalBindings.Any(binding =>
                binding.DeclaringModule == "unity.game_object" &&
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
                binding.DeclaringModule == "unity.vector3_binding" &&
                binding.SobakasuName == "Vector3.smooth_damp" &&
                binding.SobakasuParameterTypes.Count == 6);
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
    }
}
