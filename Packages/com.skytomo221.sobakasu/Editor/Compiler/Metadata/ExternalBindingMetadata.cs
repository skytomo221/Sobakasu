using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{
    public sealed class ExternalBindingMetadata
    {
        public string SobakasuSymbol { get; }
        public string SobakasuName { get; }
        public string DeclaringModule { get; }
        public IReadOnlyList<string> SobakasuParameterTypes { get; }
        public string SobakasuReturnType { get; }
        public string ExternalDeclaringType { get; }
        public string ExternalMemberName { get; }
        public IReadOnlyList<string> ExternalParameterTypes { get; }
        public IReadOnlyList<ExternalParameterPassingMode> ExternalParameterModes { get; }
        public IReadOnlyList<ExternalParameterOutputProjection> ExternalParameterOutputProjections { get; }
        public string ExternalReturnType { get; }
        public string ResolvedExternalSignature { get; }
        public ExternalBindingInvocationKind InvocationKind { get; }
        public ExternalBindingMemberKind MemberKind { get; }
        public ExternalBindingReturnMode ReturnMode { get; }

        public ExternalBindingMetadata(string sobakasuSymbol, string sobakasuName, string declaringModule, IReadOnlyList<string> sobakasuParameterTypes, string sobakasuReturnType, string externalDeclaringType, string externalMemberName, IReadOnlyList<string> externalParameterTypes, IReadOnlyList<ExternalParameterPassingMode> externalParameterModes, string externalReturnType, string resolvedExternalSignature, ExternalBindingInvocationKind invocationKind, ExternalBindingMemberKind memberKind, ExternalBindingReturnMode returnMode)
            : this(sobakasuSymbol, sobakasuName, declaringModule, sobakasuParameterTypes, sobakasuReturnType, externalDeclaringType, externalMemberName, externalParameterTypes, externalParameterModes, externalReturnType, resolvedExternalSignature, invocationKind, memberKind, returnMode, CreateRawParameterProjections(externalParameterTypes?.Count ?? 0))
        {
        }

        public ExternalBindingMetadata(string sobakasuSymbol, string sobakasuName, string declaringModule, IReadOnlyList<string> sobakasuParameterTypes, string sobakasuReturnType, string externalDeclaringType, string externalMemberName, IReadOnlyList<string> externalParameterTypes, IReadOnlyList<ExternalParameterPassingMode> externalParameterModes, string externalReturnType, string resolvedExternalSignature, ExternalBindingInvocationKind invocationKind, ExternalBindingMemberKind memberKind, ExternalBindingReturnMode returnMode, IReadOnlyList<ExternalParameterOutputProjection> externalParameterOutputProjections)
        {
            SobakasuSymbol = sobakasuSymbol ?? throw new ArgumentNullException(nameof(sobakasuSymbol));
            SobakasuName = sobakasuName ?? throw new ArgumentNullException(nameof(sobakasuName));
            DeclaringModule = declaringModule ?? string.Empty;
            SobakasuParameterTypes = sobakasuParameterTypes ?? Array.Empty<string>();
            SobakasuReturnType = sobakasuReturnType ?? throw new ArgumentNullException(nameof(sobakasuReturnType));
            ExternalDeclaringType = externalDeclaringType ?? throw new ArgumentNullException(nameof(externalDeclaringType));
            ExternalMemberName = externalMemberName ?? throw new ArgumentNullException(nameof(externalMemberName));
            ExternalParameterTypes = externalParameterTypes ?? Array.Empty<string>();
            ExternalParameterModes = externalParameterModes ?? Array.Empty<ExternalParameterPassingMode>();
            ExternalParameterOutputProjections = externalParameterOutputProjections ?? Array.Empty<ExternalParameterOutputProjection>();
            ExternalReturnType = externalReturnType ?? throw new ArgumentNullException(nameof(externalReturnType));
            ResolvedExternalSignature = resolvedExternalSignature ?? throw new ArgumentNullException(nameof(resolvedExternalSignature));
            InvocationKind = invocationKind;
            MemberKind = memberKind;
            ReturnMode = returnMode;
        }

        private static IReadOnlyList<ExternalParameterOutputProjection> CreateRawParameterProjections(int count)
        {
            if (count <= 0)
                return Array.Empty<ExternalParameterOutputProjection>();
            return new ExternalParameterOutputProjection[count];
        }
    }
}
