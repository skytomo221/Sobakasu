using System;

namespace Skytomo221.Sobakasu.Compiler
{
    public enum SobakasuSourceKind
    {
        Script,
        Library,
        Editor,
    }

    public static class SobakasuSourceKinds
    {
        public const string ScriptSuffix = ".sobakasu";
        public const string LibrarySuffix = ".library.sobakasu";
        public const string EditorSuffix = ".editor.sobakasu";

        public static SobakasuSourceKind FromPath(string sourcePath)
        {
            if (!string.IsNullOrEmpty(sourcePath))
            {
                if (sourcePath.EndsWith(LibrarySuffix, StringComparison.OrdinalIgnoreCase))
                    return SobakasuSourceKind.Library;
                if (sourcePath.EndsWith(EditorSuffix, StringComparison.OrdinalIgnoreCase))
                    return SobakasuSourceKind.Editor;
            }

            return SobakasuSourceKind.Script;
        }

        public static string GetSuffix(SobakasuSourceKind kind)
        {
            return kind switch
            {
                SobakasuSourceKind.Library => LibrarySuffix,
                SobakasuSourceKind.Editor => EditorSuffix,
                _ => ScriptSuffix,
            };
        }
    }
}
