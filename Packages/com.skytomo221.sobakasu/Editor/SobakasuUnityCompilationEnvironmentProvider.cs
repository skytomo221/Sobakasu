#if UNITY_EDITOR
using System;
using System.IO;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.UdonApiCatalog;

namespace Skytomo221.Sobakasu
{
    internal static class SobakasuUnityCompilationEnvironmentProvider
    {
        private static readonly object Gate = new();
        private static SobakasuCompilationEnvironment _defaultEnvironment;
        private static DateTime _catalogLastWriteTimeUtc;

        public static SobakasuCompilationEnvironment GetEnvironment()
        {
            var path = UdonApiCatalogGenerator.DefaultOutputPath;
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            lock (Gate)
            {
                if (_defaultEnvironment == null ||
                    _catalogLastWriteTimeUtc != lastWriteTimeUtc)
                {
                    _defaultEnvironment = Load(path);
                    _catalogLastWriteTimeUtc = lastWriteTimeUtc;
                }
                return _defaultEnvironment;
            }
        }

        private static SobakasuCompilationEnvironment Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The Udon API catalog has not been generated.", path);
            return new SobakasuCompilationEnvironment(UdonApiCatalogLoader.Load(File.ReadAllText(path)));
        }
    }
}
#endif
