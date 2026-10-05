using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class NetworkReceiveSymbol : Symbol, IDocumentableSymbol
    {
        public DocumentationComment Documentation { get; set; }
        public override SymbolKind Kind => SymbolKind.NetworkReceive;
        public string ExportName { get; }
        public bool IsPublic { get; }
        public bool RequiresStateCapability { get; }
        public IReadOnlyList<ParameterSymbol> Parameters { get; }
        public IReadOnlyList<NetworkReceivePhysicalParameter> PhysicalParameters { get; }
        public TextSpan SourceSpan { get; }

        public NetworkReceiveSymbol(
            string name,
            string exportName,
            IReadOnlyList<ParameterSymbol> parameters,
            IReadOnlyList<NetworkReceivePhysicalParameter> physicalParameters,
            TextSpan sourceSpan,
            bool isPublic,
            bool requiresStateCapability = false)
            : base(name)
        {
            ExportName = exportName ?? throw new ArgumentNullException(nameof(exportName));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            PhysicalParameters = physicalParameters ??
                throw new ArgumentNullException(nameof(physicalParameters));
            SourceSpan = sourceSpan;
            IsPublic = isPublic;
            RequiresStateCapability = requiresStateCapability;
        }
    }
}
