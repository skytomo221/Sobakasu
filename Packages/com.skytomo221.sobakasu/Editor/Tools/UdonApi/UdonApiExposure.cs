using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Tools.UdonApi
{
    internal interface IUdonApiExposure
    {
        IReadOnlyCollection<string> ExposedSignatures { get; }
        bool IsTypeExposed(Type type);
        bool IsMemberExposed(string externSignature);
    }

    internal sealed class InstalledUdonApiExposure : IUdonApiExposure
    {
        private readonly UdonExposedNodeCache _cache;

        public InstalledUdonApiExposure(UdonExposedNodeCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public IReadOnlyCollection<string> ExposedSignatures => _cache.ExposedSignatures;

        public bool IsTypeExposed(Type type) => _cache.IsTypeExposed(type);

        public bool IsMemberExposed(string externSignature) => _cache.IsExposed(externSignature);
    }
}
