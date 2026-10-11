using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    internal enum UdonBindingSourceTypeKind
    {
        Class,
        Interface,
        Struct,
        Enum
    }

    internal enum UdonBindingSourceMemberKind
    {
        Constructor,
        StaticMethod,
        InstanceMethod,
        PropertyGetter,
        PropertySetter,
        FieldGetter,
        FieldSetter,
        Event,
        Operator
    }

    /// <summary>
    /// Generator-facing view of one physical CLR type persisted in Catalog v2.
    /// RuntimeName is the canonical CLR identity and therefore preserves '+' for
    /// nested CLR types. QualifiedName is the Sobakasu-source-oriented dotted view.
    /// </summary>
    internal sealed class UdonBindingSourceType
    {
        private readonly List<UdonBindingSourceMember> _members = new();

        public UdonApiTypeRecord Record { get; }
        public UdonBindingSourceTypeKind Kind { get; }
        public string ClrNamespace { get; }
        public IReadOnlyList<string> Path { get; }
        public string RuntimeName { get; }
        public string QualifiedName => RuntimeName.Replace('+', '.');
        public string WrapperName { get; }
        public bool IsExposed { get; }
        public bool IsStaticApiContainer =>
            Record is UdonApiClassRecord cls && cls.@abstract && cls.@sealed;
        public bool IsValueType =>
            Kind == UdonBindingSourceTypeKind.Struct ||
            Kind == UdonBindingSourceTypeKind.Enum;
        public bool IsEnum => Kind == UdonBindingSourceTypeKind.Enum;
        public IReadOnlyList<UdonApiGenericParameterRecord> GenericParameters =>
            Record.genericParameters ?? Array.Empty<UdonApiGenericParameterRecord>();
        public IReadOnlyList<UdonBindingSourceMember> Members => _members;

        public UdonBindingSourceType(
            UdonApiTypeRecord record,
            UdonBindingSourceTypeKind kind,
            string clrNamespace,
            bool isExposed)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Kind = kind;
            ClrNamespace = clrNamespace ?? throw new ArgumentNullException(nameof(clrNamespace));
            Path = record.path ?? throw new InvalidDataException(
                "The Udon API catalog contains a type without a CLR path.");
            if (Path.Count == 0 || Path.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "The Udon API catalog contains an invalid CLR type path.");
            if (Path.Any(segment => segment.Contains(".") || segment.Contains("+")))
                throw new InvalidDataException(
                    "Catalog type path segments must not contain '.' or '+'.");

            RuntimeName = BuildRuntimeName(ClrNamespace, Path);
            WrapperName = RemoveMetadataArity(Path[Path.Count - 1]);
            IsExposed = isExposed;
        }

        public void AddMember(UdonBindingSourceMember member)
        {
            _members.Add(member ?? throw new ArgumentNullException(nameof(member)));
        }

        public void SortMembers()
        {
            _members.Sort((left, right) =>
                string.CompareOrdinal(left.GetSortKey(), right.GetSortKey()));
        }

        internal static string BuildRuntimeName(
            string clrNamespace,
            IReadOnlyList<string> path)
        {
            if (path == null || path.Count == 0)
                throw new InvalidDataException("A CLR type path is required.");

            var root = string.IsNullOrEmpty(clrNamespace)
                ? path[0]
                : clrNamespace + "." + path[0];
            if (path.Count == 1)
                return root;
            return root + "+" + string.Join("+", path.Skip(1));
        }

        internal static string RemoveMetadataArity(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            var tick = value.IndexOf('`');
            return tick < 0 ? value : value.Substring(0, tick);
        }
    }

    /// <summary>
    /// Generator-facing physical member metadata. It intentionally keeps Catalog
    /// TypeRefs instead of compiler TypeSymbols so generator policy stays separate
    /// from compiler ABI lowering.
    /// </summary>
    internal sealed class UdonBindingSourceMember
    {
        public UdonBindingSourceType HostType { get; }
        public UdonBindingSourceMemberKind Kind { get; }
        public string Name { get; }
        public bool IsStatic { get; }
        public IReadOnlyList<UdonApiGenericParameterRecord> GenericParameters { get; }
        public IReadOnlyList<UdonApiParameterRecord> Parameters { get; }
        public UdonApiTypeRef ReturnType { get; }
        public UdonApiTypeRef ValueType { get; }
        public string UdonSignature { get; }
        public string ClrMemberId { get; }
        public string DisplaySignature { get; }
        public bool IsOperator => Kind == UdonBindingSourceMemberKind.Operator;

        public UdonBindingSourceMember(
            UdonBindingSourceType hostType,
            UdonBindingSourceMemberKind kind,
            string name,
            bool isStatic,
            IReadOnlyList<UdonApiGenericParameterRecord> genericParameters,
            IReadOnlyList<UdonApiParameterRecord> parameters,
            UdonApiTypeRef returnType,
            UdonApiTypeRef valueType,
            string udonSignature)
        {
            HostType = hostType ?? throw new ArgumentNullException(nameof(hostType));
            Kind = kind;
            Name = name ?? string.Empty;
            IsStatic = isStatic;
            GenericParameters = genericParameters ?? Array.Empty<UdonApiGenericParameterRecord>();
            Parameters = parameters ?? Array.Empty<UdonApiParameterRecord>();
            ReturnType = returnType;
            ValueType = valueType;
            UdonSignature = udonSignature ?? string.Empty;
            ClrMemberId = BuildClrMemberId();
            DisplaySignature = BuildDisplaySignature();
        }

        public string GetSortKey()
        {
            return $"{(int)Kind:D2}|{Name}|{DisplaySignature}|{UdonSignature}";
        }

        private string BuildClrMemberId()
        {
            var owner = HostType.QualifiedName;
            switch (Kind)
            {
                case UdonBindingSourceMemberKind.PropertyGetter:
                case UdonBindingSourceMemberKind.PropertySetter:
                case UdonBindingSourceMemberKind.FieldGetter:
                case UdonBindingSourceMemberKind.FieldSetter:
                case UdonBindingSourceMemberKind.Event:
                    return owner + "." + Name;
                case UdonBindingSourceMemberKind.Constructor:
                    return owner + "(" + FormatParameterTypes() + ")";
                default:
                    return owner + "." + Name + "(" + FormatParameterTypes() + ")";
            }
        }

        private string BuildDisplaySignature()
        {
            var parameters = string.Join(", ", Parameters.Select(parameter =>
                (string.IsNullOrEmpty(parameter.passing) ? string.Empty : parameter.passing + " ") +
                FormatTypeRef(parameter.type, HostType.GenericParameters, GenericParameters)));
            var prefix = Kind == UdonBindingSourceMemberKind.Constructor
                ? HostType.WrapperName
                : Name;
            var returnType = ReturnType == null
                ? string.Empty
                : " -> " + FormatTypeRef(ReturnType, HostType.GenericParameters, GenericParameters);
            return prefix + "(" + parameters + ")" + returnType;
        }

        private string FormatParameterTypes()
        {
            return string.Join(",", Parameters.Select(parameter =>
            {
                var type = FormatTypeRef(
                    parameter.type,
                    HostType.GenericParameters,
                    GenericParameters);
                return parameter.passing == "ref" || parameter.passing == "out" || parameter.passing == "in"
                    ? type + "&"
                    : type;
            }));
        }

        internal static string FormatTypeRef(
            UdonApiTypeRef type,
            IReadOnlyList<UdonApiGenericParameterRecord> typeParameters,
            IReadOnlyList<UdonApiGenericParameterRecord> methodParameters)
        {
            if (type == null)
                return "<missing>";
            if (type.named != null)
                return type.named.Replace('+', '.');
            if (type.array != null)
            {
                var rank = type.rank ?? 1;
                return FormatTypeRef(type.array, typeParameters, methodParameters) +
                    "[" + new string(',', Math.Max(0, rank - 1)) + "]";
            }
            if (type.typeParameter.HasValue)
            {
                var ordinal = type.typeParameter.Value;
                return ordinal >= 0 && ordinal < (typeParameters?.Count ?? 0)
                    ? typeParameters[ordinal].name
                    : "!" + ordinal;
            }
            if (type.methodParameter.HasValue)
            {
                var ordinal = type.methodParameter.Value;
                return ordinal >= 0 && ordinal < (methodParameters?.Count ?? 0)
                    ? methodParameters[ordinal].name
                    : "!!" + ordinal;
            }
            if (type.generic != null)
            {
                var arguments = type.arguments ?? new List<UdonApiTypeRef>();
                return type.generic.Replace('+', '.') + "<" +
                    string.Join(",", arguments.Select(argument =>
                        FormatTypeRef(argument, typeParameters, methodParameters))) + ">";
            }
            return "<invalid>";
        }
    }

    /// <summary>
    /// Standard Library Generator projection of Catalog v2. All catalog declarations
    /// are indexed so TypeRefs can resolve through unexposed metadata, while Types
    /// contains only Udon-exposed declarations that are publication candidates.
    /// </summary>
    internal sealed class UdonBindingSourceModel
    {
        private readonly Dictionary<string, UdonBindingSourceType> _typesByRuntimeName =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _unexposedTypeNames =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _udonExposedSignatures =
            new(StringComparer.Ordinal);

        public IReadOnlyList<UdonBindingSourceType> Types { get; }
        public IReadOnlyDictionary<string, UdonBindingSourceType> TypesByRuntimeName =>
            _typesByRuntimeName;
        public ISet<string> UnexposedTypeNames => _unexposedTypeNames;
        public IReadOnlyCollection<string> UdonExposedSignatures =>
            _udonExposedSignatures;
        public IReadOnlyList<string> UnmodeledUdonSignatures { get; }

        private UdonBindingSourceModel(UdonApiCatalogData catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (catalog.formatVersion != 2)
            {
                throw new InvalidDataException(
                    $"Unsupported Udon API catalog format version '{catalog.formatVersion}'. " +
                    "Regenerate it manually from Window/Sobakasu/Build Udon API Catalog.");
            }
            if (catalog.namespaces == null || catalog.namespaces.Count == 0)
                throw new InvalidDataException(
                    "The Udon API catalog does not contain namespace records.");

            var generated = new List<UdonBindingSourceType>();
            var seenNamespaces = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ns in catalog.namespaces)
            {
                if (ns == null || ns.name == null || !seenNamespaces.Add(ns.name))
                    throw new InvalidDataException(
                        "The Udon API catalog contains an invalid or duplicate namespace.");
                AddTypeSet(ns.name, ns.exposed, true, generated);
                AddTypeSet(ns.name, ns.unexposed, false, generated);
            }

            foreach (var type in generated)
            {
                AddMembers(type, type.Record.exposed);
                type.SortMembers();
            }

            generated.Sort((left, right) =>
                string.CompareOrdinal(left.RuntimeName, right.RuntimeName));
            Types = generated;

            UnmodeledUdonSignatures =
                (catalog.unmodeledUdonSignatures ?? new List<string>())
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
            foreach (var signature in UnmodeledUdonSignatures)
            {
                if (string.IsNullOrEmpty(signature))
                    throw new InvalidDataException(
                        "Unmodeled Udon signatures must be non-empty.");
                _udonExposedSignatures.Add(signature);
            }
        }

        public static UdonBindingSourceModel Load(string catalogPath)
        {
            if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
            {
                throw new FileNotFoundException(
                    "Udon API catalog was not found. Generate it manually from: " +
                    "Window/Sobakasu/Build Udon API Catalog",
                    catalogPath);
            }

            using var reader = File.OpenText(catalogPath);
            return FromCatalog(UdonApiCatalogReader.Read(reader));
        }

        public static UdonBindingSourceModel FromCatalog(UdonApiCatalogData catalog)
        {
            return new UdonBindingSourceModel(catalog);
        }

        private void AddTypeSet(
            string clrNamespace,
            UdonApiTypeSet set,
            bool exposed,
            ICollection<UdonBindingSourceType> generated)
        {
            if (set == null)
                return;

            AddTypes(set.classes, UdonBindingSourceTypeKind.Class, clrNamespace, exposed, generated);
            AddTypes(set.interfaces, UdonBindingSourceTypeKind.Interface, clrNamespace, exposed, generated);
            AddTypes(set.structs, UdonBindingSourceTypeKind.Struct, clrNamespace, exposed, generated);
            AddTypes(set.enums, UdonBindingSourceTypeKind.Enum, clrNamespace, exposed, generated);
        }

        private void AddTypes<T>(
            IEnumerable<T> records,
            UdonBindingSourceTypeKind kind,
            string clrNamespace,
            bool exposed,
            ICollection<UdonBindingSourceType> generated)
            where T : UdonApiTypeRecord
        {
            if (records == null)
                return;
            foreach (var record in records)
            {
                if (record == null)
                    throw new InvalidDataException(
                        "The Udon API catalog contains a null type declaration.");
                var type = new UdonBindingSourceType(
                    record,
                    kind,
                    clrNamespace,
                    exposed);
                if (!_typesByRuntimeName.TryAdd(type.RuntimeName, type))
                {
                    throw new InvalidDataException(
                        $"The Udon API catalog contains duplicate type '{type.RuntimeName}'.");
                }
                if (exposed)
                    generated.Add(type);
                else
                    _unexposedTypeNames.Add(type.RuntimeName);
            }
        }

        private void AddMembers(
            UdonBindingSourceType host,
            UdonApiMemberExposure exposure)
        {
            if (exposure == null)
                return;

            foreach (var constructor in exposure.constructors ??
                     Enumerable.Empty<UdonApiConstructorRecord>())
            {
                AddMember(host, new UdonBindingSourceMember(
                    host,
                    UdonBindingSourceMemberKind.Constructor,
                    host.WrapperName,
                    false,
                    null,
                    constructor.parameters,
                    UdonApiTypeRef.Named(host.RuntimeName),
                    null,
                    constructor.udonSignature));
            }

            AddMemberSet(host, exposure.instance, false);
            AddMemberSet(host, exposure.@static, true);

            foreach (var op in exposure.operators ?? Enumerable.Empty<UdonApiOperatorRecord>())
            {
                AddMember(host, new UdonBindingSourceMember(
                    host,
                    UdonBindingSourceMemberKind.Operator,
                    op.name,
                    true,
                    null,
                    op.parameters,
                    op.returnType,
                    null,
                    op.udonSignature));
            }
        }

        private void AddMemberSet(
            UdonBindingSourceType host,
            UdonApiMemberSet set,
            bool isStatic)
        {
            if (set == null)
                return;

            foreach (var method in set.methods ?? Enumerable.Empty<UdonApiMethodRecord>())
            {
                AddMember(host, new UdonBindingSourceMember(
                    host,
                    isStatic
                        ? UdonBindingSourceMemberKind.StaticMethod
                        : UdonBindingSourceMemberKind.InstanceMethod,
                    method.name,
                    isStatic,
                    method.genericParameters,
                    method.parameters,
                    method.returnType,
                    null,
                    method.udonSignature));
            }

            foreach (var property in set.propertyGetters ??
                     Enumerable.Empty<UdonApiPropertyAccessorRecord>())
            {
                AddMember(host, new UdonBindingSourceMember(
                    host,
                    UdonBindingSourceMemberKind.PropertyGetter,
                    property.name,
                    isStatic,
                    null,
                    property.parameters,
                    property.type,
                    property.type,
                    property.udonSignature));
            }
            foreach (var property in set.propertySetters ??
                     Enumerable.Empty<UdonApiPropertyAccessorRecord>())
            {
                AddMember(host, new UdonBindingSourceMember(
                    host,
                    UdonBindingSourceMemberKind.PropertySetter,
                    property.name,
                    isStatic,
                    null,
                    property.parameters,
                    UdonApiTypeRef.Named("System.Void"),
                    property.type,
                    property.udonSignature));
            }
            foreach (var field in set.fieldGetters ??
                     Enumerable.Empty<UdonApiFieldAccessorRecord>())
            {
                AddMember(host, new UdonBindingSourceMember(
                    host,
                    UdonBindingSourceMemberKind.FieldGetter,
                    field.name,
                    isStatic,
                    null,
                    null,
                    field.type,
                    field.type,
                    field.udonSignature));
            }
            foreach (var field in set.fieldSetters ??
                     Enumerable.Empty<UdonApiFieldAccessorRecord>())
            {
                AddMember(host, new UdonBindingSourceMember(
                    host,
                    UdonBindingSourceMemberKind.FieldSetter,
                    field.name,
                    isStatic,
                    null,
                    null,
                    UdonApiTypeRef.Named("System.Void"),
                    field.type,
                    field.udonSignature));
            }
            foreach (var @event in set.events ?? Enumerable.Empty<UdonApiEventRecord>())
            {
                AddMember(host, new UdonBindingSourceMember(
                    host,
                    UdonBindingSourceMemberKind.Event,
                    @event.name,
                    isStatic,
                    null,
                    null,
                    UdonApiTypeRef.Named("System.Void"),
                    @event.type,
                    null));
            }
        }

        private void AddMember(
            UdonBindingSourceType host,
            UdonBindingSourceMember member)
        {
            host.AddMember(member);
            if (!string.IsNullOrEmpty(member.UdonSignature))
                _udonExposedSignatures.Add(member.UdonSignature);
        }
    }
}
