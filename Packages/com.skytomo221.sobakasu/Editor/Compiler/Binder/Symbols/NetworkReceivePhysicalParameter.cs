using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class NetworkReceivePhysicalParameter
    {
        public ParameterSymbol LogicalParameter { get; }
        public ParameterSymbol PhysicalParameter { get; }
        public IReadOnlyList<string> Path { get; }

        public NetworkReceivePhysicalParameter(
            ParameterSymbol logicalParameter,
            ParameterSymbol physicalParameter,
            IReadOnlyList<string> path)
        {
            LogicalParameter = logicalParameter ??
                throw new ArgumentNullException(nameof(logicalParameter));
            PhysicalParameter = physicalParameter ??
                throw new ArgumentNullException(nameof(physicalParameter));
            Path = path ?? Array.Empty<string>();
        }
    }
}
