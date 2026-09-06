using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundEventDeclaration : BoundNode
    {
        public BoundEventSymbol EventSymbol { get; }
        public string Name => EventSymbol.SourceName;
        public string ExportName => EventSymbol.UdonName;
        public BoundBlockStatement Body { get; }

        public BoundEventDeclaration(
            BoundEventSymbol eventSymbol,
            BoundBlockStatement body)
        {
            EventSymbol = eventSymbol ?? throw new ArgumentNullException(nameof(eventSymbol));
            Body = body;
        }
    }
}
