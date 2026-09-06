using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ArrayIntrinsicSymbols
    {
        public string ConstructorExternSignature { get; }
        public string GetterExternSignature { get; }
        public string SetterExternSignature { get; }
        public string LengthExternSignature { get; }
        public TypeSymbol IndexType { get; }

        public ArrayIntrinsicSymbols(
            string constructorExternSignature,
            string getterExternSignature,
            string setterExternSignature,
            string lengthExternSignature,
            TypeSymbol indexType)
        {
            ConstructorExternSignature = constructorExternSignature ??
                throw new ArgumentNullException(nameof(constructorExternSignature));
            GetterExternSignature = getterExternSignature ??
                throw new ArgumentNullException(nameof(getterExternSignature));
            SetterExternSignature = setterExternSignature ??
                throw new ArgumentNullException(nameof(setterExternSignature));
            LengthExternSignature = lengthExternSignature ??
                throw new ArgumentNullException(nameof(lengthExternSignature));
            IndexType = indexType ?? throw new ArgumentNullException(nameof(indexType));
        }
    }
}
