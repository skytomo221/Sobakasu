using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using UnityEditor;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuLocalVariableTests
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

        [TestCaseSource(nameof(SuccessfulCompilationSources))]
        public void CompileToUasm_SucceedsForSupportedLocalVariableScenarios(string source)
        {
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [TestCaseSource(nameof(FailedCompilationSources))]
        public void CompileToUasm_ReportsExpectedDiagnosticsForInvalidLocalVariableScenarios(
            string source,
            string expectedDiagnosticCode)
        {
            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics, Is.Not.Empty);
            Assert.That(ContainsDiagnosticCode(result.Diagnostics, expectedDiagnosticCode), Is.True, result.ErrorText);
        }

        [Test]
        public void CompileToUasm_LowersMutableLocalDeclarationAssignmentAndRead()
        {
            const string source = @"on Interact() {
  let mut x = 1;
  x = 2;
  Debug.Log(x);
}";

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("__local_0"));
            Assert.That(result.Uasm, Does.Contain("COPY"));
            Assert.That(result.Uasm, Does.Contain("PUSH, __local_0"));
        }

        [Test]
        public void SetUasmAndAssemble_SucceedsForLocalDeclarationAssignmentAndRead()
        {
            const string source = @"on Interact() {
  let mut x = 1;
  x = 2;
  Debug.Log(x);
}";

            var result = SobakasuCompiler.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError), Is.True, assemblyError);
        }

        private static IEnumerable<TestCaseData> SuccessfulCompilationSources()
        {
            yield return new TestCaseData(@"on Interact() {
  let x = 1;
}");

            yield return new TestCaseData(@"on Interact() {
  let x: i32 = 1;
}");

            yield return new TestCaseData(@"on Interact() {
  let mut x = 1;
  x = 2;
}");

            yield return new TestCaseData(@"on Interact() {
  let x = 1;
  let x = 2;
}");

            yield return new TestCaseData(@"on Interact() {
  let x = 1;
  {
    let x = 2;
  }
}");

            yield return new TestCaseData(@"on Interact() {
  let x = 1;
  Debug.Log(x);
}");

            yield return new TestCaseData(@"on Interact() {
  let mut x = 1;
  x = 2;
  Debug.Log(x);
}");
        }

        private static IEnumerable<TestCaseData> FailedCompilationSources()
        {
            yield return new TestCaseData(
                @"on Interact() {
  let x = 1;
  x = 2;
}",
                "SBK2016");

            yield return new TestCaseData(
                @"on Interact() {
  let x: i32 = ""a"";
}",
                "SBK2005");

            yield return new TestCaseData(
                @"on Interact() {
  y = 1;
}",
                "SBK2002");

            yield return new TestCaseData(
                @"on Interact() {
  let x: i32;
}",
                "SBK2014");

            yield return new TestCaseData(
                @"on Interact() {
  let x: Unknown = 1;
}",
                "SBK2015");

            yield return new TestCaseData(
                @"on Interact() {
  let x: i64 = 1;
}",
                "SBK2005");
        }

        private SobakasuProgramAsset CreateProgramAsset()
        {
            var folderGuid = AssetDatabase.CreateFolder("Assets", $"SobakasuLocalVariableTests_{Guid.NewGuid():N}");
            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            RegisterForCleanup(folderPath);

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/SobakasuProgramAsset.asset");
            var asset = ScriptableObject.CreateInstance<SobakasuProgramAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            RegisterForCleanup(assetPath);

            return AssetDatabase.LoadAssetAtPath<SobakasuProgramAsset>(assetPath);
        }

        private void RegisterForCleanup(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                _cleanupAssetPaths.Add(assetPath);
            }
        }

        private static bool ContainsDiagnosticCode(
            IReadOnlyList<Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic> diagnostics,
            string expectedCode)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == expectedCode)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
