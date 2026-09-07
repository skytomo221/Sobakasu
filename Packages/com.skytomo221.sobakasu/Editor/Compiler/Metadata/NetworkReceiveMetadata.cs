using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{
    public sealed class NetworkReceiveMetadata
    {
        public string Name { get; }
        public IReadOnlyList<NetworkReceiveParameterMetadata> Parameters { get; }

        public NetworkReceiveMetadata(string name, IReadOnlyList<NetworkReceiveParameterMetadata> parameters)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }
    }
}
