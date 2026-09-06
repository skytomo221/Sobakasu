using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public enum TypeKind
    {
        Error,
        Never,
        // Value 2 was the removed legacy void-like kind. Keep later serialized
        // values stable for existing heap-patch metadata.
        I8 = 3,
        U8,
        I16,
        U16,
        I32,
        U32,
        I64,
        U64,
        F32,
        F64,
        Char,
        String,
        Bool,
        // Value 16 was the removed source-level Null kind. Keep later serialized
        // values stable for existing heap-patch metadata.
        Array = 17,
        Named,
        ModulePseudo,
        NamespacePseudo,
        MethodGroupPseudo,
        Tuple
    }

    internal sealed class TypeSymbol : Symbol, IEquatable<TypeSymbol>
    {
        private static readonly Dictionary<TypeSymbol, TypeSymbol> ArrayTypes = new();
        private static readonly object ArrayTypesGate = new();
        private static readonly Dictionary<TypeArgumentListKey, TypeSymbol> TupleTypes = new();
        private static readonly object TupleTypesGate = new();
        public static readonly TypeSymbol Error =
            new(TypeKind.Error, "error", "error", false);
        public static readonly TypeSymbol Never =
            new(TypeKind.Never, "<never>", "<never>", false);
        public static readonly TypeSymbol Unit = Tuple(System.Array.Empty<TypeSymbol>());
        public static readonly TypeSymbol I8 =
            new(TypeKind.I8, "i8", "i8", false, runtimeQualifiedName: "System.SByte", isBuiltIn: true, runtimeClrType: typeof(sbyte));
        public static readonly TypeSymbol U8 =
            new(TypeKind.U8, "u8", "u8", false, runtimeQualifiedName: "System.Byte", isBuiltIn: true, runtimeClrType: typeof(byte));
        public static readonly TypeSymbol I16 =
            new(TypeKind.I16, "i16", "i16", false, runtimeQualifiedName: "System.Int16", isBuiltIn: true, runtimeClrType: typeof(short));
        public static readonly TypeSymbol U16 =
            new(TypeKind.U16, "u16", "u16", false, runtimeQualifiedName: "System.UInt16", isBuiltIn: true, runtimeClrType: typeof(ushort));
        public static readonly TypeSymbol I32 =
            new(TypeKind.I32, "i32", "i32", false, runtimeQualifiedName: "System.Int32", isBuiltIn: true, runtimeClrType: typeof(int));
        public static readonly TypeSymbol U32 =
            new(TypeKind.U32, "u32", "u32", false, runtimeQualifiedName: "System.UInt32", isBuiltIn: true, runtimeClrType: typeof(uint));
        public static readonly TypeSymbol I64 =
            new(TypeKind.I64, "i64", "i64", false, runtimeQualifiedName: "System.Int64", isBuiltIn: true, runtimeClrType: typeof(long));
        public static readonly TypeSymbol U64 =
            new(TypeKind.U64, "u64", "u64", false, runtimeQualifiedName: "System.UInt64", isBuiltIn: true, runtimeClrType: typeof(ulong));
        public static readonly TypeSymbol F32 =
            new(TypeKind.F32, "f32", "f32", false, runtimeQualifiedName: "System.Single", isBuiltIn: true, runtimeClrType: typeof(float));
        public static readonly TypeSymbol F64 =
            new(TypeKind.F64, "f64", "f64", false, runtimeQualifiedName: "System.Double", isBuiltIn: true, runtimeClrType: typeof(double));
        public static readonly TypeSymbol Char =
            new(TypeKind.Char, "char", "char", false, runtimeQualifiedName: "System.Char", isBuiltIn: true, runtimeClrType: typeof(char));
        public static readonly TypeSymbol String =
            new(TypeKind.String, "string", "string", true, runtimeQualifiedName: "System.String", isBuiltIn: true, runtimeClrType: typeof(string));
        public static readonly TypeSymbol Bool =
            new(TypeKind.Bool, "bool", "bool", false, runtimeQualifiedName: "System.Boolean", isBuiltIn: true, runtimeClrType: typeof(bool));
        public static readonly TypeSymbol Object =
            new(
                TypeKind.Named,
                "object",
                "System.Object",
                true,
                runtimeQualifiedName: "System.Object",
                isBuiltIn: true,
                runtimeClrType: typeof(object));
        public static readonly TypeSymbol NamespacePseudoType =
            new(TypeKind.NamespacePseudo, "<namespace>", "<namespace>", false);
        public static readonly TypeSymbol ModulePseudoType =
            new(TypeKind.ModulePseudo, "<module>", "<module>", false);
        public static readonly TypeSymbol MethodGroupPseudoType =
            new(TypeKind.MethodGroupPseudo, "<method-group>", "<method-group>", false);

        private readonly Dictionary<string, MethodGroupSymbol> _methodGroups =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _unsupportedImportMembers =
            new(StringComparer.Ordinal);
        private readonly List<AggregateFieldSymbol> _aggregateFields = new();
        private readonly List<EnumVariantSymbol> _enumVariants = new();
        private readonly Dictionary<string, AggregateFieldSymbol> _aggregateFieldsByName =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, EnumVariantSymbol> _enumVariantsByName =
            new(StringComparer.Ordinal);
        private readonly List<TypeSymbol> _genericParameters = new();
        private readonly Dictionary<TypeArgumentListKey, TypeSymbol> _constructedGenericTypes =
            new();
        private bool _aggregateMembersInitialized;
        private bool _constructedMembersInitialized;

        public override SymbolKind Kind => SymbolKind.Type;
        public TypeKind TypeKind { get; }
        public string QualifiedName { get; }
        public string RuntimeQualifiedName { get; }
        public Type RuntimeClrType { get; }
        public TypeSymbol ElementType { get; }
        public IReadOnlyList<TypeSymbol> TupleElementTypes { get; }
        public bool IsReferenceType { get; }
        public bool IsBuiltIn { get; }
        internal bool IsCanonicalExternPrimitive => TypeKind is TypeKind.Bool or
            TypeKind.Char or
            TypeKind.I8 or
            TypeKind.U8 or
            TypeKind.I16 or
            TypeKind.U16 or
            TypeKind.I32 or
            TypeKind.U32 or
            TypeKind.I64 or
            TypeKind.U64 or
            TypeKind.F32 or
            TypeKind.F64 or
            TypeKind.String;
        public bool IsExternalBinding { get; }
        public bool IsPublic { get; }
        public string DeclaringModule { get; }
        public UserAggregateKind? AggregateKind { get; }
        public bool IsAggregate => AggregateKind.HasValue;
        public bool UsesFlattenedAggregateStorage => IsAggregate && !IsExternalBinding;
        public bool IsGenericParameter { get; }
        public object GenericParameterOwner { get; }
        public int GenericParameterOrdinal { get; }
        public IReadOnlyList<TypeSymbol> GenericParameters => _genericParameters;
        public TypeSymbol GenericDefinition { get; }
        public IReadOnlyList<TypeSymbol> TypeArguments { get; }
        public bool IsGenericDefinition => GenericDefinition == null &&
            _genericParameters.Count > 0;
        public bool IsConstructedGenericType => GenericDefinition != null;
        public bool ContainsGenericParameters
        {
            get
            {
                if (IsGenericParameter || IsGenericDefinition)
                    return true;
                if (TypeKind == TypeKind.Array)
                    return ElementType?.ContainsGenericParameters == true;
                if (TypeKind == TypeKind.Tuple)
                {
                    foreach (var element in TupleElementTypes)
                    {
                        if (element.ContainsGenericParameters)
                            return true;
                    }
                    return false;
                }
                if (!IsConstructedGenericType)
                    return false;
                foreach (var argument in TypeArguments)
                {
                    if (argument.ContainsGenericParameters)
                        return true;
                }
                return false;
            }
        }
        public IReadOnlyCollection<TypeSymbol> ConstructedGenericTypes =>
            _constructedGenericTypes.Values;
        public IReadOnlyList<AggregateFieldSymbol> AggregateFields => _aggregateFields;
        public IReadOnlyList<EnumVariantSymbol> EnumVariants => _enumVariants;
        public string DeclarationIdentity => string.IsNullOrEmpty(DeclaringModule)
            ? Name
            : $"{DeclaringModule}.{Name}";
        public string CanonicalPublicPath { get; private set; }

        private TypeSymbol(
            TypeKind typeKind,
            string name,
            string qualifiedName,
            bool isReferenceType,
            TypeSymbol elementType = null,
            string runtimeQualifiedName = null,
            bool isBuiltIn = false,
            bool isExternalBinding = false,
            bool isPublic = true,
            string declaringModule = null,
            UserAggregateKind? aggregateKind = null,
            bool isGenericParameter = false,
            object genericParameterOwner = null,
            int genericParameterOrdinal = -1,
            TypeSymbol genericDefinition = null,
            IReadOnlyList<TypeSymbol> typeArguments = null,
            IReadOnlyList<TypeSymbol> tupleElementTypes = null,
            Type runtimeClrType = null)
            : base(name)
        {
            TypeKind = typeKind;
            QualifiedName = qualifiedName ?? name;
            IsReferenceType = isReferenceType;
            ElementType = elementType;
            RuntimeQualifiedName = runtimeQualifiedName ?? qualifiedName ?? name;
            RuntimeClrType = runtimeClrType;
            IsBuiltIn = isBuiltIn;
            IsExternalBinding = isExternalBinding;
            IsPublic = isPublic;
            DeclaringModule = declaringModule ?? string.Empty;
            AggregateKind = aggregateKind;
            IsGenericParameter = isGenericParameter;
            GenericParameterOwner = genericParameterOwner;
            GenericParameterOrdinal = genericParameterOrdinal;
            GenericDefinition = genericDefinition;
            TypeArguments = typeArguments ?? System.Array.Empty<TypeSymbol>();
            TupleElementTypes = tupleElementTypes ?? System.Array.Empty<TypeSymbol>();
        }

        public static TypeSymbol CreateNamed(
            string name,
            string qualifiedName,
            bool isReferenceType = true,
            Type runtimeClrType = null,
            bool isExternalBinding = false)
        {
            return new TypeSymbol(
                TypeKind.Named,
                name,
                qualifiedName,
                isReferenceType,
                runtimeQualifiedName: qualifiedName,
                isExternalBinding: isExternalBinding,
                runtimeClrType: runtimeClrType);
        }

        public static TypeSymbol CreateExternalBinding(
            string name,
            string qualifiedName,
            TypeSymbol runtimeType,
            bool isPublic,
            string declaringModule)
        {
            if (runtimeType == null)
                throw new ArgumentNullException(nameof(runtimeType));

            return new TypeSymbol(
                TypeKind.Named,
                name,
                qualifiedName,
                runtimeType.IsReferenceType,
                runtimeQualifiedName: runtimeType.RuntimeQualifiedName,
                isExternalBinding: true,
                isPublic: isPublic,
                declaringModule: declaringModule,
                runtimeClrType: runtimeType.RuntimeClrType);
        }

        public static TypeSymbol CreateAggregate(
            string name,
            string qualifiedName,
            UserAggregateKind aggregateKind,
            bool isPublic,
            string declaringModule)
        {
            return new TypeSymbol(
                TypeKind.Named,
                name,
                qualifiedName,
                false,
                runtimeQualifiedName: string.Empty,
                isPublic: isPublic,
                declaringModule: declaringModule,
                aggregateKind: aggregateKind);
        }

        public static TypeSymbol CreateExternalAggregateBinding(
            string name,
            string qualifiedName,
            TypeSymbol runtimeType,
            UserAggregateKind aggregateKind,
            bool isPublic,
            string declaringModule)
        {
            if (runtimeType == null)
                throw new ArgumentNullException(nameof(runtimeType));
            return new TypeSymbol(
                TypeKind.Named,
                name,
                qualifiedName,
                runtimeType.IsReferenceType,
                runtimeQualifiedName: runtimeType.RuntimeQualifiedName,
                isExternalBinding: true,
                isPublic: isPublic,
                declaringModule: declaringModule,
                aggregateKind: aggregateKind,
                runtimeClrType: runtimeType.RuntimeClrType);
        }

        public static TypeSymbol CreateGenericParameter(
            string name,
            object declarationIdentity,
            int ordinal,
            string ownerDisplayName,
            Type runtimeClrType = null)
        {
            if (declarationIdentity == null)
                throw new ArgumentNullException(nameof(declarationIdentity));

            return new TypeSymbol(
                TypeKind.Named,
                name,
                $"{ownerDisplayName}.{name}#{ordinal}",
                false,
                runtimeQualifiedName: string.Empty,
                isPublic: false,
                isGenericParameter: true,
                genericParameterOwner: declarationIdentity,
                genericParameterOrdinal: ordinal,
                runtimeClrType: runtimeClrType);
        }

        public void SetGenericParameters(IReadOnlyList<TypeSymbol> parameters)
        {
            if (GenericDefinition != null || IsGenericParameter)
                throw new InvalidOperationException("Only a generic definition can declare parameters.");

            _genericParameters.Clear();
            if (parameters == null)
                return;
            foreach (var parameter in parameters)
                _genericParameters.Add(parameter);
        }

        public TypeSymbol Construct(IReadOnlyList<TypeSymbol> typeArguments)
        {
            if (!IsGenericDefinition)
                throw new InvalidOperationException($"Type '{Name}' is not a generic definition.");
            if (typeArguments == null)
                throw new ArgumentNullException(nameof(typeArguments));
            if (typeArguments.Count != _genericParameters.Count)
                throw new ArgumentException("Generic arity does not match.", nameof(typeArguments));

            var copiedArguments = new TypeSymbol[typeArguments.Count];
            for (var index = 0; index < copiedArguments.Length; index++)
                copiedArguments[index] = typeArguments[index];
            var key = new TypeArgumentListKey(copiedArguments);
            if (_constructedGenericTypes.TryGetValue(key, out var existing))
                return existing;

            var sourceName = $"{Name}<{string.Join(", ", GetTypeNames(copiedArguments))}>";
            var qualifiedName = $"{QualifiedName}<{string.Join(", ", GetQualifiedTypeNames(copiedArguments))}>";
            Type constructedRuntimeType = null;
            if (RuntimeClrType?.IsGenericTypeDefinition == true)
            {
                var runtimeArguments = new Type[copiedArguments.Length];
                var canConstructRuntimeType = true;
                for (var index = 0; index < copiedArguments.Length; index++)
                {
                    runtimeArguments[index] = copiedArguments[index].RuntimeClrType;
                    canConstructRuntimeType &= runtimeArguments[index] != null;
                }
                if (canConstructRuntimeType)
                    constructedRuntimeType = RuntimeClrType.MakeGenericType(runtimeArguments);
            }
            var constructed = new TypeSymbol(
                TypeKind.Named,
                sourceName,
                qualifiedName,
                IsReferenceType,
                runtimeQualifiedName: constructedRuntimeType?.FullName ?? string.Empty,
                isExternalBinding: IsExternalBinding,
                isPublic: IsPublic,
                declaringModule: DeclaringModule,
                aggregateKind: AggregateKind,
                genericDefinition: this,
                typeArguments: copiedArguments,
                runtimeClrType: constructedRuntimeType);
            _constructedGenericTypes.Add(key, constructed);
            constructed.InitializeConstructedMembers();
            return constructed;
        }

        public static TypeSymbol Substitute(
            TypeSymbol type,
            IReadOnlyDictionary<TypeSymbol, TypeSymbol> substitutions)
        {
            if (type == null || substitutions == null || substitutions.Count == 0)
                return type;
            if (type.IsGenericParameter && substitutions.TryGetValue(type, out var replacement))
                return replacement;
            if (type.TypeKind == TypeKind.Array)
            {
                var element = Substitute(type.ElementType, substitutions);
                return ReferenceEquals(element, type.ElementType) ? type : Array(element);
            }
            if (type.TypeKind == TypeKind.Tuple)
            {
                var tupleChanged = false;
                var elements = new TypeSymbol[type.TupleElementTypes.Count];
                for (var index = 0; index < elements.Length; index++)
                {
                    elements[index] = Substitute(type.TupleElementTypes[index], substitutions);
                    tupleChanged |= !ReferenceEquals(elements[index], type.TupleElementTypes[index]);
                }
                return tupleChanged ? Tuple(elements) : type;
            }
            if (!type.IsConstructedGenericType)
                return type;

            var changed = false;
            var arguments = new TypeSymbol[type.TypeArguments.Count];
            for (var index = 0; index < arguments.Length; index++)
            {
                arguments[index] = Substitute(type.TypeArguments[index], substitutions);
                changed |= !ReferenceEquals(arguments[index], type.TypeArguments[index]);
            }
            return changed ? type.GenericDefinition.Construct(arguments) : type;
        }

        public void SetAggregateFields(IReadOnlyList<AggregateFieldSymbol> fields)
        {
            if (!IsAggregate ||
                AggregateKind != UserAggregateKind.Struct &&
                AggregateKind != UserAggregateKind.Tuple)
            {
                throw new InvalidOperationException("Only struct and tuple types have direct fields.");
            }

            _aggregateFields.Clear();
            _aggregateFieldsByName.Clear();
            foreach (var field in fields)
            {
                _aggregateFields.Add(field);
                _aggregateFieldsByName[field.Name] = field;
            }
            _aggregateMembersInitialized = true;
            RefreshConstructedMembers();
        }

        public void SetEnumVariants(IReadOnlyList<EnumVariantSymbol> variants)
        {
            if (!IsAggregate || AggregateKind != UserAggregateKind.Enum)
                throw new InvalidOperationException("Only enum types have variants.");

            _enumVariants.Clear();
            _enumVariantsByName.Clear();
            foreach (var variant in variants)
            {
                _enumVariants.Add(variant);
                _enumVariantsByName[variant.Name] = variant;
            }
            _aggregateMembersInitialized = true;
            RefreshConstructedMembers();
        }

        public bool TryGetAggregateField(string name, out AggregateFieldSymbol field)
        {
            return _aggregateFieldsByName.TryGetValue(name, out field);
        }

        public bool TryGetEnumVariant(string name, out EnumVariantSymbol variant)
        {
            return _enumVariantsByName.TryGetValue(name, out variant);
        }

        public static TypeSymbol Array(TypeSymbol elementType)
        {
            if (elementType == null)
                throw new ArgumentNullException(nameof(elementType));

            lock (ArrayTypesGate)
            {
                if (ArrayTypes.TryGetValue(elementType, out var existing))
                    return existing;

                var arrayType = new TypeSymbol(
                    TypeKind.Array,
                    $"[{elementType.Name}]",
                    $"[{elementType.QualifiedName}]",
                    true,
                    elementType,
                    runtimeQualifiedName: elementType.RuntimeClrType?.MakeArrayType().FullName ??
                        $"{elementType.RuntimeQualifiedName}[]",
                    runtimeClrType: elementType.RuntimeClrType?.MakeArrayType());
                ArrayTypes.Add(elementType, arrayType);
                return arrayType;
            }
        }

        public static TypeSymbol Tuple(IReadOnlyList<TypeSymbol> elementTypes)
        {
            if (elementTypes == null)
                throw new ArgumentNullException(nameof(elementTypes));

            var copiedElements = new TypeSymbol[elementTypes.Count];
            for (var index = 0; index < copiedElements.Length; index++)
            {
                copiedElements[index] = elementTypes[index] ??
                    throw new ArgumentException("Tuple element types cannot be null.", nameof(elementTypes));
            }

            var key = new TypeArgumentListKey(copiedElements);
            lock (TupleTypesGate)
            {
                if (TupleTypes.TryGetValue(key, out var existing))
                    return existing;

                var names = new string[copiedElements.Length];
                var qualifiedNames = new string[copiedElements.Length];
                for (var index = 0; index < copiedElements.Length; index++)
                {
                    names[index] = copiedElements[index].Name;
                    qualifiedNames[index] = copiedElements[index].QualifiedName;
                }
                var name = FormatTupleTypeName(names);
                var qualifiedName = FormatTupleTypeName(qualifiedNames);
                var tuple = new TypeSymbol(
                    TypeKind.Tuple,
                    name,
                    qualifiedName,
                    false,
                    runtimeQualifiedName: copiedElements.Length == 0 ? "System.Void" : string.Empty,
                    isBuiltIn: copiedElements.Length == 0,
                    aggregateKind: UserAggregateKind.Tuple,
                    tupleElementTypes: copiedElements);
                TupleTypes.Add(key, tuple);

                var fields = new AggregateFieldSymbol[copiedElements.Length];
                for (var index = 0; index < fields.Length; index++)
                {
                    fields[index] = new AggregateFieldSymbol(
                        index.ToString(),
                        tuple,
                        copiedElements[index],
                        index,
                        default);
                }
                tuple.SetAggregateFields(fields);
                return tuple;
            }
        }

        private static string FormatTupleTypeName(IReadOnlyList<string> elementNames)
        {
            if (elementNames.Count == 0)
                return "()";
            if (elementNames.Count == 1)
                return $"({elementNames[0]},)";
            return $"({string.Join(", ", elementNames)})";
        }

        public void AddMethod(MethodSymbol method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (!ReferenceEquals(method.ContainingType, this))
            {
                throw new InvalidOperationException(
                    "Method must belong to the containing type it is added to.");
            }

            GetOrCreateMethodGroup(method.Name).AddMethod(method);
        }

        private void RefreshConstructedMembers()
        {
            if (!IsGenericDefinition || _constructedGenericTypes.Count == 0)
                return;

            var constructedTypes = new List<TypeSymbol>(_constructedGenericTypes.Values);
            foreach (var constructed in constructedTypes)
                constructed.InitializeConstructedMembers();
        }

        private void InitializeConstructedMembers()
        {
            if (!IsConstructedGenericType ||
                _constructedMembersInitialized ||
                !GenericDefinition._aggregateMembersInitialized)
            {
                return;
            }

            var substitutions = new Dictionary<TypeSymbol, TypeSymbol>();
            for (var index = 0; index < GenericDefinition.GenericParameters.Count; index++)
            {
                substitutions.Add(
                    GenericDefinition.GenericParameters[index],
                    TypeArguments[index]);
            }

            if (AggregateKind == UserAggregateKind.Struct)
            {
                var fields = new List<AggregateFieldSymbol>();
                foreach (var field in GenericDefinition.AggregateFields)
                {
                    fields.Add(new AggregateFieldSymbol(
                        field.Name,
                        this,
                        Substitute(field.Type, substitutions),
                        field.Ordinal,
                        field.DeclarationSpan));
                }
                SetConstructedFields(fields);
            }
            else if (AggregateKind == UserAggregateKind.Enum)
            {
                var variants = new List<EnumVariantSymbol>();
                foreach (var variant in GenericDefinition.EnumVariants)
                {
                    var fields = new List<AggregateFieldSymbol>();
                    foreach (var field in variant.Fields)
                    {
                        fields.Add(new AggregateFieldSymbol(
                            field.Name,
                            this,
                            Substitute(field.Type, substitutions),
                            field.Ordinal,
                            field.DeclarationSpan));
                    }
                    variants.Add(new EnumVariantSymbol(
                        variant.Name,
                        this,
                        variant.VariantKind,
                        variant.Tag,
                        fields,
                        variant.DeclarationSpan));
                }
                SetConstructedVariants(variants);
            }

            _constructedMembersInitialized = true;
        }

        private void SetConstructedFields(IReadOnlyList<AggregateFieldSymbol> fields)
        {
            _aggregateFields.Clear();
            _aggregateFieldsByName.Clear();
            foreach (var field in fields)
            {
                _aggregateFields.Add(field);
                _aggregateFieldsByName[field.Name] = field;
            }
        }

        private void SetConstructedVariants(IReadOnlyList<EnumVariantSymbol> variants)
        {
            _enumVariants.Clear();
            _enumVariantsByName.Clear();
            foreach (var variant in variants)
            {
                _enumVariants.Add(variant);
                _enumVariantsByName[variant.Name] = variant;
            }
        }

        private static IEnumerable<string> GetTypeNames(IReadOnlyList<TypeSymbol> types)
        {
            foreach (var type in types)
                yield return type.Name;
        }

        private static IEnumerable<string> GetQualifiedTypeNames(IReadOnlyList<TypeSymbol> types)
        {
            foreach (var type in types)
                yield return type.QualifiedName;
        }

        public void RegisterPublicPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            if (string.IsNullOrEmpty(CanonicalPublicPath) ||
                path.Split('.').Length < CanonicalPublicPath.Split('.').Length ||
                path.Split('.').Length == CanonicalPublicPath.Split('.').Length &&
                string.CompareOrdinal(path, CanonicalPublicPath) < 0)
            {
                CanonicalPublicPath = path;
            }
        }

        public void AddRejectedCandidate(string methodName, ExternCandidate candidate)
        {
            if (string.IsNullOrWhiteSpace(methodName))
                throw new ArgumentException("Method group name is required.", nameof(methodName));

            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));

            GetOrCreateMethodGroup(methodName).AddRejectedCandidate(candidate);
        }

        public void AddUnsupportedImportMember(string memberName, string reason)
        {
            if (string.IsNullOrWhiteSpace(memberName))
                throw new ArgumentException("Member name is required.", nameof(memberName));

            if (_unsupportedImportMembers.ContainsKey(memberName))
                return;

            _unsupportedImportMembers.Add(memberName, reason ?? string.Empty);
        }

        public MethodGroupSymbol GetMethodGroup(string name)
        {
            if (_methodGroups.TryGetValue(name, out var methodGroup))
                return methodGroup;

            return null;
        }

        public bool TryGetUnsupportedImportMemberReason(string memberName, out string reason)
        {
            return _unsupportedImportMembers.TryGetValue(memberName, out reason);
        }

        public bool Equals(TypeSymbol other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null || TypeKind != other.TypeKind)
                return false;

            if (IsGenericParameter || other.IsGenericParameter)
                return false;

            if (IsAggregate || other.IsAggregate)
            {
                if (TypeKind != TypeKind.Tuple)
                    return false;
            }

            if (TypeKind == TypeKind.Tuple)
            {
                if (TupleElementTypes.Count != other.TupleElementTypes.Count)
                    return false;
                for (var index = 0; index < TupleElementTypes.Count; index++)
                {
                    if (TupleElementTypes[index] != other.TupleElementTypes[index])
                        return false;
                }
                return true;
            }

            if (TypeKind == TypeKind.Array)
                return Equals(ElementType, other.ElementType);

            if (TypeKind == TypeKind.Named)
            {
                return string.Equals(
                    QualifiedName,
                    other.QualifiedName,
                    StringComparison.Ordinal);
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is TypeSymbol other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)TypeKind * 397;
                hash = (hash * 397) ^ (QualifiedName?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (ElementType?.GetHashCode() ?? 0);
                foreach (var element in TupleElementTypes)
                    hash = (hash * 397) ^ element.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(TypeSymbol left, TypeSymbol right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TypeSymbol left, TypeSymbol right)
        {
            return !Equals(left, right);
        }

        private MethodGroupSymbol GetOrCreateMethodGroup(string name)
        {
            if (_methodGroups.TryGetValue(name, out var methodGroup))
                return methodGroup;

            methodGroup = new MethodGroupSymbol(name, this);
            _methodGroups.Add(name, methodGroup);
            return methodGroup;
        }

        private sealed class TypeArgumentListKey : IEquatable<TypeArgumentListKey>
        {
            private readonly IReadOnlyList<TypeSymbol> _arguments;

            public TypeArgumentListKey(IReadOnlyList<TypeSymbol> arguments)
            {
                _arguments = arguments;
            }

            public bool Equals(TypeArgumentListKey other)
            {
                if (ReferenceEquals(this, other))
                    return true;
                if (other == null || _arguments.Count != other._arguments.Count)
                    return false;
                for (var index = 0; index < _arguments.Count; index++)
                {
                    if (_arguments[index] != other._arguments[index])
                        return false;
                }
                return true;
            }

            public override bool Equals(object obj)
            {
                return obj is TypeArgumentListKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    foreach (var argument in _arguments)
                        hash = hash * 31 + (argument?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }
    }

    internal enum UserAggregateKind
    {
        Struct,
        Enum,
        Tuple
    }
}
