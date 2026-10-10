using System;
using System.Collections.Generic;
using System.IO;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    internal enum UdonApiMemberKind { Constructor, StaticMethod, InstanceMethod, PropertyGetter, PropertySetter, FieldGetter, FieldSetter, Event }

    internal sealed class UdonBindingSourceType
    {
        public UdonApiTypeRecord Record { get; }
        public IReadOnlyList<UdonBindingSourceMember> Members { get; }
        public string RuntimeName => Record.runtimeName;
        public string ClrNamespace => Record.clrNamespace;
        public string TypePath { get; }
        public string QualifiedName => RuntimeName.Replace('+', '.');
        public string WrapperName
        {
            get
            {
                var segment = TypePath.Substring(TypePath.LastIndexOf('.') + 1);
                var tick = segment.IndexOf('`');
                return tick < 0 ? segment : segment.Substring(0, tick);
            }
        }
        public string SkipReason { get; set; }
        public bool IsGenerated => string.IsNullOrEmpty(SkipReason);
        public bool IsValueType => Record.shape == "Value" || Record.shape == "Enum";
        public bool IsEnum => Record.shape == "Enum";
        public bool IsStaticApiContainer => Record.isStaticApiContainer == true;
        public void SkipGeneratedMembers(string reason) { foreach (var member in Members) if (member.IsGenerated) member.SkipReason = reason; }
        public UdonBindingSourceType(UdonApiTypeRecord record, IReadOnlyList<UdonBindingSourceMember> members)
        {
            Record = record;
            Members = members;
            var prefix = string.IsNullOrEmpty(record.clrNamespace) ? string.Empty : record.clrNamespace + ".";
            if (!record.runtimeName.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException($"Catalog type '{record.runtimeName}' does not match its persisted CLR namespace.");
            TypePath = record.runtimeName.Substring(prefix.Length).Replace('+', '.');
        }
    }

    internal sealed class UdonBindingSourceMember
    {
        public UdonApiMemberRecord Record { get; }
        public string HostRuntimeName => Record.hostType?.runtimeName ?? string.Empty;
        public string DeclaringRuntimeName => Record.clrDeclaringType?.runtimeName ?? string.Empty;
        public string Name => Record.name ?? string.Empty;
        public string MemberName => Name;
        public string SourceKind => Record.sourceKind ?? string.Empty;
        public UdonApiMemberKind Kind => Enum.TryParse(SourceKind, out UdonApiMemberKind kind) ? kind : UdonApiMemberKind.Event;
        public bool IsStatic => Record.isStatic == true;
        public string ClrSignature => Record.clrSignature ?? string.Empty;
        public string ExternSignature => Record.externSignature ?? string.Empty;
        public string DisplaySignature => Record.displaySignature ?? string.Empty;
        public IReadOnlyList<ExternParameterRecord> Parameters => Record.abiParameters;
        public ExternTypeRef ReturnType => Record.abiReturnType;
        public IReadOnlyList<UdonApiGenericParameterRecord> GenericParameters => Record.genericParameters;
        public bool IsOperator => Record.kind == "Operator";
        public string OperatorName => IsOperator ? Name : null;
        public string SurfaceTypeName => HostRuntimeName.Replace('+', '.');
        public string ClrDeclaringTypeName => DeclaringRuntimeName.Replace('+', '.');
        public string PhysicalFullName => $"{ClrDeclaringTypeName}.{Name}";
        public string SurfaceFullName => $"{SurfaceTypeName}.{Name}";
        public string FullName => PhysicalFullName;
        public bool IsUdonExposed => true;
        public string SkipReason { get; set; }
        public bool IsGenerated => string.IsNullOrEmpty(SkipReason);
        public UdonBindingSourceMember(UdonApiMemberRecord record) { Record = record; }
    }

    internal sealed class UdonBindingSourceModel
    {
        public IReadOnlyList<UdonBindingSourceType> Types { get; }
        public IReadOnlyDictionary<string, UdonApiTypeRecord> TypesByRuntimeName { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<UdonBindingSourceMember>> MembersByHostRuntimeName { get; }
        public ISet<string> UnexposedTypeNames { get; }
        public IReadOnlyList<string> UnmatchedUdonSignatures { get; }
        public IReadOnlyCollection<string> UdonExposedSignatures { get; }

        private UdonBindingSourceModel(UdonApiCatalogData catalog)
        {
            if (catalog.formatVersion != 2)
                throw new InvalidDataException("The Udon API catalog is too old for standard-library generation. Regenerate it manually from Window/Sobakasu/Build Udon API Catalog.");
            var records = new Dictionary<string, UdonApiTypeRecord>(StringComparer.Ordinal);
            foreach (var type in catalog.types ?? new List<UdonApiTypeRecord>())
            {
                if (type == null || string.IsNullOrWhiteSpace(type.runtimeName) || type.clrNamespace == null || !type.isStaticApiContainer.HasValue || records.ContainsKey(type.runtimeName))
                    throw new InvalidDataException("The Udon API catalog is missing required type metadata or contains a duplicate type.");
                records.Add(type.runtimeName, type);
            }
            var unexposed = new HashSet<string>(catalog.unexposedClrTypeNames ?? new List<string>(), StringComparer.Ordinal);
            var groups = new Dictionary<string, List<UdonBindingSourceMember>>(StringComparer.Ordinal);
            foreach (var record in catalog.members ?? new List<UdonApiMemberRecord>())
            {
                if (record == null || !IsKnownSourceKind(record.sourceKind) || record.displaySignature == null || !record.isStatic.HasValue || record.clrSignature == null || record.clrDeclaringType == null || record.hostType == null || string.IsNullOrWhiteSpace(record.hostType.runtimeName))
                    throw new InvalidDataException("The Udon API catalog is missing required standard-library member metadata.");
                if (!groups.TryGetValue(record.hostType.runtimeName, out var members)) groups.Add(record.hostType.runtimeName, members = new List<UdonBindingSourceMember>());
                members.Add(new UdonBindingSourceMember(record));
            }
            var readOnlyGroups = new Dictionary<string, IReadOnlyList<UdonBindingSourceMember>>(StringComparer.Ordinal);
            foreach (var pair in groups) readOnlyGroups.Add(pair.Key, pair.Value);
            var types = new List<UdonBindingSourceType>();
            foreach (var record in records.Values)
            {
                if (unexposed.Contains(record.runtimeName)) continue;
                groups.TryGetValue(record.runtimeName, out var members);
                types.Add(new UdonBindingSourceType(record, members ?? new List<UdonBindingSourceMember>()));
            }
            types.Sort((left, right) => string.CompareOrdinal(left.RuntimeName, right.RuntimeName));
            Types = types;
            TypesByRuntimeName = records;
            MembersByHostRuntimeName = readOnlyGroups;
            UnexposedTypeNames = unexposed;
            UnmatchedUdonSignatures = catalog.unmatchedUdonSignatures ?? new List<string>();
            var signatures = new HashSet<string>(UnmatchedUdonSignatures, StringComparer.Ordinal);
            foreach (var member in catalog.members ?? new List<UdonApiMemberRecord>())
                if (!string.IsNullOrEmpty(member.externSignature)) signatures.Add(member.externSignature);
            UdonExposedSignatures = signatures;
        }

        public static UdonBindingSourceModel Load(string catalogPath)
        {
            if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
                throw new FileNotFoundException("Udon API catalog was not found. Generate it manually from: Window/Sobakasu/Build Udon API Catalog", catalogPath);
            using var reader = File.OpenText(catalogPath);
            return FromCatalog(UdonApiCatalogReader.Read(reader));
        }

        public static UdonBindingSourceModel FromCatalog(UdonApiCatalogData catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return new UdonBindingSourceModel(catalog);
        }

        private static bool IsKnownSourceKind(string value)
        {
            return value == "StaticMethod" || value == "InstanceMethod" || value == "Constructor" ||
                value == "PropertyGetter" || value == "PropertySetter" || value == "FieldGetter" ||
                value == "FieldSetter" || value == "Event";
        }
    }
}
