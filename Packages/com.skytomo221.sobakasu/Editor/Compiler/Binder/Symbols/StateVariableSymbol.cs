using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal enum StateSynchronizationMode
    {
        None,
        Linear,
        Smooth
    }

    internal static class StateSynchronizationModeFacts
    {
        public static string GetSourceName(StateSynchronizationMode mode)
        {
            return mode switch
            {
                StateSynchronizationMode.None => "none",
                StateSynchronizationMode.Linear => "linear",
                StateSynchronizationMode.Smooth => "smooth",
                _ => "unknown"
            };
        }
    }

    internal sealed class StateVariableSymbol : VariableSymbol, IDocumentableSymbol
    {
        public DocumentationComment Documentation { get; set; }
        public override SymbolKind Kind => SymbolKind.State;
        public bool IsPublic { get; }
        public StateSynchronizationMode? SynchronizationMode { get; }
        public bool IsSynchronized => SynchronizationMode.HasValue;
        public object InitialValue { get; }
        public TextSpan InitializerSpan { get; }
        public int Ordinal { get; }

        public StateVariableSymbol(
            string name,
            TypeSymbol type,
            bool isPublic,
            StateSynchronizationMode? synchronizationMode,
            object initialValue,
            TextSpan declarationSpan,
            TextSpan initializerSpan,
            int ordinal)
            : base(name, type, true, declarationSpan)
        {
            IsPublic = isPublic;
            SynchronizationMode = synchronizationMode;
            InitialValue = initialValue;
            InitializerSpan = initializerSpan;
            Ordinal = ordinal;
        }
    }
}
