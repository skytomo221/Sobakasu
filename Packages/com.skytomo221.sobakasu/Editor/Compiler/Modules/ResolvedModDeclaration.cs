using System;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
    internal sealed class ResolvedModDeclaration
    {
        public ModDeclarationSyntax Syntax { get; }
        public StandardLibraryModule ChildModule { get; }
        public bool IsPublic => Syntax.IsPublic;

        public ResolvedModDeclaration(ModDeclarationSyntax syntax, StandardLibraryModule childModule)
        {
            Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
            ChildModule = childModule ?? throw new ArgumentNullException(nameof(childModule));
        }
    }
}
