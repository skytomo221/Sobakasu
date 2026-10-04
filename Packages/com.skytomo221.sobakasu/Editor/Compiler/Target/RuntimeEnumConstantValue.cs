using System;

namespace Skytomo221.Sobakasu.Compiler.Target
{
    public sealed class RuntimeEnumConstantValue
    {
        public RuntimeTypeIdentity Type { get; }
        public string Name { get; }
        public string NumericValue { get; }

        public RuntimeEnumConstantValue(RuntimeTypeIdentity type, string name, string numericValue)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            NumericValue = numericValue ?? throw new ArgumentNullException(nameof(numericValue));
        }
    }
}
