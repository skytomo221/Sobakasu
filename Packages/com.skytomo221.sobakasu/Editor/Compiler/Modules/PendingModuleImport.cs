using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
    internal class PendingModuleImport
    {
        public UseDirectiveSyntax Syntax { get; }
        public UseTreeSyntax Tree { get; }
        public string TargetModuleName { get; }
        public IReadOnlyList<string> DeclarationPath { get; }
        public string Path { get; }
        public string IntroducedName { get; }
        public bool IsGlob { get; }
        public bool HasAlias => Tree.Alias != null;
        public bool IsMaterialized { get; private set; }
        public StandardLibraryModule ResolvedModule { get; private set; }

        public PendingModuleImport(UseDirectiveSyntax syntax, UseTreeSyntax tree,
            string targetModuleName, IReadOnlyList<string> declarationPath, string path,
            string introducedName, bool isGlob)
        {
            Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            TargetModuleName = targetModuleName ?? string.Empty;
            DeclarationPath = declarationPath ?? Array.Empty<string>();
            Path = path ?? string.Empty;
            IntroducedName = introducedName ?? string.Empty;
            IsGlob = isGlob;
        }

        public ResolvedUseDirective Materialize(StandardLibraryModule targetModule,
            StandardLibraryModule resolvedModule = null)
        {
            IsMaterialized = true;
            ResolvedModule = resolvedModule;
            return new ResolvedUseDirective(Syntax, Tree, targetModule, DeclarationPath, Path,
                IntroducedName, IsGlob);
        }
    }
}
