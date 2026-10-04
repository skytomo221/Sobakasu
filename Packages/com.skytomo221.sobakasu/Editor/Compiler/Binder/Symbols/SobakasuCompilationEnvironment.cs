using System;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public sealed class SobakasuCompilationEnvironment
    {
        internal NamespaceSymbol GlobalNamespace { get; }
        internal ExternCatalog ExternCatalog { get; }

        internal SobakasuCompilationEnvironment(ExternCatalog externCatalog)
        {
            ExternCatalog = externCatalog ?? throw new ArgumentNullException(nameof(externCatalog));
            GlobalNamespace = externCatalog.GlobalNamespace;
        }

        public static SobakasuCompilationEnvironment FromUdonApiCatalogJson(
            string json)
        {
            return new SobakasuCompilationEnvironment(UdonApiCatalogLoader.Load(json));
        }

        public bool IsExternTypeAvailable(string runtimeTypeName)
        {
            return ExternCatalog.TryGetTypeSymbol(runtimeTypeName, out _);
        }
    }
}
