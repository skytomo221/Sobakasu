using System;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler
{
    public sealed class HeapPatchEntry
    {
        public string SymbolName { get; }
        public TypeKind SymbolType { get; }
        public object RuntimeValue { get; }
        public string RuntimeTypeName { get; }
        public HeapPatchKind Kind { get; }
        public TextSpan? SourceSpan { get; }

        public HeapPatchEntry(string symbolName, TypeKind symbolType, object runtimeValue, HeapPatchKind kind, TextSpan? sourceSpan = null, string runtimeTypeName = null)
        {
            SymbolName = symbolName ?? throw new ArgumentNullException(nameof(symbolName));
            SymbolType = symbolType;
            RuntimeValue = runtimeValue;
            RuntimeTypeName = runtimeTypeName;
            Kind = kind;
            SourceSpan = sourceSpan;
        }
    }
}
