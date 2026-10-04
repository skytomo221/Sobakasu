using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Target
{
    public sealed class RuntimeArrayConstantValue
    {
        public RuntimeTypeIdentity Type { get; }
        public IReadOnlyList<object> Elements { get; }

        public RuntimeArrayConstantValue(RuntimeTypeIdentity type, IReadOnlyList<object> elements)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Elements = elements ?? throw new ArgumentNullException(nameof(elements));
        }
    }
}
