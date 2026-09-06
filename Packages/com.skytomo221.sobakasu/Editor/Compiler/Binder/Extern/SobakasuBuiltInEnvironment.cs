using System;
using System.Collections.Generic;
using System.Reflection;
using VRC.Udon.Editor;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal static class SobakasuBuiltInEnvironment
    {
        private static readonly Lazy<SobakasuCompilationEnvironment> DefaultEnvironment =
            new(CreateDefault);

        public static SobakasuCompilationEnvironment Default => DefaultEnvironment.Value;

        private static SobakasuCompilationEnvironment CreateDefault()
        {
            var catalog = new ReflectionExternCatalogBuilder(UdonExposedNodeCache.Default)
                .BuildDefaultCatalog();
            return new SobakasuCompilationEnvironment(catalog);
        }
    }
}
