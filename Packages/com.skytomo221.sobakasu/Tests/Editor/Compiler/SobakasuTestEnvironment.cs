using System;
using System.IO;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class SobakasuTestEnvironment
    {
        private static readonly Lazy<SobakasuCompilationEnvironment> DefaultEnvironment =
            new(LoadDefault);

        public static SobakasuCompilationEnvironment Default => DefaultEnvironment.Value;

        public static string RepositoryRoot => GetRepositoryRoot();

        public static string StandardLibraryRoot => GetStandardLibraryRoot();

        public static SobakasuCompiler.CompileResult CompileToUasm(
            string sourceText,
            string standardLibraryRoot = null)
        {
            return SobakasuCompiler.CompileToUasm(
                sourceText,
                Default,
                standardLibraryRoot ?? GetStandardLibraryRoot());
        }

        private static SobakasuCompilationEnvironment LoadDefault()
        {
            var path = Path.Combine(
                GetRepositoryRoot(),
                "Packages",
                "com.skytomo221.sobakasu",
                "UdonApiCatalog~",
                "udon-api-catalog.json");
            return SobakasuCompilationEnvironment.FromUdonApiCatalogJson(
                File.ReadAllText(path));
        }

        private static string GetStandardLibraryRoot()
        {
            return Path.Combine(
                GetRepositoryRoot(),
                "Packages",
                "com.skytomo221.sobakasu",
                "StandardLibrary~");
        }

        private static string GetRepositoryRoot()
        {
            foreach (var start in new[]
                     {
                         AppContext.BaseDirectory,
                         Environment.CurrentDirectory
                     })
            {
                for (var directory = new DirectoryInfo(start);
                     directory != null;
                     directory = directory.Parent)
                {
                    if (File.Exists(Path.Combine(
                            directory.FullName,
                            "Packages",
                            "com.skytomo221.sobakasu",
                            "package.json")))
                    {
                        return directory.FullName;
                    }
                }
            }

            throw new DirectoryNotFoundException(
                "Could not locate the Sobakasu repository root.");
        }
    }
}
