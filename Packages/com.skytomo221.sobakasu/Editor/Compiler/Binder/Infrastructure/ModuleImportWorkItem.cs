using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ModuleImportWorkItem
    {
        public StandardLibraryModule Module { get; }
        public ResolvedUseDirective Import { get; }

        public ModuleImportWorkItem(StandardLibraryModule module, ResolvedUseDirective import)
        {
            Module = module;
            Import = import;
        }
    }
}
