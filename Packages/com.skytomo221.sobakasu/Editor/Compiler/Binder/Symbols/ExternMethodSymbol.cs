using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ExternMethodSymbol : MethodSymbol
    {
        public ExternMemberKind MemberKind { get; }
        public override string ExternSignature { get; }
        public override bool UsesExternalCallConversions => true;
        public IReadOnlyList<ExternParameterSymbol> AbiParameters { get; }
        public TypeSymbol AbiReturnType { get; }
        public IReadOnlyList<TypeSymbol> GenericParameters { get; }
        public IReadOnlyList<ExternGenericParameterConstraint> GenericConstraints { get; }
        public IReadOnlyList<TypeSymbol> TypeArguments { get; }
        public bool IsGenericDefinition => GenericParameters.Count > 0 && TypeArguments.Count == 0;
        public bool IsConstructedGenericMethod => TypeArguments.Count > 0;
        public bool UsesAbiAdapter => AbiParameters != null;

        public ExternMethodSymbol(
            string name,
            TypeSymbol containingType,
            IReadOnlyList<ParameterSymbol> parameters,
            TypeSymbol returnType,
            string externSignature,
            bool? isStatic = null,
            ExternMemberKind memberKind = ExternMemberKind.Method,
            IReadOnlyList<ExternParameterSymbol> abiParameters = null,
            TypeSymbol abiReturnType = null,
            IReadOnlyList<TypeSymbol> genericParameters = null,
            IReadOnlyList<ExternGenericParameterConstraint> genericConstraints = null,
            IReadOnlyList<TypeSymbol> typeArguments = null)
            : base(
                name,
                containingType,
                parameters,
                returnType,
                isStatic ?? true)
        {
            ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature));
            MemberKind = memberKind;
            AbiParameters = abiParameters;
            AbiReturnType = abiReturnType ?? returnType;
            GenericParameters = genericParameters ?? Array.Empty<TypeSymbol>();
            GenericConstraints = genericConstraints ?? Array.Empty<ExternGenericParameterConstraint>();
            TypeArguments = typeArguments ?? Array.Empty<TypeSymbol>();
        }

        // Kept only as a source-compatibility bridge for Unity-side discovery code.
        // The value is intentionally discarded and never enters the semantic model.
        public ExternMethodSymbol(
            string name,
            TypeSymbol containingType,
            IReadOnlyList<ParameterSymbol> parameters,
            TypeSymbol returnType,
            object ignoredDiscoveryMetadata,
            string externSignature,
            bool? isStatic = null,
            ExternMemberKind memberKind = ExternMemberKind.Method,
            IReadOnlyList<ExternParameterSymbol> abiParameters = null,
            TypeSymbol abiReturnType = null,
            IReadOnlyList<TypeSymbol> genericParameters = null,
            IReadOnlyList<ExternGenericParameterConstraint> genericConstraints = null,
            IReadOnlyList<TypeSymbol> typeArguments = null)
            : this(name, containingType, parameters, returnType, externSignature, isStatic,
                memberKind, abiParameters, abiReturnType, genericParameters,
                genericConstraints, typeArguments)
        {
        }

        public static TypeSymbol BuildLogicalReturnType(
            TypeSymbol abiReturnType,
            IReadOnlyList<ExternParameterSymbol> parameters)
        {
            var outputs = new List<TypeSymbol>();
            if (abiReturnType != TypeSymbol.Unit)
                outputs.Add(abiReturnType);
            foreach (var parameter in parameters)
            {
                if (parameter.PassingMode == ExternParameterPassingMode.Ref ||
                    parameter.PassingMode == ExternParameterPassingMode.Out)
                    outputs.Add(parameter.LogicalOutputType);
            }
            return outputs.Count == 0 ? TypeSymbol.Unit :
                outputs.Count == 1 ? outputs[0] : TypeSymbol.Tuple(outputs);
        }
    }

    internal enum ExternParameterPassingMode
    {
        Normal,
        Ref,
        Out,
        In,
        GenericTypeArgument
    }

    internal enum ExternLogicalOutputProjection
    {
        Raw,
        Maybe
    }

    internal sealed class ExternMaybeOutputProjection
    {
        public ExternMethodSymbol ValidityMethod { get; }
        public EnumVariantSymbol JustVariant { get; }
        public EnumVariantSymbol NothingVariant { get; }
        public TypeSymbol Type => JustVariant.ContainingType;

        public ExternMaybeOutputProjection(
            ExternMethodSymbol validityMethod,
            EnumVariantSymbol justVariant,
            EnumVariantSymbol nothingVariant)
        {
            ValidityMethod = validityMethod ??
                throw new ArgumentNullException(nameof(validityMethod));
            JustVariant = justVariant ??
                throw new ArgumentNullException(nameof(justVariant));
            NothingVariant = nothingVariant ??
                throw new ArgumentNullException(nameof(nothingVariant));
        }
    }

    internal sealed class ExternParameterSymbol
    {
        public string Name { get; }
        public TypeSymbol Type { get; }
        public ExternParameterPassingMode PassingMode { get; }
        public int LogicalInputOrdinal { get; }
        public ExternLogicalOutputProjection LogicalOutputProjection =>
            MaybeProjection == null
                ? ExternLogicalOutputProjection.Raw
                : ExternLogicalOutputProjection.Maybe;
        public TypeSymbol LogicalOutputType => MaybeProjection?.Type ?? Type;
        public ExternMaybeOutputProjection MaybeProjection { get; }

        public ExternParameterSymbol(
            string name,
            TypeSymbol type,
            ExternParameterPassingMode passingMode,
            int logicalInputOrdinal,
            ExternMaybeOutputProjection maybeProjection = null)
        {
            Name = name ?? string.Empty;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            PassingMode = passingMode;
            LogicalInputOrdinal = logicalInputOrdinal;
            MaybeProjection = maybeProjection;
            if (maybeProjection != null && passingMode != ExternParameterPassingMode.Out)
            {
                throw new ArgumentException(
                    "Maybe output projection is only valid for out parameters.",
                    nameof(maybeProjection));
            }
        }
    }

    internal enum ExternMemberKind
    {
        Method,
        Getter,
        Setter,
        Constructor,
        Operator
    }

    internal enum ExternalInvocationKind
    {
        Static,
        Instance
    }

    internal enum ExternalReturnBindingMode
    {
        Raw,
        Maybe
    }
}
