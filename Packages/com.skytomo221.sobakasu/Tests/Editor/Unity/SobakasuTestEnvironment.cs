using System.IO;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class SobakasuTestEnvironment
    {
        private static readonly string StandardLibraryRoot = Path.GetFullPath(Path.Combine(
            "Packages",
            "com.skytomo221.sobakasu",
            "StandardLibrary~"));

        public static SobakasuCompilationEnvironment Default =>
            SobakasuUnityCompilationEnvironmentProvider.GetEnvironment();

        public static SobakasuCompiler.CompileResult CompileToUasm(
            string sourceText,
            string standardLibraryRoot = null)
        {
            return SobakasuCompiler.CompileToUasm(
                sourceText,
                Default,
                standardLibraryRoot ?? StandardLibraryRoot);
        }
    }
}
