using System;
using System.IO;
using Skytomo221.Sobakasu.Compiler;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class SobakasuTestCompiler
    {
        public static SobakasuCompiler.CompileResult CompileWithoutStandardLibrary(
            string sourceText,
            string sourcePath = "<entry>")
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-empty-standard-library-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                return SobakasuTestEnvironment.CompileToUasm(
                    sourceText,
                    root,
                    sourcePath);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
