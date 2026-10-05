using System;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
    internal sealed class ResolvedModuleDeclaration
    {
        public ModuleDeclarationSyntax Syntax { get; }
        public StandardLibraryModule ChildModule { get; }
        public bool IsPublic => Syntax.IsPublic;

        public ResolvedModuleDeclaration(ModuleDeclarationSyntax syntax, StandardLibraryModule childModule)
        {
            Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
            ChildModule = childModule ?? throw new ArgumentNullException(nameof(childModule));
        }
    }
}
