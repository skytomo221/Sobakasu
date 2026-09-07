using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
    internal sealed class ResolvedUseDirective
    {
        public UseDirectiveSyntax Syntax { get; }
        public UseTreeSyntax Tree { get; }
        public StandardLibraryModule TargetModule { get; }
        public IReadOnlyList<string> DeclarationPath { get; }
        public string DeclarationName => DeclarationPath.Count == 0 ? string.Empty : DeclarationPath[^1];
        public string Path { get; }
        public string IntroducedName { get; }
        public bool ImportsModule => !IsGlob && DeclarationPath.Count == 0;
        public bool IsGlob { get; }
        public bool HasAlias => Tree.Alias != null;
        public bool IsReExport => Syntax.IsReExport;

        public ResolvedUseDirective(UseDirectiveSyntax syntax, UseTreeSyntax tree,
            StandardLibraryModule targetModule, IReadOnlyList<string> declarationPath,
            string path, string introducedName, bool isGlob)
        {
            Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            TargetModule = targetModule ?? throw new ArgumentNullException(nameof(targetModule));
            DeclarationPath = declarationPath ?? Array.Empty<string>();
            Path = path ?? string.Empty;
            IntroducedName = introducedName ?? string.Empty;
            IsGlob = isGlob;
        }
    }
}
