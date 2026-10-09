using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public sealed class HeapPatchMetadataTests
    {
        [Test]
        public void CompileToUasm_ReturnsHeapPatchMetadataForSupportedLiterals()
        {
            const string source = @"behavior {
  on interact {
    extern UnityEngine.Debug.Log(""hello"");
    extern UnityEngine.Debug.Log(true);
    extern UnityEngine.Debug.Log(1i64);
    extern UnityEngine.Debug.Log(1u64);
    extern UnityEngine.Debug.Log(1f64);
    extern UnityEngine.Debug.Log('A');
  }
}";

            var result = SobakasuTestEnvironment.CompileToUasm(source);

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
                    continue;

                Assert.That(patch.SymbolName, Does.StartWith("__const_"));
                Assert.That(patch.SymbolType, Is.EqualTo(expectedType));
                Assert.That(patch.Kind, Is.EqualTo(HeapPatchKind.Constant));
                Assert.That(patch.SourceSpan.HasValue, Is.True);
                Assert.That(sourceText.ToString(patch.SourceSpan.Value), Is.EqualTo(expectedSpanText));
                return;
            }

            Assert.Fail($"Could not find heap patch '{expectedValue}' with type '{expectedType}'.");
        }
    }
}
