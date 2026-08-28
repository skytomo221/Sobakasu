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
        private const string DebugLogExternSignature =
            "UnityEngineDebug.__Log__SystemObject__SystemVoid";
        private const string MathfSqrtExternSignature =
            "UnityEngineMathf.__Sqrt__SystemSingle__SystemSingle";
        private const string MathfClampExternSignature =
            "UnityEngineMathf.__Clamp__SystemInt32_SystemInt32_SystemInt32__SystemInt32";

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
            const string source = @"on interact() {
  let mut x = 1;
  x = 2;
  extern UnityEngine.Debug.Log(x);
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
            const string source = @"on interact() {
  let mut x = 1;
  x = 2;
  extern UnityEngine.Debug.Log(x);
}";

            var result = SobakasuCompiler.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError), Is.True, assemblyError);
        }

        [Test]
        public void CompileToUasm_LowersExternCallInitializerIntoLocal()
        {
            const string source = @"
on interact() {
  let x = extern UnityEngine.Mathf.Sqrt(2.0f32);
  extern UnityEngine.Debug.Log(x);
}";

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(MathfSqrtExternSignature));
            Assert.That(result.Uasm, Does.Contain("PUSH, __temp_0"));
            Assert.That(result.Uasm, Does.Contain("COPY"));
        }

        [Test]
        public void CompileToUasm_LowersExternCallAssignmentRightHandSide()
        {
            const string source = @"
on interact() {
  let mut x = 0.0f32;
  x = extern UnityEngine.Mathf.Sqrt(2.0f32);
  extern UnityEngine.Debug.Log(x);
}";

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(MathfSqrtExternSignature));
            Assert.That(result.Uasm, Does.Contain("PUSH, __temp_0"));
            Assert.That(result.Uasm, Does.Contain("COPY"));
        }

        [Test]
        public void CompileToUasm_LowersNestedExternCallArgument()
        {
            const string source = @"
on interact() {
  extern UnityEngine.Debug.Log(extern UnityEngine.Mathf.Sqrt(2.0f32));
}";

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(MathfSqrtExternSignature));
            Assert.That(result.Uasm, Does.Contain(DebugLogExternSignature));
            Assert.That(result.Uasm, Does.Contain("PUSH, __temp_0"));
        }

        [Test]
        public void CompileToUasm_LowersMultiArgumentExternValueCall()
        {
            const string source = @"
on interact() {
  extern UnityEngine.Debug.Log(extern UnityEngine.Mathf.Clamp(2, 0, 10));
}";

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(MathfClampExternSignature));
            Assert.That(result.Uasm, Does.Contain("PUSH, __temp_0"));
        }

        [Test]
        public void CompileToUasm_EmitsResolvedOperatorExternsAndShortCircuitBranches()
        {
            const string source = @"
on interact() {
  let mut x = 1;
  x += 2 * 3;
  let a = false;
  let b = a && ((extern UnityEngine.Mathf.Sqrt(1.0f32)) > 0.0f32);
  extern UnityEngine.Mathf.Clamp(x, 0, 10);
}";

            var result = SobakasuCompiler.CompileToUasm(source);

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("op_Multiplication"));
            Assert.That(result.Uasm, Does.Contain("op_Addition"));
            Assert.That(result.Uasm, Does.Contain("op_GreaterThan"));
            Assert.That(result.Uasm, Does.Contain(MathfSqrtExternSignature));
            Assert.That(result.Uasm, Does.Contain("JUMP_IF_FALSE"));
            Assert.That(result.Uasm, Does.Not.Contain("op_LogicalAnd"));
        }

        [Test]
        public void SetUasmAndAssemble_SucceedsForExternCallInitializerAndRead()
        {
            const string source = @"
on interact() {
  let x = extern UnityEngine.Mathf.Sqrt(2.0f32);
  extern UnityEngine.Debug.Log(x);
}";

            var result = SobakasuCompiler.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError), Is.True, assemblyError);
        }

        [Test]
        public void SetUasmAndAssemble_SucceedsForCompoundAssignmentAndShortCircuitOperators()
        {
            const string source = @"
on interact() {
  let mut x = 1;
  x += 1;
  x <<= 1;
  let a = false;
  let b = a || ((extern UnityEngine.Mathf.Sqrt(1.0f32)) > 0.0f32);
}";

            var result = SobakasuCompiler.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);

            var asset = CreateProgramAsset();
            Assert.That(asset.SetUasmAndAssemble(result.Uasm, out var assemblyError), Is.True, assemblyError);
        }

        private static IEnumerable<TestCaseData> SuccessfulCompilationSources()
        {
            yield return new TestCaseData(@"on interact() {
  let x = 1;
}");

            yield return new TestCaseData(@"on interact() {
  let x: i32 = 1;
}");

            yield return new TestCaseData(@"on interact() {
  let mut x = 1;
  x = 2;
}");

            yield return new TestCaseData(@"on interact() {
  let x = 1;
  let x = 2;
}");

            yield return new TestCaseData(@"on interact() {
  let x = 1;
  {
    let x = 2;
  }
}");

            yield return new TestCaseData(@"on interact() {
  let x = 1;
  extern UnityEngine.Debug.Log(x);
}");

            yield return new TestCaseData(@"on interact() {
  let mut x = 1;
  x = 2;
  extern UnityEngine.Debug.Log(x);
}");

            yield return new TestCaseData(@"
on interact() {
  let x = extern UnityEngine.Mathf.Sqrt(2.0f32);
  extern UnityEngine.Debug.Log(x);
}");

            yield return new TestCaseData(@"on interact() {
  1 + 1;
}");

            yield return new TestCaseData(@"on interact() {
  +1;
}");

            yield return new TestCaseData(@"on interact() {
  -1;
}");

            yield return new TestCaseData(@"on interact() {
  1 + 2 * 3;
}");

            yield return new TestCaseData(@"on interact() {
  (1 + 2) * 3;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 6;
  let b = 2;
  a / b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 7;
  let b = 3;
  a % b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 1;
  let b = 2;
  let c = 3;
  a + b + c;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 2;
  let b = 3;
  let c = 4;
  a * b + c;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 1;
  let b = 2;
  let c = 1;
  a + b << c;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 1;
  let b = 1;
  a == b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 1;
  let b = 2;
  a != b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 1;
  let b = 2;
  a < b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 1;
  let b = 2;
  a <= b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 2;
  let b = 1;
  a > b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 2;
  let b = 1;
  a >= b;
}");

            yield return new TestCaseData(@"on interact() {
  let flag = true;
  !flag;
}");

            yield return new TestCaseData(@"on interact() {
  let a = true;
  let b = false;
  a && b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = true;
  let b = false;
  a || b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = true;
  let b = false;
  let c = true;
  a && b || c;
}");

            yield return new TestCaseData(@"on interact() {
  let mask = 1;
  ~mask;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 3;
  let b = 1;
  a & b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 3;
  let b = 1;
  a | b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 3;
  let b = 1;
  a ^ b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 4;
  a << 1;
}");

            yield return new TestCaseData(@"on interact() {
  let a = 4;
  a >> 1;
}");

            yield return new TestCaseData(@"on interact() {
  let a = true;
  let b = false;
  a & b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = true;
  let b = false;
  a | b;
}");

            yield return new TestCaseData(@"on interact() {
  let a = true;
  let b = false;
  a ^ b;
}");

            yield return new TestCaseData(@"on interact() {
  let mut x = 0;
  x = 1;
}");

            yield return new TestCaseData(@"on interact() {
  let mut x = 1;
  x += 1;
}");

            yield return new TestCaseData(@"on interact() {
  let mut x = 1;
  x *= 2 + 3;
}");

            yield return new TestCaseData(@"on interact() {
  let mut x = 1;
  x <<= 1;
}");

            yield return new TestCaseData(@"
on interact() {
  extern UnityEngine.Mathf.Clamp(1 + 2 * 3, 0, 10);
}");
        }

        private static IEnumerable<TestCaseData> FailedCompilationSources()
        {
            yield return new TestCaseData(
                @"on interact() {
  let x = 1;
  x = 2;
}",
                "SBK2016");

            yield return new TestCaseData(
                @"on interact() {
  let x: i32 = ""a"";
}",
                "SBK2005");

            yield return new TestCaseData(
                @"on interact() {
  y = 1;
}",
                "SBK2002");

            yield return new TestCaseData(
                @"on interact() {
  let x: i32;
}",
                "SBK2014");

            yield return new TestCaseData(
                @"on interact() {
  let x: Unknown = 1;
}",
                "SBK2015");

            yield return new TestCaseData(
                @"on interact() {
  let x: i64 = 1;
}",
                "SBK2005");

            yield return new TestCaseData(
                @"on interact() {
  let a = ""a"";
  let b = ""b"";
  a + b;
}",
                "SBK2027");

            yield return new TestCaseData(
                @"on interact() {
  let a = 1;
  let b = 1u32;
  a + b;
}",
                "SBK2027");

            yield return new TestCaseData(
                @"on interact() {
  1 + 1.0f32;
}",
                "SBK2027");

            yield return new TestCaseData(
                @"on interact() {
  !1;
}",
                "SBK2026");

            yield return new TestCaseData(
                @"on interact() {
  ~true;
}",
                "SBK2026");

            yield return new TestCaseData(
                @"on interact() {
  1 && true;
}",
                "SBK2030");

            yield return new TestCaseData(
                @"on interact() {
  let mut x = 1;
  (x + 1) = 2;
}",
                "SBK2017");

            yield return new TestCaseData(
                @"on interact() {
  let mut x = 1;
  (x + 1) += 2;
}",
                "SBK2029");
        }

        private SobakasuProgramAsset CreateProgramAsset()
        {
            return SobakasuTestAssetFactory.CreateImportedProgramAsset(
                "SobakasuLocalVariableTests",
                RegisterForCleanup);
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
