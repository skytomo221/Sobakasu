using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuArrayTests
    {
        private const string IntArrayConstructor =
            "SystemInt32Array.__ctor__SystemInt32__SystemInt32Array";
        private const string IntArrayGetter =
            "SystemInt32Array.__Get__SystemInt32__SystemInt32";
        private const string IntArraySetter =
            "SystemInt32Array.__Set__SystemInt32_SystemInt32__SystemVoid";
        private const string IntArrayLength =
            "SystemInt32Array.__get_Length__SystemInt32";
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
        public void Parser_ParsesArrayTypesLiteralsRepeatIndexingAndLength()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"fn inspect(values: [i32], matrix: [[i32]], index: i32) {
  let literal = [1, 2, 3];
  let empty: [i32] = [];
  let repeated = [1; 4];
  let defaults = [i32; index];
  values[index] = 2;
  values[index] += 1;
  matrix[index][0] = 3;
  values.length;
  values.length();
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));
            var function = syntax.Members[0] as FunctionDeclarationSyntax;
            Assert.That(function, Is.Not.Null);
            Assert.That(function.Parameters[0].Type.IsArray, Is.True);
            Assert.That(function.Parameters[0].Type.ElementType.GetText(), Is.EqualTo("i32"));
            Assert.That(function.Parameters[1].Type.IsArray, Is.True);
            Assert.That(function.Parameters[1].Type.ElementType.IsArray, Is.True);
        }

        [Test]
        public void Parser_RecoversAfterMalformedArrayBeforeFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"fn broken() { let values = [1, 2; ]; }
fn after() -> i32 { 42 }
on start {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members.Count, Is.EqualTo(3));
            Assert.That(syntax.Members[1], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members[2], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Binder_InternsArrayTypesAndResolvesInstalledUdonAbi()
        {
            var first = TypeSymbol.Array(TypeSymbol.I32);
            var second = TypeSymbol.Array(TypeSymbol.I32);

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.RuntimeQualifiedName, Is.EqualTo("System.Int32[]"));
            var catalog = SobakasuBuiltInEnvironment.Default.ExternCatalog;
            Assert.That(catalog.TryGetArrayIntrinsics(first, out var intrinsics, out var reason),
                Is.True, reason);
            Assert.That(intrinsics.ConstructorExternSignature, Is.EqualTo(IntArrayConstructor));
            Assert.That(intrinsics.GetterExternSignature, Is.EqualTo(IntArrayGetter));
            Assert.That(intrinsics.SetterExternSignature, Is.EqualTo(IntArraySetter));
            Assert.That(intrinsics.LengthExternSignature, Is.EqualTo(IntArrayLength));
            Assert.That(intrinsics.IndexType, Is.SameAs(TypeSymbol.I32));
        }

        [Test]
        public void Compiler_LowersLiteralDefaultRepeatIndexAssignmentAndLength()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn first(values: [i32]) -> i32 { values[0] }
fn create_values(length: i32) -> [i32] { [i32; length] }

on start {
  let mut values: [i32] = [1, 2, 3];
  values[0] += 10;
  values = create_values(4);
  let repeated = [values[0]; 2];
  extern UnityEngine.Debug.Log(first(repeated));
  extern UnityEngine.Debug.Log(values.length);
  extern UnityEngine.Debug.Log(values.length());
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(IntArrayConstructor));
            Assert.That(result.Uasm, Does.Contain(IntArrayGetter));
            Assert.That(result.Uasm, Does.Contain(IntArraySetter));
            Assert.That(CountOccurrences(result.Uasm, IntArrayLength), Is.EqualTo(2));
        }

        [Test]
        public void Documentation_ArraySampleCompiles()
        {
            var source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "docs",
                "samples",
                "arrays.sobakasu"));

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_ContextuallyTypesEmptyAndObjectArrayArguments()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn consume_ints(values: [i32]) {}
fn consume_objects(values: [object]) {}

on start {
  consume_ints([]);
  consume_objects([1, ""text"", true]);
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("%SystemObjectArray"));
            Assert.That(result.Uasm, Does.Contain("SystemObjectArray.__Set__SystemInt32_SystemObject__SystemVoid"));
        }

        [Test]
        public void Compiler_UsesStringAndExternalBindingArrayAbiTypes()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl GameObject = extern UnityEngine.GameObject {}
pub state targets: [GameObject] = [];

on start {
  let names: [string] = [""Sobakasu"", ""Fallback""];
  let local_targets: [GameObject] = [extern UnityEngine.GameObject.Find(""Sobakasu"")];
  local_targets[0] = targets[0];
  extern UnityEngine.Debug.Log(names.length);
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("%SystemStringArray"));
            Assert.That(result.Uasm, Does.Contain("%UnityEngineGameObjectArray"));
            Assert.That(result.Uasm, Does.Contain("UnityEngineGameObjectArray.__ctor__SystemInt32__UnityEngineGameObjectArray"));
        }

        [Test]
        public void Compiler_LowersRepeatAsDynamicLoopWithSingleLengthAndOperandSites()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn repeat_length() -> i32 {
  extern UnityEngine.Mathf.Abs(-3)
}

fn next_value() -> i32 {
  extern UnityEngine.Mathf.Clamp(1, 0, 2)
}

on start {
  let values = [next_value(); repeat_length()];
  let empty = [next_value(); 0];
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(
                result.Uasm,
                "UnityEngineMathf.__Abs__SystemInt32__SystemInt32"), Is.EqualTo(1));
            Assert.That(CountOccurrences(
                result.Uasm,
                "UnityEngineMathf.__Clamp__SystemInt32_SystemInt32_SystemInt32__SystemInt32"),
                Is.EqualTo(2));
            Assert.That(result.Uasm, Does.Contain("array_repeat_condition"));
            Assert.That(result.Uasm, Does.Contain("JUMP_IF_FALSE"));
        }

        [Test]
        public void Compiler_DefaultConstructionOmitsElementInitializationLoop()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "on start { let values = [i32; 4]; }");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, IntArrayConstructor), Is.EqualTo(1));
            Assert.That(result.Uasm, Does.Not.Contain(IntArraySetter));
            Assert.That(result.Uasm, Does.Not.Contain("array_repeat_condition"));
        }

        [Test]
        public void Compiler_CapturesCompoundIndexTargetAndRightHandSideOnce()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn get_array() -> [i32] { [1] }
fn next_index() -> i32 { extern UnityEngine.Mathf.Abs(0) }
fn value() -> i32 { extern UnityEngine.Mathf.Clamp(1, 0, 2) }

on start {
  get_array()[next_index()] += value();
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, IntArrayConstructor), Is.EqualTo(1));
            Assert.That(CountOccurrences(result.Uasm, IntArrayGetter), Is.EqualTo(1));
            Assert.That(CountOccurrences(result.Uasm, IntArraySetter), Is.EqualTo(2));
            Assert.That(CountOccurrences(
                result.Uasm,
                "UnityEngineMathf.__Abs__SystemInt32__SystemInt32"), Is.EqualTo(1));
            Assert.That(CountOccurrences(
                result.Uasm,
                "UnityEngineMathf.__Clamp__SystemInt32_SystemInt32_SystemInt32__SystemInt32"),
                Is.EqualTo(1));
        }

        [TestCase("on start { let values = []; }", "SBK2010")]
        [TestCase("on start { let values = [1, \"text\"]; }", "SBK2011")]
        [TestCase("on start { let values = [i32; -1]; }", "SBK2095")]
        [TestCase("on start { let values = [i32; true]; }", "SBK2094")]
        [TestCase("on start { let values = [missing; 1]; }", "SBK2092")]
        [TestCase("on start { let value = 1; let item = value[0]; }", "SBK2096")]
        [TestCase("on start { let values = [1]; let item = values[true]; }", "SBK2097")]
        [TestCase("on start { let values = [1]; values[0] = \"text\"; }", "SBK2098")]
        [TestCase("on start { let values = [true]; values[0] += true; }", "SBK2099")]
        public void Compiler_ReportsArrayDiagnostics(string source, string expectedCode)
        {
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, expectedCode), Is.True, result.ErrorText);
        }

        [Test]
        public void Binder_ReportsAmbiguousRepeatOperandWhenTypeAndValueCollide()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub impl GameObject = extern UnityEngine.GameObject {}
on start {
  let GameObject: GameObject = extern UnityEngine.GameObject.Find(""Sobakasu"");
  let values = [GameObject; 2];
}" );

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK2093"), Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_AllowsElementMutationButRequiresMutForReferenceReplacement()
        {
            var elementMutation = SobakasuCompiler.CompileToUasm(
                "on start { let values = [1]; values[0] = 2; }");
            var immutableReplacement = SobakasuCompiler.CompileToUasm(
                "on start { let values = [1]; values = [2]; }");
            var mutableReplacement = SobakasuCompiler.CompileToUasm(
                "on start { let mut values = [1]; values = [2]; }");

            Assert.That(elementMutation.Success, Is.True, elementMutation.ErrorText);
            Assert.That(immutableReplacement.Success, Is.False);
            Assert.That(ContainsCode(immutableReplacement.Diagnostics, "SBK2016"), Is.True,
                immutableReplacement.ErrorText);
            Assert.That(mutableReplacement.Success, Is.True, mutableReplacement.ErrorText);
        }

        [Test]
        public void Compiler_ArrayAssignmentCopiesTheReferenceWithoutCloning()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on start {
  let original = [1, 2, 3];
  let shared = original;
  shared[0] = 100;
  extern UnityEngine.Debug.Log(original[0]);
}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(CountOccurrences(result.Uasm, IntArrayConstructor), Is.EqualTo(1));
            Assert.That(result.Uasm, Does.Contain("COPY"));
        }

        [Test]
        public void Compiler_SeparatesPublicAndSynchronizationArrayChecks()
        {
            var supported = SobakasuCompiler.CompileToUasm(
                @"pub state values: [i32] = [];
sync state scores: [i32] = [];
on start {}" );
            var linear = SobakasuCompiler.CompileToUasm(
                "sync(linear) state values: [i32] = []; on start {}");
            var references = SobakasuCompiler.CompileToUasm(
                "sync state targets: [object] = []; on start {}");

            Assert.That(supported.Success, Is.True, supported.ErrorText);
            Assert.That(linear.Success, Is.False);
            Assert.That(ContainsCode(linear.Diagnostics, "SBK2061"), Is.True, linear.ErrorText);
            Assert.That(references.Success, Is.False);
            Assert.That(ContainsCode(references.Diagnostics, "SBK2061"), Is.True,
                references.ErrorText);
        }

        [Test]
        public void UasmAssembler_AcceptsPublicAndNoneSynchronizedArrayStates()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"pub state values: [i32] = [];
sync state scores: [i32] = [];
on start {}" );
            Assert.That(result.Success, Is.True, result.ErrorText);
            var asset = CreateProgramAsset();

            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
        }

        [Test]
        public void Compiler_AcceptsJaggedArraysOnlyWhenInstalledUdonAbiExposesThem()
        {
            const string source = @"on start {
  let matrix = [[i32; 2]; 3];
  matrix[1][0] = 42;
}";
            var jaggedType = TypeSymbol.Array(TypeSymbol.Array(TypeSymbol.I32));
            var isAvailable = SobakasuBuiltInEnvironment.Default.ExternCatalog
                .TryGetArrayIntrinsics(jaggedType, out _, out _);
            TestContext.Out.WriteLine($"Installed SDK exposes i32[][] ABI: {isAvailable}");
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.EqualTo(isAvailable), result.ErrorText);
            if (!isAvailable)
                Assert.That(ContainsCode(result.Diagnostics, "SBK2091"), Is.True, result.ErrorText);
        }

        [Test]
        public void Compiler_ProducesTypedArrayStateHeapPatches()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "state values: [i32] = [1, 2, 3]; on start {}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.HeapPatches.Count, Is.EqualTo(1));
            var patch = result.HeapPatches[0];
            Assert.That(patch.Kind, Is.EqualTo(HeapPatchKind.GlobalInitializer));
            Assert.That(patch.SymbolType, Is.EqualTo(TypeKind.Array));
            Assert.That(patch.RuntimeTypeName, Is.EqualTo("System.Int32[]"));
            Assert.That(patch.RuntimeValue, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Compiler_PreservesObjectArrayBoxingTypesInStatePatch()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "state values: [object] = [1, \"text\", true]; on start {}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            var patch = result.HeapPatches[0];
            Assert.That(patch.RuntimeTypeName, Is.EqualTo("System.Object[]"));
            var values = patch.RuntimeValue as object[];
            Assert.That(values, Is.Not.Null);
            Assert.That(values[0], Is.TypeOf<int>());
            Assert.That(values[1], Is.TypeOf<string>());
            Assert.That(values[2], Is.TypeOf<bool>());
        }

        [Test]
        public void Compiler_EvaluatesConstantDefaultAndRepeatArrayStateInitializers()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"state zeros: [i32] = [i32; 4];
state repeated: [i32] = [1 + 1; 3];
on start {}" );

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.HeapPatches.Count, Is.EqualTo(2));
            Assert.That(result.HeapPatches[0].RuntimeValue,
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(result.HeapPatches[1].RuntimeValue,
                Is.EqualTo(new[] { 2, 2, 2 }));
        }

        [Test]
        public void HeapPatchSerializer_RoundTripsNestedArraysNullsAndBoxingTypes()
        {
            object[] value = { 1, "text", true, null, new[] { 2, 3 } };
            var serialized = HeapPatchValueSerializer.SerializeRuntimeValue(
                value,
                TypeKind.Array,
                "System.Object[]");
            var restored = (object[])HeapPatchValueSerializer.DeserializeRuntimeValue(
                serialized,
                TypeKind.Array,
                "System.Object[]");

            Assert.That(restored[0], Is.TypeOf<int>());
            Assert.That(restored[0], Is.EqualTo(1));
            Assert.That(restored[1], Is.TypeOf<string>());
            Assert.That(restored[2], Is.TypeOf<bool>());
            Assert.That(restored[3], Is.Null);
            Assert.That(restored[4], Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        public void RefreshProgram_ReappliesArrayStateHeapPatchManifest()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "state values: [i32] = [1, 2, 3]; on start {}");
            Assert.That(result.Success, Is.True, result.ErrorText);
            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError),
                Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError),
                Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError),
                Is.True, commitError);
            RegisterForCleanup(AssetDatabase.GetAssetPath(asset.SerializedProgramAsset));

            asset.RefreshProgram();

            var patch = result.HeapPatches[0];
            var program = asset.GetRealProgram();
            var address = program.SymbolTable.GetAddressFromSymbol(patch.SymbolName);
            Assert.That(program.Heap.GetHeapVariable(address), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Binder_RejectsRuntimeArrayStateInitializersAsNonConstant()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"fn next_value() -> i32 { 1 }
state values = [next_value(); 4];
on start {}" );

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsCode(result.Diagnostics, "SBK2062"), Is.True, result.ErrorText);
        }

        private SobakasuProgramAsset CreateProgramAsset()
        {
            var folderGuid = AssetDatabase.CreateFolder(
                "Assets",
                $"SobakasuArrayTests_{Guid.NewGuid():N}");
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
    }
}
