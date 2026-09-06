using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ExternalFunctionBinding
    {
        public FunctionSymbol SobakasuSymbol { get; }
        public ExternMethodSymbol ExternalMethod { get; }
        public TypeSymbol ExternalDeclaringType => ExternalMethod.ContainingType;
        public string ExternalMemberName => ExternalMethod.Name;
        public string ResolvedExternalSignature => ExternalMethod.ExternSignature;
        public ExternalInvocationKind InvocationKind { get; }
        public ExternMemberKind MemberKind => ExternalMethod.MemberKind;
        public ExternalReturnBindingMode ReturnBindingMode { get; }

        public ExternalFunctionBinding(
            FunctionSymbol sobakasuSymbol,
            ExternMethodSymbol externalMethod,
            ExternalReturnBindingMode returnBindingMode)
        {
            SobakasuSymbol = sobakasuSymbol ??
                throw new ArgumentNullException(nameof(sobakasuSymbol));
            ExternalMethod = externalMethod ??
                throw new ArgumentNullException(nameof(externalMethod));
            InvocationKind = externalMethod.IsStatic
                ? ExternalInvocationKind.Static
                : ExternalInvocationKind.Instance;
            ReturnBindingMode = returnBindingMode;
        }
    }
}
