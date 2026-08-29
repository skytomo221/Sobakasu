#if UNITY_EDITOR
using System;
using Skytomo221.Sobakasu.Compiler;

namespace Skytomo221.Sobakasu
{
    internal static class SobakasuProgramCompiler
    {
        public static void CompileAndStore(
            SobakasuProgramAsset programAsset,
            string sourceText,
            string sourcePath = "")
        {
            if (programAsset == null)
                throw new ArgumentNullException(nameof(programAsset));

            var result = SobakasuCompiler.CompileToUasm(sourceText ?? string.Empty);
            SobakasuUnityDiagnosticReporter.Report(
                programAsset,
                sourcePath,
                sourceText,
                result.Diagnostics);

            if (!result.Success)
            {
                programAsset.SetCompilationFailure(result.ErrorText);
                return;
            }

            if (!programAsset.SetUasmAndAssemble(
                    result.Uasm,
                    result.NetworkReceivers,
                    out var assemblyError))
            {
                programAsset.SetCompilationFailure(
                    "Udon Assembly error:\n" + assemblyError);
                return;
            }

            if (!programAsset.ApplyHeapPatches(result.HeapPatches, out _))
                return;

            programAsset.CommitProgram(result.HeapPatches, out _);
        }
    }
}
#endif
