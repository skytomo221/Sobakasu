using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class SobakasuCompilationEnvironment
    {
        public NamespaceSymbol GlobalNamespace { get; }
        public ExternCatalog ExternCatalog { get; }

        public SobakasuCompilationEnvironment(ExternCatalog externCatalog)
        {
            ExternCatalog = externCatalog ?? throw new ArgumentNullException(nameof(externCatalog));
            GlobalNamespace = externCatalog.GlobalNamespace;
        }
    }
}
