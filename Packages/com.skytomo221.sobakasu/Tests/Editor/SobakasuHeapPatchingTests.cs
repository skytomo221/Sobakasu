using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuHeapPatchingTests
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
        public void CompileToUasm_ReturnsHeapPatchMetadataForSupportedLiterals()
        {
            const string source = @"on Interact() {
    Debug.Log(""hello"");
    Debug.Log(true);
    Debug.Log(1i64);
    Debug.Log(1u64);
    Debug.Log(1f64);
    Debug.Log('A');
}";

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Diagnostics, Is.Not.Null);
            Assert.That(result.HeapPatches.Count, Is.EqualTo(6));

            var sourceText = SourceText.From(source);

            AssertPatch(result.HeapPatches, "hello", TypeKind.String, "\"hello\"", sourceText);
            AssertPatch(result.HeapPatches, true, TypeKind.Bool, "true", sourceText);
            AssertPatch(result.HeapPatches, 1L, TypeKind.I64, "1i64", sourceText);
            AssertPatch(result.HeapPatches, 1UL, TypeKind.U64, "1u64", sourceText);
            AssertPatch(result.HeapPatches, 1d, TypeKind.F64, "1f64", sourceText);
            AssertPatch(result.HeapPatches, 'A', TypeKind.Char, "'A'", sourceText);
        }

        [Test]
        public void ApplyHeapPatches_PatchesBooleanLiteral()
        {
            AssertPatchedLiteralValue("true", true);
        }

        [Test]
        public void ApplyHeapPatches_PatchesInt64Literal()
        {
            AssertPatchedLiteralValue("9223372036854775807i64", long.MaxValue);
        }

        [Test]
        public void ApplyHeapPatches_PatchesUInt64Literal()
        {
            AssertPatchedLiteralValue("18446744073709551615u64", ulong.MaxValue);
        }

        [Test]
        public void ApplyHeapPatches_PatchesFloat64Literal()
        {
            AssertPatchedLiteralValue("3.141592653589793f64", 3.141592653589793d);
        }

        [Test]
        public void ApplyHeapPatches_PatchesStringLiteral()
        {
            AssertPatchedLiteralValue(@"""heap patch""", "heap patch");
        }

        [Test]
        public void ApplyHeapPatches_PatchesCharLiteral()
        {
            AssertPatchedLiteralValue("'Z'", 'Z');
        }

        [Test]
        public void ApplyHeapPatches_ReturnsExplicitErrorForUnsupportedType()
        {
            var asset = CreateProgramAsset();
            var result = CompileLiteral("true");

            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError), Is.True, assemblyError);

            var unsupportedPatch = new HeapPatchEntry(
                result.HeapPatches[0].SymbolName,
                TypeKind.Array,
                Array.Empty<int>(),
                HeapPatchKind.ArrayInitializer,
                result.HeapPatches[0].SourceSpan);

            Assert.That(asset.ApplyHeapPatches(new[] { unsupportedPatch }, out var patchError), Is.False);
            Assert.That(patchError, Does.Contain(result.HeapPatches[0].SymbolName));
            Assert.That(patchError, Does.Contain(TypeKind.Array.ToString()));
        }

        [Test]
        public void ApplyHeapPatches_ReturnsExplicitErrorForMissingSymbol()
        {
            var asset = CreateProgramAsset();
            var result = CompileLiteral("true");

            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError), Is.True, assemblyError);

            var missingSymbolPatch = new HeapPatchEntry(
                "__missing_symbol",
                TypeKind.Bool,
                true,
                HeapPatchKind.Constant,
                result.HeapPatches[0].SourceSpan);

            Assert.That(asset.ApplyHeapPatches(new[] { missingSymbolPatch }, out var patchError), Is.False);
            Assert.That(patchError, Does.Contain("__missing_symbol"));
            Assert.That(patchError, Does.Contain(TypeKind.Bool.ToString()));
        }

        [Test]
        public void ApplyHeapPatches_InvalidatesPersistedProgramOnFailure()
        {
            var asset = CreateProgramAsset();
            var result = CompileLiteral("true");

            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError), Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError), Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError), Is.True, commitError);

            var serializedProgramAsset = asset.SerializedProgramAsset;
            RegisterForCleanup(AssetDatabase.GetAssetPath(serializedProgramAsset));
            Assert.That(serializedProgramAsset.RetrieveProgram(), Is.Not.Null);

            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out assemblyError), Is.True, assemblyError);

            var missingSymbolPatch = new HeapPatchEntry(
                "__missing_symbol",
                TypeKind.Bool,
                true,
                HeapPatchKind.Constant,
                result.HeapPatches[0].SourceSpan);

            Assert.That(asset.ApplyHeapPatches(new[] { missingSymbolPatch }, out patchError), Is.False);
            Assert.That(asset.GetRealProgram(), Is.Null);
            Assert.That(serializedProgramAsset.RetrieveProgram(), Is.Null);
        }

        [Test]
        public void RefreshProgram_ReappliesStoredHeapPatchManifest()
        {
            var asset = CreateProgramAsset();
            var result = CompileLiteral(@"""refresh me""");

            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError), Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError), Is.True, patchError);
            Assert.That(asset.CommitProgram(result.HeapPatches, out var commitError), Is.True, commitError);

            var patch = result.HeapPatches[0];
            RegisterForCleanup(AssetDatabase.GetAssetPath(asset.SerializedProgramAsset));
            var initialProgram = asset.GetRealProgram();
            var initialAddress = initialProgram.SymbolTable.GetAddressFromSymbol(patch.SymbolName);
            Assert.That(initialProgram.Heap.GetHeapVariable(initialAddress), Is.EqualTo("refresh me"));

            asset.RefreshProgram();

            var refreshedProgram = asset.GetRealProgram();
            Assert.That(refreshedProgram, Is.Not.Null);

            var refreshedAddress = refreshedProgram.SymbolTable.GetAddressFromSymbol(patch.SymbolName);
            Assert.That(refreshedProgram.Heap.GetHeapVariable(refreshedAddress), Is.EqualTo("refresh me"));
        }

        private void AssertPatchedLiteralValue(string literal, object expectedValue)
        {
            var asset = CreateProgramAsset();
            var result = CompileLiteral(literal);

            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError), Is.True, assemblyError);
            Assert.That(asset.ApplyHeapPatches(result.HeapPatches, out var patchError), Is.True, patchError);

            var patch = result.HeapPatches[0];
            var program = asset.GetRealProgram();
            var address = program.SymbolTable.GetAddressFromSymbol(patch.SymbolName);
            var actualValue = program.Heap.GetHeapVariable(address);

            Assert.That(actualValue, Is.EqualTo(expectedValue));
            Assert.That(actualValue.GetType(), Is.EqualTo(expectedValue.GetType()));
        }

        private SobakasuProgramAsset CreateProgramAsset()
        {
            var folderGuid = AssetDatabase.CreateFolder("Assets", $"SobakasuHeapPatchTests_{Guid.NewGuid():N}");
            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            RegisterForCleanup(folderPath);

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/SobakasuProgramAsset.asset");
            var asset = ScriptableObject.CreateInstance<SobakasuProgramAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            RegisterForCleanup(assetPath);

            return AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
        }

        private SobakasuCompiler.CompileResult CompileLiteral(string literal)
        {
            var source = $"on Interact() {{ Debug.Log({literal}); }}";
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.HeapPatches.Count, Is.EqualTo(1));
            return result;
        }

        private void RegisterForCleanup(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                _cleanupAssetPaths.Add(assetPath);
            }
        }

        private static void AssertPatch(
            IReadOnlyList<HeapPatchEntry> patches,
            object expectedValue,
            TypeKind expectedType,
            string expectedSpanText,
            SourceText sourceText)
        {
            foreach (var patch in patches)
            {
                if (!Equals(patch.RuntimeValue, expectedValue) || patch.SymbolType != expectedType)
                {
                    continue;
                }

                Assert.That(patch.SymbolName, Does.StartWith("__const_"));
                Assert.That(patch.Kind, Is.EqualTo(HeapPatchKind.Constant));
                Assert.That(patch.SourceSpan.HasValue, Is.True);
                Assert.That(sourceText.ToString(patch.SourceSpan.Value), Is.EqualTo(expectedSpanText));
                return;
            }

            Assert.Fail($"Could not find heap patch '{expectedValue}' with type '{expectedType}'.");
        }
    }
}
