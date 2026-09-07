using System;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
    internal sealed class PendingChildModule
    {
        public string Name { get; }
        public string LogicalName { get; }
        public ModDeclarationSyntax Syntax { get; }
        public bool IsPublic => Syntax.IsPublic;

        public PendingChildModule(string name, string logicalName, ModDeclarationSyntax syntax)
        {
            Name = name ?? string.Empty;
            LogicalName = logicalName ?? string.Empty;
            Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
        }
    }
}
