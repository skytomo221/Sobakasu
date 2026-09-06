using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class ModuleTestSupport
    {
        internal static void WriteHierarchy(string root, bool includePrelude)
        {
            if (includePrelude)
                WriteModule(root, "prelude", "pub use api;");
            WriteModule(root, "api", @"mod private_child;
pub mod public_child;
pub use private_child.twice;
pub use private_child.GameObject;");
            WriteModule(root, "api.private_child", @"impl i32 { pub fn *(rhs: Self) -> Self = extern self * rhs }
pub fn twice(value: i32) -> i32 { value * 2 }
pub impl GameObject = extern UnityEngine.GameObject {}");
            WriteModule(root, "api.public_child",
                "pub fn identity(value: i32) -> i32 { value }");
        }
        internal static string GetModulePath(string root, string logicalName)
        {
            return Path.Combine(
                root,
                logicalName.Replace('.', Path.DirectorySeparatorChar) + ".sobakasu");
        }
        internal static void WriteModule(string root, string logicalName, string source)
        {
            var path = GetModulePath(root, logicalName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, source);
        }
        internal static void WithTemporaryLibrary(Action<string> action)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "sobakasu-standard-library-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                action(root);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }
        internal static bool ContainsCode(
            Skytomo221.Sobakasu.Compiler.Diagnostic.DiagnosticBag diagnostics,
            string code)
        {
            foreach (var diagnostic in diagnostics.Diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }
        internal static bool ContainsCode(
            SobakasuCompiler.CompileResult result,
            string code)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }
    }
}
