using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class NetworkReceiveSymbol : Symbol
    {
        public override SymbolKind Kind => SymbolKind.NetworkReceive;
        public string ExportName { get; }
        public IReadOnlyList<ParameterSymbol> Parameters { get; }
        public IReadOnlyList<NetworkReceivePhysicalParameter> PhysicalParameters { get; }
        public TextSpan SourceSpan { get; }

        public NetworkReceiveSymbol(
            string name,
            string exportName,
            IReadOnlyList<ParameterSymbol> parameters,
            IReadOnlyList<NetworkReceivePhysicalParameter> physicalParameters,
            TextSpan sourceSpan)
            : base(name)
        {
            ExportName = exportName ?? throw new ArgumentNullException(nameof(exportName));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            PhysicalParameters = physicalParameters ??
                throw new ArgumentNullException(nameof(physicalParameters));
            SourceSpan = sourceSpan;
        }
    }
}
