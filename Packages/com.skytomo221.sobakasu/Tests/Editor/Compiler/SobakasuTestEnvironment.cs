using System;
using System.IO;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class SobakasuTestEnvironment
    {
        private static readonly Lazy<SobakasuCompilationEnvironment> DefaultEnvironment =
            new(LoadDefault);

        public static SobakasuCompilationEnvironment Default => DefaultEnvironment.Value;

        public static SobakasuCompiler.CompileResult CompileToUasm(
            string sourceText,
            string standardLibraryRoot = null)
        {
            return SobakasuCompiler.CompileToUasm(
                sourceText,
                Default,
                standardLibraryRoot);
        }

        private static SobakasuCompilationEnvironment LoadDefault()
        {
            var path = Path.Combine(
                "Packages",
                "com.skytomo221.sobakasu",
                "UdonApiCatalog~",
                "udon-api-catalog.json");
            return new SobakasuCompilationEnvironment(
                UdonApiCatalogLoader.Load(File.ReadAllText(path)));
        }
    }
}
