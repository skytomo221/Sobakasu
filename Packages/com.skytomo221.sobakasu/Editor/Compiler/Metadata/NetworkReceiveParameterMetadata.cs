using System;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler
{
    public sealed class NetworkReceiveParameterMetadata
    {
        public string StorageName { get; }
        public TypeKind Type { get; }
        public string RuntimeTypeName { get; }

        public NetworkReceiveParameterMetadata(string storageName, TypeKind type, string runtimeTypeName)
        {
            StorageName = storageName ?? throw new ArgumentNullException(nameof(storageName));
            Type = type;
            RuntimeTypeName = runtimeTypeName;
        }
    }
}
