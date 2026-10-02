using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog
{
    internal static class UdonApiCatalogLoader
    {
        private const int SupportedFormatVersion = 1;

        public static ExternCatalog Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("The Udon API catalog is empty.");
            var data = JsonConvert.DeserializeObject<UdonApiCatalogData>(json);
            if (data == null)
                throw new InvalidDataException("The Udon API catalog is empty.");
            return Load(data);
        }

        public static ExternCatalog Load(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            return Load(reader.ReadToEnd());
        }

        private static ExternCatalog Load(UdonApiCatalogData data)
        {
            if (data.formatVersion != SupportedFormatVersion)
                throw new InvalidDataException($"Unsupported Udon API catalog format version '{data.formatVersion}'.");

            var global = new NamespaceSymbol("<global>", "");
            var types = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal);
            var records = new Dictionary<string, UdonApiTypeRecord>(StringComparer.Ordinal);
            var unexposedTypeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var runtimeName in data.unexposedClrTypeNames ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(runtimeName) || !unexposedTypeNames.Add(runtimeName))
                    throw new InvalidDataException("The catalog contains a duplicate or invalid unexposed runtime type.");
            }
            foreach (var record in data.types ?? new List<UdonApiTypeRecord>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.runtimeName) || records.ContainsKey(record.runtimeName))
                    throw new InvalidDataException("The catalog contains a duplicate or invalid runtime type.");
                records.Add(record.runtimeName, record);
                var symbol = TryGetBuiltIn(record.runtimeName, out var builtIn)
                    ? builtIn
                    : TypeSymbol.CreateNamed(GetSimpleName(record.runtimeName), GetQualifiedName(record.runtimeName),
                        !string.Equals(record.shape, "Value", StringComparison.Ordinal) && !string.Equals(record.shape, "Enum", StringComparison.Ordinal),
                        RuntimeTypeIdentity.Named(record.runtimeName), isExternalBinding: true);
                types.Add(record.runtimeName, symbol);
                AddToNamespace(global, record.runtimeName, symbol);
            }

            // Older format-version-1 catalogs contain metadata only for exposed
            // types while still referring to unexposed base/interface types in
            // supertypes.  Those names are declared by unexposedClrTypeNames;
            // create non-ABI placeholder symbols so the persisted graph remains
            // closed without falling back to reflection.
            foreach (var runtimeName in unexposedTypeNames)
            {
                if (types.ContainsKey(runtimeName))
                    continue;
                var symbol = TypeSymbol.CreateNamed(
                    GetSimpleName(runtimeName),
                    GetQualifiedName(runtimeName),
                    isReferenceType: true,
                    RuntimeTypeIdentity.Named(runtimeName),
                    isExternalBinding: true);
                types.Add(runtimeName, symbol);
                AddToNamespace(global, runtimeName, symbol);
            }

            foreach (var pair in records)
            {
                var symbol = types[pair.Key];
                if (pair.Value.genericArity < 0)
                    throw new InvalidDataException($"Type '{pair.Key}' has an invalid generic arity.");
                if (pair.Value.genericArity > 0)
                {
                    var parameters = new TypeSymbol[pair.Value.genericArity];
                    for (var index = 0; index < parameters.Length; index++)
                        parameters[index] = TypeSymbol.CreateGenericParameter($"T{index}", symbol, index, pair.Key);
                    symbol.SetGenericParameters(parameters);
                }
            }

            var sourceTypes = new Dictionary<ExternSourceTypeKey, TypeSymbol>();
            foreach (var pair in records)
            {
                var key = new ExternSourceTypeKey(
                    ExternCatalog.GetSourceQualifiedName(pair.Key),
                    pair.Value.genericArity);
                if (sourceTypes.ContainsKey(key))
                    throw new InvalidDataException($"The catalog contains a duplicate source type '{key.QualifiedName}' with generic arity '{key.GenericArity}'.");
                sourceTypes.Add(key, types[pair.Key]);
            }

            var metadata = new Dictionary<TypeSymbol, ExternTypeMetadata>();
            foreach (var pair in records)
            {
                var record = pair.Value;
                var supertypes = new List<RuntimeTypeIdentity>();
                foreach (var supertype in record.supertypes ?? new List<ExternTypeRef>())
                    supertypes.Add(ResolveIdentity(supertype, types, null, pair.Key));
                ExternEnumMetadata enumMetadata = null;
                if (record.@enum != null)
                {
                    var constants = new Dictionary<string, ExternEnumConstant>(StringComparer.Ordinal);
                    foreach (var constant in record.@enum.constants ?? new List<UdonApiEnumConstantRecord>())
                    {
                        if (constant == null || string.IsNullOrWhiteSpace(constant.name) || constants.ContainsKey(constant.name))
                            throw new InvalidDataException($"Enum '{pair.Key}' has a duplicate or invalid constant.");
                        constants.Add(constant.name, new ExternEnumConstant(constant.name, constant.value ?? string.Empty));
                    }
                    enumMetadata = new ExternEnumMetadata(ResolveType(record.@enum.underlyingType, types, null, pair.Key), constants);
                }
                metadata.Add(types[pair.Key], new ExternTypeMetadata(
                    RuntimeTypeIdentity.Named(pair.Key),
                    ParseShape(record.shape, pair.Key),
                    record.genericArity,
                    supertypes,
                    !unexposedTypeNames.Contains(pair.Key),
                    record.satisfiesDefaultConstructorConstraint,
                    enumMetadata));
            }

            foreach (var runtimeName in unexposedTypeNames)
            {
                if (records.ContainsKey(runtimeName))
                    continue;
                metadata.Add(types[runtimeName], new ExternTypeMetadata(
                    RuntimeTypeIdentity.Named(runtimeName),
                    ExternTypeShape.Reference,
                    0,
                    Array.Empty<RuntimeTypeIdentity>(),
                    isAbiAvailable: false,
                    satisfiesDefaultConstructorConstraint: false,
                    @enum: null));
            }

            var memberGroups = new Dictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>>();
            var operatorGroups = new Dictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>>();
            foreach (var record in data.members ?? new List<UdonApiMemberRecord>())
                AddMember(record, types, memberGroups, operatorGroups);
            AddLegacySyntheticOperators(data, types, memberGroups, operatorGroups);

            foreach (var rejected in data.unexposedMembers ?? new List<UdonApiUnexposedMemberRecord>())
            {
                if (rejected == null || !types.TryGetValue(rejected.hostType ?? string.Empty, out var host) || string.IsNullOrWhiteSpace(rejected.name))
                    throw new InvalidDataException("The catalog contains an unexposed member with an unknown host type.");
                var memberName = GetCompilerMemberName(rejected.name, rejected.kind);
                GetOrCreateMemberGroup(memberGroups, host, memberName).AddRejectedCandidate(
                    new ExternCandidate($"{rejected.hostType}.{memberName}", rejected.externSignature ?? string.Empty, false, "The member is not exposed to Udon."));
            }

            var arrays = new Dictionary<RuntimeTypeIdentity, ArrayIntrinsicSymbols>();
            foreach (var record in data.capabilities?.arrays ?? new List<ArrayCapabilityRecord>())
            {
                var identity = ResolveIdentity(record?.arrayType, types, null, "array capability");
                if (identity.Kind != RuntimeTypeIdentityKind.Array || !types.ContainsKey(record.indexType?.runtimeName ?? string.Empty) ||
                    string.IsNullOrWhiteSpace(record.constructorSignature) || string.IsNullOrWhiteSpace(record.getterSignature) ||
                    string.IsNullOrWhiteSpace(record.setterSignature) || string.IsNullOrWhiteSpace(record.lengthSignature) ||
                    arrays.ContainsKey(identity))
                    throw new InvalidDataException("The catalog contains an invalid or duplicate array capability.");
                arrays.Add(identity, new ArrayIntrinsicSymbols(record.constructorSignature, record.getterSignature, record.setterSignature, record.lengthSignature, ResolveType(record.indexType, types, null, "array capability")));
            }
            AddLegacyArrayCapabilities(data, types, arrays);

            var immutableMemberGroups = new Dictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>();
            foreach (var pair in memberGroups)
                immutableMemberGroups.Add(pair.Key, new Dictionary<string, MethodGroupSymbol>(pair.Value, StringComparer.Ordinal));
            var immutableOperatorGroups = new Dictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>();
            foreach (var pair in operatorGroups)
                immutableOperatorGroups.Add(pair.Key, new Dictionary<string, MethodGroupSymbol>(pair.Value, StringComparer.Ordinal));
            return new ExternCatalog(global, types, sourceTypes, metadata, immutableMemberGroups, immutableOperatorGroups, arrays);
        }

        private static void AddLegacySyntheticOperators(
            UdonApiCatalogData data,
            IReadOnlyDictionary<string, TypeSymbol> types,
            IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> memberGroups,
            IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> operatorGroups)
        {
            const string booleanLogicalNotSignature =
                "SystemBoolean.__op_UnaryNegation__SystemBoolean__SystemBoolean";

            foreach (var member in data.members ?? new List<UdonApiMemberRecord>())
            {
                if (string.Equals(
                        member?.externSignature,
                        booleanLogicalNotSignature,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            var unmatched = data.unmatchedUdonSignatures ?? new List<string>();
            if (!unmatched.Contains(booleanLogicalNotSignature) ||
                !types.ContainsKey("System.Boolean"))
            {
                return;
            }

            AddMember(
                new UdonApiMemberRecord
                {
                    hostType = new ExternTypeRef
                    {
                        kind = "Named",
                        runtimeName = "System.Boolean"
                    },
                    clrDeclaringType = new ExternTypeRef
                    {
                        kind = "Named",
                        runtimeName = "System.Boolean"
                    },
                    name = "op_LogicalNot",
                    kind = "Operator",
                    origin = "SyntheticUdon",
                    isStatic = true,
                    clrSignature = "System.Boolean.op_LogicalNot(System.Boolean)",
                    externSignature = booleanLogicalNotSignature,
                    abiParameters = new List<ExternParameterRecord>
                    {
                        new()
                        {
                            name = "arg0",
                            type = new ExternTypeRef
                            {
                                kind = "Named",
                                runtimeName = "System.Boolean"
                            },
                            passingMode = "Normal"
                        }
                    },
                    abiReturnType = new ExternTypeRef
                    {
                        kind = "Named",
                        runtimeName = "System.Boolean"
                    }
                },
                types,
                memberGroups,
                operatorGroups);
        }

        private static void AddLegacyArrayCapabilities(
            UdonApiCatalogData data,
            IReadOnlyDictionary<string, TypeSymbol> types,
            IDictionary<RuntimeTypeIdentity, ArrayIntrinsicSymbols> arrays)
        {
            if (!types.TryGetValue("System.Int32", out var indexType))
                return;

            var exposedSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var signature in data.unmatchedUdonSignatures ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(signature))
                    exposedSignatures.Add(signature);
            }
            foreach (var member in data.members ?? new List<UdonApiMemberRecord>())
            {
                if (!string.IsNullOrWhiteSpace(member?.externSignature))
                    exposedSignatures.Add(member.externSignature);
            }

            foreach (var pair in types)
            {
                var element = pair.Value;
                if (element == TypeSymbol.Unit || element.ContainsGenericParameters ||
                    element.RuntimeTypeIdentity == null)
                    continue;

                var arrayIdentity = RuntimeTypeIdentity.Array(element.RuntimeTypeIdentity);
                if (arrays.ContainsKey(arrayIdentity))
                    continue;

                var elementName = ToUdonExternTypeName(pair.Key);
                var arrayName = elementName + "Array";
                var constructor = $"{arrayName}.__ctor__SystemInt32__{arrayName}";
                var getter = $"{arrayName}.__Get__SystemInt32__{elementName}";
                var setter = $"{arrayName}.__Set__SystemInt32_{elementName}__SystemVoid";
                var length = $"{arrayName}.__get_Length__SystemInt32";
                if (!exposedSignatures.Contains(constructor) ||
                    !exposedSignatures.Contains(getter) ||
                    !exposedSignatures.Contains(setter) ||
                    !exposedSignatures.Contains(length))
                    continue;

                arrays.Add(
                    arrayIdentity,
                    new ArrayIntrinsicSymbols(
                        constructor, getter, setter, length, indexType));
            }
        }

        private static string ToUdonExternTypeName(string runtimeName)
        {
            return runtimeName
                .Replace(".", string.Empty)
                .Replace("+", string.Empty)
                .Replace("[]", "Array");
        }

        private static void AddMember(UdonApiMemberRecord record, IReadOnlyDictionary<string, TypeSymbol> types, IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> memberGroups, IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> operatorGroups)
        {
            if (record == null || record.hostType == null || !types.TryGetValue(record.hostType.runtimeName ?? string.Empty, out var host) || string.IsNullOrWhiteSpace(record.name) || string.IsNullOrWhiteSpace(record.externSignature))
                throw new InvalidDataException("The catalog contains an invalid member.");
            var memberKind = ParseMemberKind(record.kind);
            var memberName = GetCompilerMemberName(record.name, record.kind);
            var isStatic = memberKind == ExternMemberKind.Constructor || record.isStatic;
            var genericParameters = new TypeSymbol[(record.genericParameters ?? new List<UdonApiGenericParameterRecord>()).Count];
            for (var index = 0; index < genericParameters.Length; index++)
                genericParameters[index] = TypeSymbol.CreateGenericParameter(record.genericParameters[index].name ?? $"T{index}", record, index, $"{host.RuntimeQualifiedName}.{memberName}");
            var abiParameters = new List<ExternParameterSymbol>();
            var logicalParameters = new List<ParameterSymbol>();
            if (!isStatic) logicalParameters.Add(new ParameterSymbol("self", host, 0));
            foreach (var parameter in record.abiParameters ?? new List<ExternParameterRecord>())
            {
                var mode = ParsePassingMode(parameter?.passingMode);
                var type = ResolveType(parameter?.type, types, genericParameters, $"{host.RuntimeQualifiedName}.{memberName}");
                var ordinal = -1;
                if (mode != ExternParameterPassingMode.Out && mode != ExternParameterPassingMode.GenericTypeArgument)
                {
                    ordinal = logicalParameters.Count;
                    logicalParameters.Add(new ParameterSymbol(parameter.name ?? $"arg{ordinal}", type, ordinal));
                }
                abiParameters.Add(new ExternParameterSymbol(parameter.name ?? $"arg{abiParameters.Count}", type, mode, ordinal));
            }
            var abiReturnType = ResolveType(record.abiReturnType, types, genericParameters, $"{host.RuntimeQualifiedName}.{memberName}");
            var constraints = new List<ExternGenericParameterConstraint>();
            for (var index = 0; index < genericParameters.Length; index++)
            {
                var source = record.genericParameters[index];
                var required = new List<TypeSymbol>();
                foreach (var typeConstraint in source.typeConstraints ?? new List<ExternTypeRef>())
                    required.Add(ResolveType(typeConstraint, types, genericParameters, $"{host.RuntimeQualifiedName}.{memberName}"));
                constraints.Add(new ExternGenericParameterConstraint(genericParameters[index], source.referenceTypeConstraint, source.nonNullableValueTypeConstraint, source.defaultConstructorConstraint, required));
            }
            var method = new ExternMethodSymbol(memberName, host, logicalParameters, BuildLogicalReturnType(abiReturnType, abiParameters), record.externSignature, isStatic, memberKind, abiParameters, abiReturnType, genericParameters, constraints);
            GetOrCreateMemberGroup(memberGroups, host, memberName).AddMethod(method);
            if (method.MemberKind != ExternMemberKind.Operator || logicalParameters.Count == 0) return;
            var operand = logicalParameters[0].Type;
            if (!operatorGroups.TryGetValue(operand, out var groups))
            {
                groups = new Dictionary<string, MethodGroupSymbol>(StringComparer.Ordinal);
                operatorGroups.Add(operand, groups);
            }
            if (!groups.TryGetValue(method.Name, out var group))
            {
                group = new MethodGroupSymbol(method.Name, operand);
                groups.Add(method.Name, group);
            }
            group.AddMethod(method);
        }

        private static MethodGroupSymbol GetOrCreateMemberGroup(IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> memberGroups, TypeSymbol host, string memberName)
        {
            if (!memberGroups.TryGetValue(host, out var groups))
            {
                groups = new Dictionary<string, MethodGroupSymbol>(StringComparer.Ordinal);
                memberGroups.Add(host, groups);
            }
            if (!groups.TryGetValue(memberName, out var group))
            {
                group = new MethodGroupSymbol(memberName, host);
                groups.Add(memberName, group);
            }
            return group;
        }

        private static TypeSymbol BuildLogicalReturnType(TypeSymbol abiReturnType, IReadOnlyList<ExternParameterSymbol> parameters)
        {
            var outputs = new List<TypeSymbol>();
            if (abiReturnType != TypeSymbol.Unit) outputs.Add(abiReturnType);
            foreach (var parameter in parameters)
                if (parameter.PassingMode == ExternParameterPassingMode.Ref || parameter.PassingMode == ExternParameterPassingMode.Out) outputs.Add(parameter.LogicalOutputType);
            return outputs.Count == 0 ? TypeSymbol.Unit : outputs.Count == 1 ? outputs[0] : TypeSymbol.Tuple(outputs);
        }

        private static TypeSymbol ResolveType(ExternTypeRef reference, IReadOnlyDictionary<string, TypeSymbol> types, IReadOnlyList<TypeSymbol> genericParameters, string owner)
        {
            if (reference == null) throw new InvalidDataException($"'{owner}' has an unknown type reference.");
            if (string.Equals(reference.kind, "GenericParameter", StringComparison.Ordinal))
            {
                if (genericParameters == null || reference.ordinal < 0 || reference.ordinal >= genericParameters.Count) throw new InvalidDataException($"'{owner}' has an invalid generic parameter ordinal.");
                return genericParameters[reference.ordinal];
            }
            if (string.Equals(reference.kind, "Array", StringComparison.Ordinal)) return TypeSymbol.Array(ResolveType(reference.element, types, genericParameters, owner));
            if (string.Equals(reference.kind, "ConstructedGeneric", StringComparison.Ordinal))
            {
                var definition = ResolveType(reference.definition, types, genericParameters, owner);
                var arguments = new List<TypeSymbol>();
                foreach (var argument in reference.arguments ?? new List<ExternTypeRef>()) arguments.Add(ResolveType(argument, types, genericParameters, owner));
                if (!definition.IsGenericDefinition || definition.GenericParameters.Count != arguments.Count) throw new InvalidDataException($"'{owner}' has an invalid constructed generic type.");
                return definition.Construct(arguments);
            }
            if (!string.Equals(reference.kind, "Named", StringComparison.Ordinal) || !types.TryGetValue(reference.runtimeName ?? string.Empty, out var type)) throw new InvalidDataException($"'{owner}' references unknown type '{reference.runtimeName}'.");
            return type;
        }

        private static RuntimeTypeIdentity ResolveIdentity(ExternTypeRef reference, IReadOnlyDictionary<string, TypeSymbol> types, IReadOnlyList<TypeSymbol> genericParameters, string owner) =>
            ResolveType(reference, types, genericParameters, owner).RuntimeTypeIdentity ?? throw new InvalidDataException($"'{owner}' has a non-concrete runtime type identity.");

        private static ExternTypeShape ParseShape(string value, string name) => value switch
        {
            "Void" => ExternTypeShape.Void, "Value" => ExternTypeShape.Value, "Reference" => ExternTypeShape.Reference, "Enum" => ExternTypeShape.Enum,
            _ => throw new InvalidDataException($"Type '{name}' has an invalid shape '{value}'.")
        };
        private static ExternMemberKind ParseMemberKind(string value) => value switch
        {
            "Getter" => ExternMemberKind.Getter, "Setter" => ExternMemberKind.Setter, "Constructor" => ExternMemberKind.Constructor, "Operator" => ExternMemberKind.Operator, "Method" => ExternMemberKind.Method,
            _ => throw new InvalidDataException($"The catalog contains an invalid member kind '{value}'.")
        };
        private static string GetCompilerMemberName(string catalogName, string catalogKind) =>
            string.Equals(catalogKind, "Constructor", StringComparison.Ordinal) ? "new" : catalogName;
        private static ExternParameterPassingMode ParsePassingMode(string value) => value switch
        {
            "Ref" => ExternParameterPassingMode.Ref, "Out" => ExternParameterPassingMode.Out, "In" => ExternParameterPassingMode.In, "GenericTypeArgument" => ExternParameterPassingMode.GenericTypeArgument, "Normal" => ExternParameterPassingMode.Normal,
            _ => throw new InvalidDataException($"The catalog contains an invalid parameter passing mode '{value}'.")
        };
        private static void AddToNamespace(NamespaceSymbol global, string runtimeName, TypeSymbol type)
        {
            var qualifiedName = GetQualifiedName(runtimeName);
            var lastDot = qualifiedName.LastIndexOf('.');
            var current = global;
            if (lastDot >= 0) foreach (var segment in qualifiedName[..lastDot].Split('.')) current = current.GetOrAddNamespace(segment);
            current.AddType(type);
        }
        private static string GetQualifiedName(string runtimeName)
        {
            // The CLR generic arity is part of the runtime identity.  Removing it
            // makes System.Action and System.Action`1 compare as the same symbol.
            return runtimeName.Replace('+', '.');
        }
        private static string GetSimpleName(string runtimeName)
        {
            var qualifiedName = GetQualifiedName(runtimeName);
            var dot = qualifiedName.LastIndexOf('.');
            return dot >= 0 ? qualifiedName[(dot + 1)..] : qualifiedName;
        }
        private static bool TryGetBuiltIn(string runtimeName, out TypeSymbol symbol)
        {
            symbol = runtimeName switch
            {
                "System.Void" => TypeSymbol.Unit, "System.String" => TypeSymbol.String, "System.Boolean" => TypeSymbol.Bool, "System.Char" => TypeSymbol.Char,
                "System.SByte" => TypeSymbol.I8, "System.Byte" => TypeSymbol.U8, "System.Int16" => TypeSymbol.I16, "System.UInt16" => TypeSymbol.U16,
                "System.Int32" => TypeSymbol.I32, "System.UInt32" => TypeSymbol.U32, "System.Int64" => TypeSymbol.I64, "System.UInt64" => TypeSymbol.U64,
                "System.Single" => TypeSymbol.F32, "System.Double" => TypeSymbol.F64, "System.Object" => TypeSymbol.Object, _ => null
            };
            return symbol != null;
        }
    }
}
