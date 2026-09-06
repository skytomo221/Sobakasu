using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal interface ICallableSymbol
    {
        IReadOnlyList<ParameterSymbol> Parameters { get; }
        TypeSymbol ReturnType { get; }
        bool UsesExternalCallConversions { get; }
    }
}
