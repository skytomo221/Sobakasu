using System;
using System.Collections.Generic;
using System.Reflection;
using VRC.Udon.Editor;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ExternCatalog
    {
        private readonly IReadOnlyDictionary<Type, TypeSymbol> _typeSymbolsByClrType;
        private readonly IReadOnlyDictionary<string, TypeSymbol> _typesByQualifiedName;
        private readonly Dictionary<TypeSymbol, Type> _clrTypesByTypeSymbol;
        private readonly IReadOnlyDictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>
            _operatorGroupsByFirstOperandType;
        private readonly UdonExposedNodeCache _exposedNodeCache;

        public NamespaceSymbol GlobalNamespace { get; }

        public ExternCatalog(
            NamespaceSymbol globalNamespace,
            IReadOnlyDictionary<Type, TypeSymbol> typeSymbolsByClrType,
            IReadOnlyDictionary<string, TypeSymbol> typesByQualifiedName,
            UdonExposedNodeCache exposedNodeCache = null,
            IReadOnlyDictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>
                operatorGroupsByFirstOperandType = null)
        {
            GlobalNamespace = globalNamespace ?? throw new ArgumentNullException(nameof(globalNamespace));
            _typeSymbolsByClrType = typeSymbolsByClrType ??
                throw new ArgumentNullException(nameof(typeSymbolsByClrType));
            _typesByQualifiedName = typesByQualifiedName ??
                throw new ArgumentNullException(nameof(typesByQualifiedName));
            _exposedNodeCache = exposedNodeCache;
            _operatorGroupsByFirstOperandType = operatorGroupsByFirstOperandType ??
                new Dictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>();
            _clrTypesByTypeSymbol = new Dictionary<TypeSymbol, Type>();

            foreach (var pair in _typeSymbolsByClrType)
            {
                if (!_clrTypesByTypeSymbol.ContainsKey(pair.Value))
                    _clrTypesByTypeSymbol.Add(pair.Value, pair.Key);
            }
        }

        public bool TryGetTypeSymbol(Type clrType, out TypeSymbol typeSymbol)
        {
            return _typeSymbolsByClrType.TryGetValue(clrType, out typeSymbol);
        }

        public bool TryGetTypeSymbol(string qualifiedName, out TypeSymbol typeSymbol)
        {
            return _typesByQualifiedName.TryGetValue(qualifiedName, out typeSymbol);
        }

        public bool TryGetClrType(TypeSymbol typeSymbol, out Type clrType)
        {
            if (typeSymbol == null)
            {
                clrType = null;
                return false;
            }

            if (_clrTypesByTypeSymbol.TryGetValue(typeSymbol, out clrType))
                return true;

            if (typeSymbol.RuntimeClrType != null)
            {
                clrType = typeSymbol.RuntimeClrType;
                return true;
            }

            if (typeSymbol.TypeKind == TypeKind.Array &&
                TryGetClrType(typeSymbol.ElementType, out var elementType))
            {
                clrType = elementType.MakeArrayType();
                return true;
            }

            if (_typesByQualifiedName.TryGetValue(
                    typeSymbol.RuntimeQualifiedName,
                    out var runtimeTypeSymbol) &&
                _clrTypesByTypeSymbol.TryGetValue(runtimeTypeSymbol, out clrType))
            {
                return true;
            }

            clrType = null;
            return false;
        }

        public TypeSymbol GetRuntimeTypeSymbol(TypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                return TypeSymbol.Error;

            if (TryGetClrType(typeSymbol, out var clrType) &&
                _typeSymbolsByClrType.TryGetValue(clrType, out var canonicalType))
            {
                return canonicalType;
            }

            return _typesByQualifiedName.TryGetValue(
                typeSymbol.RuntimeQualifiedName,
                out var runtimeType)
                ? runtimeType
                : typeSymbol;
        }

        public bool IsTypeExposed(TypeSymbol typeSymbol)
        {
            return TryGetClrType(typeSymbol, out var clrType) &&
                   (_exposedNodeCache?.IsTypeExposed(clrType) ?? true);
        }

        public bool TryGetArrayIntrinsics(
            TypeSymbol arrayType,
            out ArrayIntrinsicSymbols intrinsics,
            out string reason)
        {
            intrinsics = null;
            reason = null;
            if (arrayType == null || arrayType.TypeKind != TypeKind.Array)
            {
                reason = "The requested type is not an array.";
                return false;
            }

            if (!TryGetClrType(arrayType, out var arrayClrType) ||
                !TryGetClrType(arrayType.ElementType, out var elementClrType))
            {
                reason = $"CLR ABI type '{arrayType.RuntimeQualifiedName}' could not be constructed.";
                return false;
            }

            if (!(_exposedNodeCache?.IsTypeExposed(arrayClrType) ?? true))
            {
                reason = $"Udon does not expose ABI type '{arrayClrType.FullName}'.";
                return false;
            }

            var arrayName = UdonExternSignatureFormatter.GetUdonTypeName(arrayClrType);
            var elementName = UdonExternSignatureFormatter.GetUdonTypeName(elementClrType);
            var constructor = $"{arrayName}.__ctor__SystemInt32__{arrayName}";
            var getter = $"{arrayName}.__Get__SystemInt32__{elementName}";
            var setter = $"{arrayName}.__Set__SystemInt32_{elementName}__SystemVoid";
            var length = $"{arrayName}.__get_Length__SystemInt32";

            if (!IsArrayExternExposed(constructor) || !IsArrayExternExposed(length))
            {
                reason = $"Udon does not expose construction and length operations for '{arrayClrType.FullName}'.";
                return false;
            }

            if (!IsArrayExternExposed(getter) || !IsArrayExternExposed(setter))
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(elementClrType))
                {
                    var objectArrayName = UdonExternSignatureFormatter.GetUdonTypeName(typeof(object[]));
                    var objectName = UdonExternSignatureFormatter.GetUdonTypeName(typeof(object));
                    var objectGetter = $"{objectArrayName}.__Get__SystemInt32__{objectName}";
                    var objectSetter = $"{objectArrayName}.__Set__SystemInt32_{objectName}__SystemVoid";
                    if (IsArrayExternExposed(objectGetter) && IsArrayExternExposed(objectSetter))
                    {
                        getter = objectGetter;
                        setter = objectSetter;
                    }
                    else
                    {
                        reason = $"Udon does not expose getter and setter operations for '{arrayClrType.FullName}'.";
                        return false;
                    }
                }
                else
                {
                    reason = $"Udon does not expose getter and setter operations for '{arrayClrType.FullName}'.";
                    return false;
                }
            }

            intrinsics = new ArrayIntrinsicSymbols(
                constructor,
                getter,
                setter,
                length,
                TypeSymbol.I32);
            return true;
        }

        public bool IsPublicArrayType(TypeSymbol arrayType)
        {
            return arrayType != null &&
                arrayType.TypeKind == TypeKind.Array &&
                IsTypeExposed(arrayType);
        }

        private bool IsArrayExternExposed(string signature)
        {
            return _exposedNodeCache?.IsExposed(signature) ?? true;
        }

        public MethodGroupSymbol GetExternalMethodGroup(
            TypeSymbol typeSymbol,
            string memberName)
        {
            return GetRuntimeTypeSymbol(typeSymbol).GetMethodGroup(memberName);
        }

        public MethodGroupSymbol GetExternalOperatorGroup(
            TypeSymbol firstOperandType,
            string operatorName)
        {
            var runtimeType = GetRuntimeTypeSymbol(firstOperandType);
            return _operatorGroupsByFirstOperandType.TryGetValue(runtimeType, out var groups) &&
                groups.TryGetValue(operatorName, out var group)
                ? group
                : null;
        }

        public bool TryLookupSymbol(string qualifiedPath, out Symbol symbol)
        {
            symbol = null;
            if (string.IsNullOrWhiteSpace(qualifiedPath))
                return false;

            var segments = qualifiedPath.Split('.');
            symbol = GlobalNamespace;

            for (var index = 0; index < segments.Length; index++)
            {
                var segment = segments[index];
                if (symbol is NamespaceSymbol namespaceSymbol)
                {
                    symbol = namespaceSymbol.Lookup(segment);
                }
                else if (symbol is TypeSymbol typeSymbol && index == segments.Length - 1)
                {
                    symbol = typeSymbol.GetMethodGroup(segment);
                }
                else
                {
                    symbol = null;
                }

                if (symbol == null)
                    return false;
            }

            return true;
        }

        public IReadOnlyList<string> GetUnaryOperatorSignatures(
            string operatorName,
            TypeSymbol operandType,
            TypeSymbol resultType)
        {
            if (!TryGetClrType(operandType, out var operandClrType) ||
                !TryGetClrType(resultType, out var resultClrType))
            {
                return Array.Empty<string>();
            }

            return GetOperatorSignatures(
                operandClrType,
                operatorName,
                new[] { operandClrType },
                resultClrType);
        }

        public IReadOnlyList<string> GetBinaryOperatorSignatures(
            string operatorName,
            TypeSymbol leftType,
            TypeSymbol rightType,
            TypeSymbol resultType)
        {
            if (!TryGetClrType(leftType, out var leftClrType) ||
                !TryGetClrType(rightType, out var rightClrType) ||
                !TryGetClrType(resultType, out var resultClrType))
            {
                return Array.Empty<string>();
            }

            return GetOperatorSignatures(
                leftClrType,
                operatorName,
                new[] { leftClrType, rightClrType },
                resultClrType);
        }

        private IReadOnlyList<string> GetOperatorSignatures(
            Type declaringClrType,
            string operatorName,
            IReadOnlyList<Type> parameterTypes,
            Type resultClrType)
        {
            if (_exposedNodeCache == null ||
                string.IsNullOrWhiteSpace(operatorName) ||
                declaringClrType == null ||
                resultClrType == null)
            {
                return Array.Empty<string>();
            }

            var signatures = new List<string>();
            var operatorNames = GetOperatorNameVariants(operatorName);
            const BindingFlags operatorFlags = BindingFlags.Public | BindingFlags.Static;

            foreach (var method in declaringClrType.GetMethods(operatorFlags))
            {
                if (!method.IsSpecialName ||
                    !string.Equals(method.Name, operatorName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!HasExactParameterSignature(method, parameterTypes, resultClrType))
                    continue;

                var signature = UdonExternSignatureFormatter.GetUdonMethodName(method);
                if (_exposedNodeCache.IsExposed(signature))
                    AddUnique(signatures, signature);
            }

            foreach (var operatorNameVariant in operatorNames)
            {
                var exactSignature = BuildOperatorExternSignature(
                    declaringClrType,
                    operatorNameVariant,
                    parameterTypes,
                    resultClrType);
                if (_exposedNodeCache.IsExposed(exactSignature))
                    AddUnique(signatures, exactSignature);
            }

            var expectedSuffix = BuildOperatorExternSignatureSuffix(parameterTypes, resultClrType);
            foreach (var exposedSignature in _exposedNodeCache.ExposedSignatures)
            {
                if (!exposedSignature.EndsWith(expectedSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var operatorNameVariant in operatorNames)
                {
                    var operatorMarker = $".__{operatorNameVariant}__";
                    if (exposedSignature.IndexOf(operatorMarker, StringComparison.Ordinal) >= 0)
                        AddUnique(signatures, exposedSignature);
                }
            }

            return signatures.ToArray();
        }

        private static bool HasExactParameterSignature(
            MethodInfo method,
            IReadOnlyList<Type> parameterTypes,
            Type resultClrType)
        {
            if (method.ReturnType != resultClrType)
                return false;

            var parameters = method.GetParameters();
            if (parameters.Length != parameterTypes.Count)
                return false;

            for (var index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].ParameterType != parameterTypes[index])
                    return false;
            }

            return true;
        }

        internal static string BuildOperatorExternSignature(
            Type declaringClrType,
            string operatorName,
            IReadOnlyList<Type> parameterTypes,
            Type resultClrType)
        {
            return $"{UdonExternSignatureFormatter.GetUdonTypeName(declaringClrType)}.__{operatorName}{BuildOperatorExternSignatureSuffix(parameterTypes, resultClrType)}";
        }

        private static string BuildOperatorExternSignatureSuffix(
            IReadOnlyList<Type> parameterTypes,
            Type resultClrType)
        {
            var suffix = "__";
            for (var index = 0; index < parameterTypes.Count; index++)
            {
                if (index > 0)
                    suffix += "_";

                suffix += UdonExternSignatureFormatter.GetUdonTypeName(parameterTypes[index]);
            }

            suffix += $"__{UdonExternSignatureFormatter.GetUdonTypeName(resultClrType)}";
            return suffix;
        }

        internal static IReadOnlyList<string> GetOperatorNameVariants(string operatorName)
        {
            var operatorNames = new List<string>();
            AddUnique(operatorNames, operatorName);

            switch (operatorName)
            {
                case "op_Multiply":
                    AddUnique(operatorNames, "op_Multiplication");
                    break;

                case "op_Modulus":
                    AddUnique(operatorNames, "op_Remainder");
                    break;

                case "op_BitwiseAnd":
                    AddUnique(operatorNames, "op_LogicalAnd");
                    break;

                case "op_BitwiseOr":
                    AddUnique(operatorNames, "op_LogicalOr");
                    break;

                case "op_ExclusiveOr":
                    AddUnique(operatorNames, "op_LogicalXor");
                    break;

                case "op_LogicalNot":
                    AddUnique(operatorNames, "op_UnaryNegation");
                    break;

                case "op_UnaryNegation":
                    AddUnique(operatorNames, "op_UnaryMinus");
                    break;

                case "op_OnesComplement":
                    AddUnique(operatorNames, "op_BitwiseNot");
                    break;
            }

            return operatorNames.ToArray();
        }

        internal static bool TryResolveOperatorExternSignature(
            MethodInfo method,
            Func<string, bool> isExposed,
            out string externSignature)
        {
            externSignature = null;
            if (method == null || isExposed == null ||
                !method.Name.StartsWith("op_", StringComparison.Ordinal))
            {
                return false;
            }

            var parameters = method.GetParameters();
            var parameterTypes = new Type[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
                parameterTypes[index] = parameters[index].ParameterType;
            foreach (var name in GetOperatorNameVariants(method.Name))
            {
                var candidate = BuildOperatorExternSignature(
                    method.DeclaringType,
                    name,
                    parameterTypes,
                    method.ReturnType);
                if (!isExposed(candidate))
                    continue;
                externSignature = candidate;
                return true;
            }

            return false;
        }

        private static void AddUnique(ICollection<string> signatures, string signature)
        {
            if (string.IsNullOrEmpty(signature) || signatures.Contains(signature))
                return;

            signatures.Add(signature);
        }
    }

}
