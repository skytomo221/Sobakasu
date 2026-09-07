using System;
using Skytomo221.Sobakasu.Compiler.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
    internal sealed class StandardLibraryResolution
    {
        public StandardLibraryModuleGraph Graph { get; }
        public DiagnosticBag Diagnostics { get; }

        public StandardLibraryResolution(StandardLibraryModuleGraph graph, DiagnosticBag diagnostics)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }
    }
}
