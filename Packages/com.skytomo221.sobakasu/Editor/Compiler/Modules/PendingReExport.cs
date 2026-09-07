using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
    internal sealed class PendingReExport : PendingModuleImport
    {
        public string ExportedName { get; }

        public PendingReExport(UseDirectiveSyntax syntax, UseTreeSyntax tree,
            string targetModuleName, IReadOnlyList<string> declarationPath, string path,
            string exportedName, bool isGlob)
            : base(syntax, tree, targetModuleName, declarationPath, path, exportedName, isGlob)
        {
            ExportedName = exportedName ?? string.Empty;
        }
    }
}
