using System;
using System.Collections.Generic;
using System.Linq;
using Skytomo221.Sobakasu.Compiler.Target;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal enum ExternTypeShape { Void, Value, Reference, Enum }

    internal enum GenericArgumentValidationMode
    {
        Concrete,
        DeferredForwarding
    }

    internal readonly struct GenericArgumentValidationContext
    {
        private readonly IReadOnlyCollection<TypeSymbol> _forwardableParameters;

        public GenericArgumentValidationMode Mode { get; }

        private GenericArgumentValidationContext(
            GenericArgumentValidationMode mode,
            IReadOnlyCollection<TypeSymbol> forwardableParameters)
        {
            Mode = mode;
            _forwardableParameters = forwardableParameters;
        }

        public static GenericArgumentValidationContext Concrete =>
            new(GenericArgumentValidationMode.Concrete, null);

        public static GenericArgumentValidationContext DeferredForwarding(
            IReadOnlyCollection<TypeSymbol> forwardableParameters) =>
            new(GenericArgumentValidationMode.DeferredForwarding,
                forwardableParameters);

        public bool CanDefer(TypeSymbol argument) =>
            Mode == GenericArgumentValidationMode.DeferredForwarding &&
            _forwardableParameters != null &&
            _forwardableParameters.Contains(argument);
    }

    internal readonly struct ExternSourceTypeKey : IEquatable<ExternSourceTypeKey>
    {
        public string QualifiedName { get; }
        public int GenericArity { get; }

        public ExternSourceTypeKey(string qualifiedName, int genericArity)
        {
            QualifiedName = qualifiedName ?? throw new ArgumentNullException(nameof(qualifiedName));
            GenericArity = genericArity;
        }

        public bool Equals(ExternSourceTypeKey other) =>
            GenericArity == other.GenericArity &&
            string.Equals(QualifiedName, other.QualifiedName,
                StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ExternSourceTypeKey other && Equals(other);

        public override int GetHashCode() =>
            (StringComparer.Ordinal.GetHashCode(QualifiedName) * 397) ^
            GenericArity;
    }

    internal sealed class ExternEnumConstant
    {
        public string Name { get; }
        public string Value { get; }
        public ExternEnumConstant(string name, string value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    internal sealed class ExternEnumMetadata
    {
        public TypeSymbol UnderlyingType { get; }
        public IReadOnlyDictionary<string, ExternEnumConstant> Constants { get; }
        public ExternEnumMetadata(TypeSymbol underlyingType, IReadOnlyDictionary<string, ExternEnumConstant> constants)
        {
            UnderlyingType = underlyingType ?? throw new ArgumentNullException(nameof(underlyingType));
            Constants = constants ?? throw new ArgumentNullException(nameof(constants));
        }
    }

    internal sealed class ExternTypeMetadata
    {
        public RuntimeTypeIdentity Identity { get; }
        public ExternTypeShape Shape { get; }
        public int GenericArity { get; }
        public IReadOnlyCollection<RuntimeTypeIdentity> Supertypes { get; }
        public bool IsAbiAvailable { get; }
        public bool SatisfiesDefaultConstructorConstraint { get; }
        public ExternEnumMetadata Enum { get; }
        public ExternTypeMetadata(RuntimeTypeIdentity identity, ExternTypeShape shape, int genericArity, IReadOnlyCollection<RuntimeTypeIdentity> supertypes, bool isAbiAvailable, bool satisfiesDefaultConstructorConstraint, ExternEnumMetadata @enum)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Shape = shape;
            GenericArity = genericArity;
            Supertypes = supertypes ?? Array.Empty<RuntimeTypeIdentity>();
            IsAbiAvailable = isAbiAvailable;
            SatisfiesDefaultConstructorConstraint = satisfiesDefaultConstructorConstraint;
            Enum = @enum;
        }
    }

    internal readonly struct SynchronizationCapability
    {
        public bool None { get; }
        public bool Linear { get; }
        public bool Smooth { get; }

        public SynchronizationCapability(bool none, bool linear, bool smooth)
        {
            None = none;
            Linear = linear;
            Smooth = smooth;
        }

        public bool IsSupported(StateSynchronizationMode mode)
        {
            return mode switch
            {
                StateSynchronizationMode.None => None,
                StateSynchronizationMode.Linear => Linear,
                StateSynchronizationMode.Smooth => Smooth,
                _ => false
            };
        }
    }

    internal sealed class ExternCatalog
    {
        private readonly IReadOnlyDictionary<string, TypeSymbol> _typesByRuntimeName;
        private readonly IReadOnlyDictionary<ExternSourceTypeKey, TypeSymbol> _typesBySourceName;
        private readonly IReadOnlyDictionary<TypeSymbol, ExternTypeMetadata> _metadataByType;
        private readonly IReadOnlyDictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>> _memberGroups;
        private readonly IReadOnlyDictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>> _operatorGroups;
        private readonly IReadOnlyDictionary<RuntimeTypeIdentity, ArrayIntrinsicSymbols> _arrayIntrinsics;
        private readonly IReadOnlyDictionary<RuntimeTypeIdentity, SynchronizationCapability> _synchronizationCapabilities;
        public NamespaceSymbol GlobalNamespace { get; }

        public ExternCatalog(NamespaceSymbol globalNamespace, IReadOnlyDictionary<string, TypeSymbol> typesByRuntimeName, IReadOnlyDictionary<ExternSourceTypeKey, TypeSymbol> typesBySourceName, IReadOnlyDictionary<TypeSymbol, ExternTypeMetadata> metadataByType, IReadOnlyDictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>> memberGroups, IReadOnlyDictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>> operatorGroups, IReadOnlyDictionary<RuntimeTypeIdentity, ArrayIntrinsicSymbols> arrayIntrinsics, IReadOnlyDictionary<RuntimeTypeIdentity, SynchronizationCapability> synchronizationCapabilities = null)
        {
            GlobalNamespace = globalNamespace ?? throw new ArgumentNullException(nameof(globalNamespace));
            _typesByRuntimeName = typesByRuntimeName ?? throw new ArgumentNullException(nameof(typesByRuntimeName));
            _typesBySourceName = typesBySourceName ?? CreateSourceTypeIndex(typesByRuntimeName);
            _metadataByType = metadataByType ?? throw new ArgumentNullException(nameof(metadataByType));
            _memberGroups = memberGroups ?? new Dictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>();
            _operatorGroups = operatorGroups ?? new Dictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>();
            _arrayIntrinsics = arrayIntrinsics ?? new Dictionary<RuntimeTypeIdentity, ArrayIntrinsicSymbols>();
            _synchronizationCapabilities = synchronizationCapabilities ??
                new Dictionary<RuntimeTypeIdentity, SynchronizationCapability>();
        }

        public ExternCatalog(NamespaceSymbol globalNamespace, IReadOnlyDictionary<string, TypeSymbol> typesByRuntimeName, IReadOnlyDictionary<TypeSymbol, ExternTypeMetadata> metadataByType, IReadOnlyDictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>> memberGroups, IReadOnlyDictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>> operatorGroups, IReadOnlyDictionary<RuntimeTypeIdentity, ArrayIntrinsicSymbols> arrayIntrinsics, IReadOnlyDictionary<RuntimeTypeIdentity, SynchronizationCapability> synchronizationCapabilities = null)
            : this(globalNamespace, typesByRuntimeName, null, metadataByType,
                memberGroups, operatorGroups, arrayIntrinsics, synchronizationCapabilities)
        {
        }

        public bool TryGetTypeSymbol(string runtimeName, out TypeSymbol typeSymbol) => _typesByRuntimeName.TryGetValue(runtimeName, out typeSymbol);

        public bool TryGetSourceTypeSymbol(
            string qualifiedName,
            int genericArity,
            out TypeSymbol typeSymbol) =>
            _typesBySourceName.TryGetValue(
                new ExternSourceTypeKey(qualifiedName, genericArity),
                out typeSymbol);

        public bool TryGetRuntimeTypeIdentity(TypeSymbol type, out RuntimeTypeIdentity identity)
        {
            identity = type?.RuntimeTypeIdentity;
            return identity != null;
        }

        public TypeSymbol GetRuntimeTypeSymbol(TypeSymbol type)
        {
            if (type == null)
                return TypeSymbol.Error;

            if (type.TypeKind == TypeKind.Array)
            {
                var element = GetRuntimeTypeSymbol(type.ElementType);
                return ReferenceEquals(element, type.ElementType)
                    ? type
                    : TypeSymbol.Array(element);
            }

            if (type.IsConstructedGenericType)
            {
                var definition = GetRuntimeTypeSymbol(type.GenericDefinition);
                if (!definition.IsGenericDefinition ||
                    definition.GenericParameters.Count != type.TypeArguments.Count)
                {
                    return type;
                }

                var arguments = new TypeSymbol[type.TypeArguments.Count];
                for (var index = 0; index < arguments.Length; index++)
                    arguments[index] = GetRuntimeTypeSymbol(type.TypeArguments[index]);
                return definition.Construct(arguments);
            }

            return type.RuntimeTypeIdentity != null &&
                   _typesByRuntimeName.TryGetValue(
                       type.RuntimeTypeIdentity.RuntimeName,
                       out var canonical)
                ? canonical
                : type;
        }

        private TypeSymbol GetCatalogDefinitionTypeSymbol(TypeSymbol type)
        {
            var runtimeType = GetRuntimeTypeSymbol(type);
            return runtimeType.IsConstructedGenericType
                ? runtimeType.GenericDefinition
                : runtimeType;
        }

        public bool TryGetTypeMetadata(TypeSymbol type, out ExternTypeMetadata metadata) =>
            _metadataByType.TryGetValue(GetCatalogDefinitionTypeSymbol(type), out metadata);
        public ExternTypeShape GetTypeShape(TypeSymbol type) => TryGetTypeMetadata(type, out var metadata) ? metadata.Shape : ExternTypeShape.Reference;

        public bool IsAbiTypeAvailable(TypeSymbol type)
        {
            if (type == null || type == TypeSymbol.Unit || type == TypeSymbol.Never || type.ContainsGenericParameters) return false;
            return type.TypeKind == TypeKind.Array
                ? TryGetArrayIntrinsics(type, out _, out _)
                : TryGetTypeMetadata(type, out var metadata) && metadata.IsAbiAvailable;
        }

        public bool TryGetArrayIntrinsics(TypeSymbol arrayType, out ArrayIntrinsicSymbols intrinsics, out string reason)
        {
            intrinsics = null;
            reason = null;
            if (arrayType?.TypeKind != TypeKind.Array || arrayType.RuntimeTypeIdentity == null)
            {
                reason = "The requested type is not a catalog-backed array.";
                return false;
            }
            if (_arrayIntrinsics.TryGetValue(arrayType.RuntimeTypeIdentity, out intrinsics)) return true;
            reason = $"Udon does not expose array ABI operations for '{arrayType.RuntimeQualifiedName}'.";
            return false;
        }

        public bool IsPublicArrayType(TypeSymbol arrayType) => arrayType?.TypeKind == TypeKind.Array && TryGetArrayIntrinsics(arrayType, out _, out _);

        public bool IsSynchronizationSupported(
            TypeSymbol type,
            StateSynchronizationMode mode)
        {
            if (type == null || type == TypeSymbol.Error)
                return false;

            var runtimeType = GetRuntimeTypeSymbol(type);
            var identity = runtimeType?.RuntimeTypeIdentity ?? type.RuntimeTypeIdentity;
            return identity != null &&
                _synchronizationCapabilities.TryGetValue(identity, out var capability) &&
                capability.IsSupported(mode);
        }

        public bool TryGetEnumConstant(TypeSymbol enumType, string name, out ExternEnumConstant constant)
        {
            constant = null;
            return TryGetTypeMetadata(enumType, out var metadata) && metadata.Enum != null && metadata.Enum.Constants.TryGetValue(name, out constant);
        }

        public MethodGroupSymbol GetExternalMethodGroup(
            TypeSymbol type,
            string memberName)
        {
            var runtimeType = GetCatalogDefinitionTypeSymbol(type);
            if (!_memberGroups.TryGetValue(runtimeType, out var groups))
                return null;
            return groups.TryGetValue(memberName, out var group)
                ? group
                : null;
        }
        public MethodGroupSymbol GetExternalOperatorGroup(TypeSymbol firstOperandType, string operatorName) =>
            _operatorGroups.TryGetValue(
                GetCatalogDefinitionTypeSymbol(firstOperandType),
                out var groups) &&
            groups.TryGetValue(operatorName, out var group)
                ? group
                : null;

        public IReadOnlyList<string> GetUnaryOperatorSignatures(
            string operatorName,
            TypeSymbol operandType,
            TypeSymbol resultType)
        {
            var signatures = GetOperatorSignatures(
                operatorName,
                operandType,
                new[] { operandType },
                resultType);
            if (signatures.Count != 0 ||
                !string.Equals(
                    operatorName,
                    "op_LogicalNot",
                    StringComparison.Ordinal))
            {
                return signatures;
            }

            // Udon exposes Boolean logical negation under the physical ABI name
            // op_UnaryNegation. Keep the source semantic name op_LogicalNot and
            // normalize only at the catalog adapter boundary.
            return GetOperatorSignatures(
                "op_UnaryNegation",
                operandType,
                new[] { operandType },
                resultType);
        }

        public IReadOnlyList<string> GetBinaryOperatorSignatures(
            string operatorName,
            TypeSymbol leftType,
            TypeSymbol rightType,
            TypeSymbol resultType)
        {
            return GetOperatorSignatures(operatorName, leftType, new[] { leftType, rightType }, resultType);
        }

        private IReadOnlyList<string> GetOperatorSignatures(
            string operatorName,
            TypeSymbol firstOperandType,
            IReadOnlyList<TypeSymbol> parameterTypes,
            TypeSymbol resultType)
        {
            var group = GetExternalOperatorGroup(firstOperandType, operatorName);
            if (group == null)
                return Array.Empty<string>();

            var signatures = new List<string>();
            foreach (var method in group.Methods)
            {
                if (method is not ExternMethodSymbol external ||
                    !external.IsStatic || external.MemberKind != ExternMemberKind.Operator ||
                    external.Parameters.Count != parameterTypes.Count ||
                    external.ReturnType != resultType)
                    continue;
                var matches = true;
                for (var index = 0; index < parameterTypes.Count; index++)
                    matches &= external.Parameters[index].Type == parameterTypes[index];
                if (matches && !signatures.Contains(external.ExternSignature))
                    signatures.Add(external.ExternSignature);
            }
            return signatures;
        }

        public bool ValidateGenericArguments(ExternMethodSymbol definition, IReadOnlyList<TypeSymbol> arguments, out string reason) =>
            ValidateGenericArguments(definition, arguments,
                GenericArgumentValidationContext.Concrete, out reason);

        public bool ValidateGenericArguments(
            ExternMethodSymbol definition,
            IReadOnlyList<TypeSymbol> arguments,
            GenericArgumentValidationContext validationContext,
            out string reason)
        {
            reason = null;
            if (definition == null || arguments == null || definition.GenericParameters.Count != arguments.Count)
            {
                reason = "Generic argument arity does not match.";
                return false;
            }
            foreach (var constraint in definition.GenericConstraints)
            {
                var argument = arguments[constraint.Parameter.GenericParameterOrdinal];
                if (argument.IsGenericParameter)
                {
                    if (validationContext.CanDefer(argument))
                        continue;
                    reason = $"Generic parameter '{argument.Name}' cannot be used as a deferred external generic argument here.";
                    return false;
                }
                if (!TryGetTypeMetadata(argument, out var metadata) ||
                    constraint.RequiresReferenceType && metadata.Shape != ExternTypeShape.Reference ||
                    constraint.RequiresNonNullableValueType && metadata.Shape != ExternTypeShape.Value && metadata.Shape != ExternTypeShape.Enum ||
                    constraint.RequiresDefaultConstructor && !metadata.SatisfiesDefaultConstructorConstraint)
                {
                    reason = $"Type argument '{argument.Name}' does not satisfy generic constraints.";
                    return false;
                }
                foreach (var required in constraint.ConstraintTypes)
                {
                    if (!TryGetRuntimeTypeIdentity(required, out var requiredIdentity) ||
                        !TryGetRuntimeTypeIdentity(argument, out var argumentIdentity) ||
                        !argumentIdentity.Equals(requiredIdentity) && !metadata.Supertypes.Contains(requiredIdentity))
                    {
                        reason = $"Type argument '{argument.Name}' does not satisfy generic constraints.";
                        return false;
                    }
                }
            }
            return true;
        }

        internal static string GetSourceQualifiedName(string runtimeName)
        {
            var runtimeSegments = runtimeName.Replace('+', '.').Split('.');
            for (var index = 0; index < runtimeSegments.Length; index++)
            {
                var aritySeparator = runtimeSegments[index].IndexOf('`');
                if (aritySeparator >= 0)
                    runtimeSegments[index] = runtimeSegments[index]
                        .Substring(0, aritySeparator);
            }
            return string.Join(".", runtimeSegments);
        }

        private static IReadOnlyDictionary<ExternSourceTypeKey, TypeSymbol>
            CreateSourceTypeIndex(
                IReadOnlyDictionary<string, TypeSymbol> typesByRuntimeName)
        {
            var result = new Dictionary<ExternSourceTypeKey, TypeSymbol>();
            foreach (var pair in typesByRuntimeName)
            {
                var key = new ExternSourceTypeKey(
                    GetSourceQualifiedName(pair.Key),
                    pair.Value.GenericParameters.Count);
                if (!result.ContainsKey(key))
                    result.Add(key, pair.Value);
            }
            return result;
        }

        public bool TryLookupSymbol(string qualifiedPath, out Symbol symbol)
        {
            symbol = null;
            if (string.IsNullOrWhiteSpace(qualifiedPath)) return false;
            symbol = GlobalNamespace;
            var segments = qualifiedPath.Split('.');
            for (var index = 0; index < segments.Length; index++)
            {
                if (symbol is NamespaceSymbol namespaceSymbol) symbol = namespaceSymbol.Lookup(segments[index]);
                else if (symbol is TypeSymbol typeSymbol && index == segments.Length - 1) symbol = GetExternalMethodGroup(typeSymbol, segments[index]);
                else symbol = null;
                if (symbol == null) return false;
            }
            return true;
        }

    }
}
