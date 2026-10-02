namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal static class SobakasuBuiltInEnvironment
    {
        [System.Obsolete("Pass a catalog-backed compilation environment explicitly.")]
        public static SobakasuCompilationEnvironment Default =>
            global::Skytomo221.Sobakasu.SobakasuUnityCompilationEnvironmentProvider.GetEnvironment();
    }
}
