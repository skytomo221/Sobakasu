using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Skytomo221.Sobakasu.Compiler.Target;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    internal enum UdonApiMemberKind
    {
        Constructor,
        StaticMethod,
        InstanceMethod,
        PropertyGetter,
        PropertySetter,
        FieldGetter,
        FieldSetter,
        Event
    }

    internal sealed class ExternTypeRef
    {
        public string kind;
        public string runtimeName;
        public ExternTypeRef element;
        public string scope;
        public int ordinal;
        public ExternTypeRef definition;
        public List<ExternTypeRef> arguments;
    }

    internal sealed class ExternParameterRecord
    {
        public string name;
        public ExternTypeRef type;
        public string passingMode;
        public bool isParams;
    }

    internal sealed class UdonBindingSourceEnumRecord
    {
        public List<UdonApiEnumConstantRecord> constants = new();
    }

    internal sealed class UdonBindingSourceTypeRecord
    {
        public string runtimeName;
        public string clrNamespace;
        public string baseRuntimeName;
        public ExternTypeRef baseType;
        public string shape;
        public int genericArity;
        public bool? isStaticApiContainer;
        public UdonBindingSourceEnumRecord @enum;
    }

    internal sealed class UdonBindingSourceMember
    {
        public string HostRuntimeName { get; }
        public string DeclaringRuntimeName { get; }
        public string Name { get; }
        public string MemberName => Name;
        public string SourceKind { get; }
        public UdonApiMemberKind Kind { get; }
        public bool IsStatic { get; }
        public string ClrSignature { get; }
        public string ExternSignature { get; }
        public string DisplaySignature { get; }
        public IReadOnlyList<ExternParameterRecord> Parameters { get; }
        public ExternTypeRef ReturnType { get; }
        public IReadOnlyList<UdonApiGenericParameterRecord> GenericParameters { get; }
        public bool IsOperator { get; }
        public string OperatorName => IsOperator ? Name : null;
        public string SurfaceTypeName => HostRuntimeName.Replace('+', '.');
        public string ClrDeclaringTypeName => DeclaringRuntimeName.Replace('+', '.');
        public string PhysicalFullName => $"{ClrDeclaringTypeName}.{Name}";
        public string SurfaceFullName => $"{SurfaceTypeName}.{Name}";
        public string FullName => PhysicalFullName;
        public bool IsUdonExposed { get; }
        public string SkipReason { get; set; }
        public bool IsGenerated => string.IsNullOrEmpty(SkipReason);

        public UdonBindingSourceMember(
            string hostRuntimeName,
            string declaringRuntimeName,
            string name,
            string sourceKind,
            UdonApiMemberKind kind,
            bool isStatic,
            string clrSignature,
            string externSignature,
            string displaySignature,
            IReadOnlyList<ExternParameterRecord> parameters,
            ExternTypeRef returnType,
            IReadOnlyList<UdonApiGenericParameterRecord> genericParameters,
            bool isOperator,
            bool isUdonExposed,
            string skipReason = null)
        {
            HostRuntimeName = hostRuntimeName ?? string.Empty;
            DeclaringRuntimeName = declaringRuntimeName ?? HostRuntimeName;
            Name = name ?? string.Empty;
            SourceKind = sourceKind ?? string.Empty;
            Kind = kind;
            IsStatic = isStatic;
            ClrSignature = clrSignature ?? string.Empty;
            ExternSignature = externSignature ?? string.Empty;
            DisplaySignature = displaySignature ?? string.Empty;
            Parameters = parameters ?? Array.Empty<ExternParameterRecord>();
            ReturnType = returnType;
            GenericParameters = genericParameters ?? Array.Empty<UdonApiGenericParameterRecord>();
            IsOperator = isOperator;
            IsUdonExposed = isUdonExposed;
            SkipReason = skipReason;
        }

        public string GetSortKey() =>
            $"{(int)Kind:D2}|{Name}|{DisplaySignature}|{ExternSignature}";
    }

    internal sealed class UdonBindingSourceType
    {
        public UdonBindingSourceTypeRecord Record { get; }
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
        public bool IsStaticApiContainer
        {
            get
            {
                if (Record.isStaticApiContainer == true)
                    return true;
                if (IsEnum)
                    return false;

                var hasDeclaredStaticMember = false;
                foreach (var member in Members)
                {
                    if (!member.IsUdonExposed)
                        continue;
                    if (member.Kind == UdonApiMemberKind.Constructor)
                        continue;
                    if (!member.IsStatic)
                        return false;
                    hasDeclaredStaticMember = true;
                }
                return hasDeclaredStaticMember;
            }
        }

        public UdonBindingSourceType(
            UdonBindingSourceTypeRecord record,
            IReadOnlyList<UdonBindingSourceMember> members,
            string skipReason = null)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Members = members ?? Array.Empty<UdonBindingSourceMember>();
            SkipReason = skipReason;
            var prefix = string.IsNullOrEmpty(record.clrNamespace)
                ? string.Empty
                : record.clrNamespace + ".";
            if (record.runtimeName == null ||
                !record.runtimeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Catalog type '{record.runtimeName}' does not match its persisted CLR namespace.");
            }
            TypePath = record.runtimeName.Substring(prefix.Length).Replace('+', '.');
        }

        public void SkipGeneratedMembers(string reason)
        {
            foreach (var member in Members)
                if (member.IsGenerated)
                    member.SkipReason = reason;
        }
    }

    internal sealed class UdonBindingSourceModel
    {
        public IReadOnlyList<UdonBindingSourceType> Types { get; }
        public IReadOnlyDictionary<string, UdonBindingSourceTypeRecord> TypesByRuntimeName { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<UdonBindingSourceMember>> MembersByHostRuntimeName { get; }
        public ISet<string> UnexposedTypeNames { get; }
        public IReadOnlyList<string> UnmatchedUdonSignatures { get; }
        public IReadOnlyCollection<string> UdonExposedSignatures { get; }
        public IReadOnlyCollection<string> CapabilitySignatures { get; private set; } = Array.Empty<string>();
        public int CatalogDeclaredMembers { get; private set; }
        public int CatalogExposedDeclaredMembers { get; private set; }
        public int CatalogUnexposedDeclaredMembers { get; private set; }
        public int CatalogUnmodeledSignatures { get; private set; }
        public IReadOnlyList<string> ReconciledSignatures { get; private set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> DeclaredPhysicalSignatures { get; private set; } = Array.Empty<string>();

        private UdonBindingSourceModel(
            IReadOnlyList<UdonBindingSourceType> types,
            IReadOnlyDictionary<string, UdonBindingSourceTypeRecord> records,
            IReadOnlyDictionary<string, IReadOnlyList<UdonBindingSourceMember>> members,
            ISet<string> unexposedTypeNames,
            IReadOnlyList<string> unmatchedUdonSignatures,
            IReadOnlyCollection<string> exposedSignatures)
        {
            Types = types;
            TypesByRuntimeName = records;
            MembersByHostRuntimeName = members;
            UnexposedTypeNames = unexposedTypeNames;
            UnmatchedUdonSignatures = unmatchedUdonSignatures;
            UdonExposedSignatures = exposedSignatures;
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
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (catalog.formatVersion != 2)
            {
                throw new InvalidDataException(
                    "The Udon API catalog is too old for standard-library generation. " +
                    "Regenerate it manually from Window/Sobakasu/Build Udon API Catalog.");
            }
            if (catalog.namespaces == null || catalog.namespaces.Count == 0)
                throw new InvalidDataException("The Udon API catalog contains no namespace records.");

            var persistedTypes = catalog.namespaces.Where(ns => ns != null)
                .SelectMany(ns => CatalogTypes(ns.exposed).Concat(CatalogTypes(ns.unexposed)))
                .Where(type => type != null).ToArray();
            var exposedMemberCount = persistedTypes.Sum(type => CountMembers(type.exposed));
            var unexposedMemberCount = persistedTypes.Sum(type => CountMembers(type.unexposed));
            var persistedUnmodeledCount = catalog.unmodeledUdonSignatures?.Count ?? 0;
            var reconciled = UdonApiCatalogExposureReconciler.Reconcile(catalog);

            var records = new Dictionary<string, UdonBindingSourceTypeRecord>(StringComparer.Ordinal);
            var exposedDeclarations = new List<(string Namespace, UdonApiTypeRecord Record, UdonBindingSourceTypeRecord Source)>();
            var unexposed = new HashSet<string>(StringComparer.Ordinal);
            var seenNamespaces = new HashSet<string>(StringComparer.Ordinal);

            foreach (var ns in catalog.namespaces)
            {
                if (ns == null || ns.name == null || !seenNamespaces.Add(ns.name))
                    throw new InvalidDataException("The Udon API catalog contains an invalid or duplicate namespace.");
                AddTypeSet(ns.name, ns.exposed, true, records, unexposed, exposedDeclarations);
                AddTypeSet(ns.name, ns.unexposed, false, records, unexposed, exposedDeclarations);
            }

            var groups = new Dictionary<string, IReadOnlyList<UdonBindingSourceMember>>(StringComparer.Ordinal);
            // CLR value types have implicit base classes; v2 stores their
            // interfaces only, so recover these language-defined inheritance edges.
            foreach (var record in records.Values)
            {
                var implicitBase = record.shape == "Value" ? "System.ValueType" :
                    record.shape == "Enum" ? "System.Enum" : null;
                if (implicitBase != null && records.ContainsKey(implicitBase))
                    record.baseRuntimeName = implicitBase;
            }
            var types = new List<UdonBindingSourceType>();
            var exposedSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var declaration in exposedDeclarations)
            {
                var members = ProjectMembers(declaration.Source, declaration.Record.exposed, exposedSignatures);
                groups.Add(declaration.Source.runtimeName, members);
                types.Add(new UdonBindingSourceType(declaration.Source, members));
            }
            types.Sort((left, right) => string.CompareOrdinal(left.RuntimeName, right.RuntimeName));

            var unmatched = (catalog.unmodeledUdonSignatures ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            foreach (var signature in unmatched)
            {
                if (string.IsNullOrEmpty(signature))
                    throw new InvalidDataException("Unmodeled Udon signatures must be non-empty.");
                exposedSignatures.Add(signature);
            }
            var capabilities = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var array in catalog.capabilities?.arrays ?? new List<ArrayCapabilityRecord>())
                foreach (var signature in new[] { array.constructor, array.getter, array.setter, array.length })
                    if (!string.IsNullOrEmpty(signature)) capabilities.Add(signature);
            exposedSignatures.UnionWith(capabilities);

            // Persist declarations once; project each owner's public surface here.
            var direct = new Dictionary<string, IReadOnlyList<UdonBindingSourceMember>>(groups, StringComparer.Ordinal);
            types.Clear();
            foreach (var declaration in exposedDeclarations)
            {
                var host = declaration.Source;
                var surface = new List<UdonBindingSourceMember>(direct[host.runtimeName]);
                var identities = new HashSet<string>(surface.Select(SurfaceSignature), StringComparer.Ordinal);
                var visited = new HashSet<string>(StringComparer.Ordinal) { host.runtimeName };
                var baseName = host.baseRuntimeName;
                var staticContainer = new UdonBindingSourceType(host, direct[host.runtimeName]).IsStaticApiContainer;
                while (!string.IsNullOrEmpty(baseName))
                {
                    if (!visited.Add(baseName))
                        throw new InvalidDataException($"Cyclic catalog inheritance at '{baseName}'.");
                    if (!records.TryGetValue(baseName, out var baseRecord))
                        throw new InvalidDataException($"Missing catalog base type '{baseName}'.");
                    if (direct.TryGetValue(baseName, out var inherited))
                        foreach (var member in inherited)
                        {
                            if (member.Kind == UdonApiMemberKind.Constructor || member.IsOperator ||
                                staticContainer && !member.IsStatic ||
                                !identities.Add(SurfaceSignature(member)))
                                continue;
                            var signature = member.ExternSignature;
                            if (!member.IsStatic && UdonExternSignatureIdentity.TryParse(signature, out var abi))
                            {
                                var candidate = abi.OnHost(host.runtimeName);
                                if (exposedSignatures.Contains(candidate)) signature = candidate;
                            }
                            if (!exposedSignatures.Contains(signature)) continue;
                            surface.Add(new UdonBindingSourceMember(host.runtimeName, member.DeclaringRuntimeName,
                                member.Name, member.SourceKind, member.Kind, member.IsStatic, member.ClrSignature,
                                signature, member.DisplaySignature, member.Parameters, member.ReturnType,
                                member.GenericParameters, member.IsOperator, true, member.SkipReason));
                        }
                    baseName = baseRecord.baseRuntimeName;
                }
                surface.Sort((left, right) => string.CompareOrdinal(left.GetSortKey(), right.GetSortKey()));
                groups[host.runtimeName] = surface;
                types.Add(new UdonBindingSourceType(host, surface));
            }
            types.Sort((left, right) => string.CompareOrdinal(left.RuntimeName, right.RuntimeName));

            return new UdonBindingSourceModel(
                types,
                records,
                groups,
                unexposed,
                unmatched,
                exposedSignatures)
            {
                CapabilitySignatures = capabilities,
                ReconciledSignatures = reconciled,
                DeclaredPhysicalSignatures = direct.Values.SelectMany(members => members)
                    .Where(member => member.IsUdonExposed && !string.IsNullOrEmpty(member.ExternSignature))
                    .Select(member => member.ExternSignature).Distinct(StringComparer.Ordinal)
                    .OrderBy(signature => signature, StringComparer.Ordinal).ToArray(),
                CatalogDeclaredMembers = exposedMemberCount + unexposedMemberCount,
                CatalogExposedDeclaredMembers = exposedMemberCount,
                CatalogUnexposedDeclaredMembers = unexposedMemberCount,
                CatalogUnmodeledSignatures = persistedUnmodeledCount
            };
        }

        private static IEnumerable<UdonApiTypeRecord> CatalogTypes(UdonApiTypeSet set) =>
            (set?.classes ?? new List<UdonApiClassRecord>()).Cast<UdonApiTypeRecord>()
                .Concat(set?.structs ?? new List<UdonApiStructRecord>())
                .Concat(set?.interfaces ?? new List<UdonApiInterfaceRecord>())
                .Concat(set?.enums ?? new List<UdonApiEnumTypeRecord>());

        private static int CountMembers(UdonApiMemberExposure exposure)
        {
            if (exposure == null) return 0;
            var count = (exposure.constructors?.Count ?? 0) + (exposure.operators?.Count ?? 0);
            foreach (var set in new[] { exposure.instance, exposure.@static })
                if (set != null)
                    count += (set.methods?.Count ?? 0) + (set.propertyGetters?.Count ?? 0) +
                        (set.propertySetters?.Count ?? 0) + (set.fieldGetters?.Count ?? 0) +
                        (set.fieldSetters?.Count ?? 0) + (set.events?.Count ?? 0);
            return count;
        }

        private static string SurfaceSignature(UdonBindingSourceMember member) =>
            $"{member.Kind}|{member.Name}|{member.GenericParameters.Count}|" +
            string.Join(",", member.Parameters.Select(parameter =>
                parameter.passingMode + ":" + FormatClrType(parameter.type, canonical: true)));

        /// <summary>
        /// Test/Catalog-generation adapter. Production Standard Library generation
        /// does not call reflection discovery; existing reflection fixtures are
        /// projected into the same source model so they exercise the production
        /// policy and renderer during migration.
        /// </summary>
        public static UdonBindingSourceModel FromReflectionModel(UdonApiModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var records = new Dictionary<string, UdonBindingSourceTypeRecord>(StringComparer.Ordinal);
            var groups = new Dictionary<string, IReadOnlyList<UdonBindingSourceMember>>(StringComparer.Ordinal);
            var types = new List<UdonBindingSourceType>();
            foreach (var reflectedType in model.Types)
            {
                var clrType = reflectedType.ClrType;
                var runtimeName = clrType.FullName ?? clrType.Name;
                var record = CreateReflectionTypeRecord(clrType, runtimeName);
                records[runtimeName] = record;

                var members = new List<UdonBindingSourceMember>();
                foreach (var reflectedMember in reflectedType.Members)
                {
                    members.Add(ProjectReflectionMember(runtimeName, reflectedMember));
                    if (reflectedMember.Callable is MethodInfo method)
                        AddReflectionTypeRecord(method.ReturnType, records);
                    if (reflectedMember.Callable != null)
                        foreach (var parameter in reflectedMember.Callable.GetParameters())
                            AddReflectionTypeRecord(parameter.ParameterType, records);
                    if (reflectedMember.Member is PropertyInfo property)
                        AddReflectionTypeRecord(property.PropertyType, records);
                    if (reflectedMember.Member is FieldInfo field)
                        AddReflectionTypeRecord(field.FieldType, records);
                    if (reflectedMember.OperatorReturnType != null)
                        AddReflectionTypeRecord(reflectedMember.OperatorReturnType, records);
                }
                members.Sort((left, right) => string.CompareOrdinal(left.GetSortKey(), right.GetSortKey()));
                groups.Add(runtimeName, members);
                types.Add(new UdonBindingSourceType(record, members, reflectedType.SkipReason));
            }
            types.Sort((left, right) => string.CompareOrdinal(left.RuntimeName, right.RuntimeName));
            return new UdonBindingSourceModel(
                types,
                records,
                groups,
                new HashSet<string>(StringComparer.Ordinal),
                Array.Empty<string>(),
                model.UdonExposedSignatures);
        }

        private static void AddReflectionTypeRecord(
            Type type, IDictionary<string, UdonBindingSourceTypeRecord> records)
        {
            if (type.HasElementType)
            {
                AddReflectionTypeRecord(type.GetElementType(), records);
                return;
            }
            if (type.IsGenericParameter)
                return;
            if (type.IsConstructedGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                    AddReflectionTypeRecord(argument, records);
                type = type.GetGenericTypeDefinition();
            }
            var name = type.FullName ?? type.Name;
            if (!records.ContainsKey(name))
                records.Add(name, CreateReflectionTypeRecord(type, name));
        }

        private static void AddTypeSet(
            string clrNamespace,
            UdonApiTypeSet set,
            bool exposed,
            IDictionary<string, UdonBindingSourceTypeRecord> records,
            ISet<string> unexposed,
            ICollection<(string Namespace, UdonApiTypeRecord Record, UdonBindingSourceTypeRecord Source)> exposedDeclarations)
        {
            if (set == null)
                return;
            AddTypes(clrNamespace, set.classes, "Reference", exposed, records, unexposed, exposedDeclarations);
            AddTypes(clrNamespace, set.interfaces, "Reference", exposed, records, unexposed, exposedDeclarations);
            AddTypes(clrNamespace, set.structs, "Value", exposed, records, unexposed, exposedDeclarations);
            AddTypes(clrNamespace, set.enums, "Enum", exposed, records, unexposed, exposedDeclarations);
        }

        private static void AddTypes<T>(
            string clrNamespace,
            IEnumerable<T> values,
            string shape,
            bool exposed,
            IDictionary<string, UdonBindingSourceTypeRecord> records,
            ISet<string> unexposed,
            ICollection<(string Namespace, UdonApiTypeRecord Record, UdonBindingSourceTypeRecord Source)> exposedDeclarations)
            where T : UdonApiTypeRecord
        {
            if (values == null)
                return;
            foreach (var value in values)
            {
                if (value?.path == null || value.path.Count == 0 ||
                    value.path.Any(segment => string.IsNullOrEmpty(segment) || segment.Contains(".") || segment.Contains("+")))
                {
                    throw new InvalidDataException("The Udon API catalog contains an invalid type path.");
                }
                var runtimeName = BuildRuntimeName(clrNamespace, value.path);
                var source = new UdonBindingSourceTypeRecord
                {
                    runtimeName = runtimeName,
                    clrNamespace = clrNamespace,
                    baseRuntimeName = (value as UdonApiClassRecord)?.baseType?.named ??
                        (value as UdonApiClassRecord)?.baseType?.generic,
                    baseType = (value as UdonApiClassRecord)?.baseType == null ? null :
                        ToExternTypeRef((value as UdonApiClassRecord).baseType),
                    shape = shape,
                    genericArity = value.genericParameters?.Count ?? 0,
                    isStaticApiContainer = value is UdonApiClassRecord cls && cls.@abstract && cls.@sealed,
                    @enum = value is UdonApiEnumTypeRecord enumRecord
                        ? new UdonBindingSourceEnumRecord
                        {
                            constants = enumRecord.constants ?? new List<UdonApiEnumConstantRecord>()
                        }
                        : null
                };
                if (!records.TryAdd(runtimeName, source))
                    throw new InvalidDataException($"The Udon API catalog contains duplicate type '{runtimeName}'.");
                if (exposed)
                    exposedDeclarations.Add((clrNamespace, value, source));
                else
                    unexposed.Add(runtimeName);
            }
        }

        private static IReadOnlyList<UdonBindingSourceMember> ProjectMembers(
            UdonBindingSourceTypeRecord host,
            UdonApiMemberExposure exposure,
            ISet<string> exposedSignatures)
        {
            var result = new List<UdonBindingSourceMember>();
            if (exposure == null)
                return result;

            foreach (var constructor in exposure.constructors ?? Enumerable.Empty<UdonApiConstructorRecord>())
            {
                AddProjectedMember(result, exposedSignatures, host, host.runtimeName, UdonApiMemberKind.Constructor,
                    "Constructor", false, constructor.parameters, UdonApiTypeRef.Named(host.runtimeName), null,
                    constructor.udonSignature, null);
            }
            ProjectMemberSet(result, exposedSignatures, host, exposure.instance, false);
            ProjectMemberSet(result, exposedSignatures, host, exposure.@static, true);
            foreach (var op in exposure.operators ?? Enumerable.Empty<UdonApiOperatorRecord>())
            {
                AddProjectedMember(result, exposedSignatures, host, op.name, UdonApiMemberKind.StaticMethod,
                    "Operator", true, op.parameters, op.returnType, null, op.udonSignature, null, true);
            }
            result.Sort((left, right) => string.CompareOrdinal(left.GetSortKey(), right.GetSortKey()));
            return result;
        }

        private static void ProjectMemberSet(
            ICollection<UdonBindingSourceMember> result,
            ISet<string> exposedSignatures,
            UdonBindingSourceTypeRecord host,
            UdonApiMemberSet set,
            bool isStatic)
        {
            if (set == null)
                return;
            foreach (var method in set.methods ?? Enumerable.Empty<UdonApiMethodRecord>())
            {
                AddProjectedMember(result, exposedSignatures, host, method.name,
                    isStatic ? UdonApiMemberKind.StaticMethod : UdonApiMemberKind.InstanceMethod,
                    isStatic ? "StaticMethod" : "InstanceMethod", isStatic, method.parameters,
                    method.returnType, method.genericParameters, method.udonSignature, null);
            }
            foreach (var property in set.propertyGetters ?? Enumerable.Empty<UdonApiPropertyAccessorRecord>())
            {
                AddProjectedMember(result, exposedSignatures, host, property.name,
                    UdonApiMemberKind.PropertyGetter, "PropertyGetter", isStatic, property.parameters,
                    property.type, null, property.udonSignature, property.type);
            }
            foreach (var property in set.propertySetters ?? Enumerable.Empty<UdonApiPropertyAccessorRecord>())
            {
                var parameters = new List<UdonApiParameterRecord>(property.parameters ?? new List<UdonApiParameterRecord>())
                {
                    new() { name = "value", type = property.type }
                };
                AddProjectedMember(result, exposedSignatures, host, property.name,
                    UdonApiMemberKind.PropertySetter, "PropertySetter", isStatic, parameters,
                    UdonApiTypeRef.Named("System.Void"), null, property.udonSignature, property.type);
            }
            foreach (var field in set.fieldGetters ?? Enumerable.Empty<UdonApiFieldAccessorRecord>())
            {
                AddProjectedMember(result, exposedSignatures, host, field.name,
                    UdonApiMemberKind.FieldGetter, "FieldGetter", isStatic, null,
                    field.type, null, field.udonSignature, field.type);
            }
            foreach (var field in set.fieldSetters ?? Enumerable.Empty<UdonApiFieldAccessorRecord>())
            {
                AddProjectedMember(result, exposedSignatures, host, field.name,
                    UdonApiMemberKind.FieldSetter, "FieldSetter", isStatic,
                    new[] { new UdonApiParameterRecord { name = "value", type = field.type } },
                    UdonApiTypeRef.Named("System.Void"), null, field.udonSignature, field.type);
            }
        }

        private static void AddProjectedMember(
            ICollection<UdonBindingSourceMember> result,
            ISet<string> exposedSignatures,
            UdonBindingSourceTypeRecord host,
            string name,
            UdonApiMemberKind kind,
            string sourceKind,
            bool isStatic,
            IEnumerable<UdonApiParameterRecord> parameters,
            UdonApiTypeRef returnType,
            IReadOnlyList<UdonApiGenericParameterRecord> genericParameters,
            string udonSignature,
            UdonApiTypeRef valueType,
            bool isOperator = false)
        {
            var projectedParameters = (parameters ?? Enumerable.Empty<UdonApiParameterRecord>())
                .Select(ToExternParameter).ToArray();
            var projectedReturn = ToExternTypeRef(returnType);
            var clrSignature = BuildClrSignature(host.runtimeName, name, kind, projectedParameters);
            var member = new UdonBindingSourceMember(
                host.runtimeName,
                host.runtimeName,
                name,
                sourceKind,
                kind,
                isStatic,
                clrSignature,
                udonSignature,
                clrSignature,
                projectedParameters,
                projectedReturn,
                genericParameters,
                isOperator,
                !string.IsNullOrEmpty(udonSignature));
            result.Add(member);
            if (!string.IsNullOrEmpty(udonSignature))
                exposedSignatures.Add(udonSignature);
        }

        private static UdonBindingSourceTypeRecord CreateReflectionTypeRecord(Type type, string runtimeName)
        {
            var enumRecord = type.IsEnum
                ? new UdonBindingSourceEnumRecord
                {
                    constants = Enum.GetNames(type)
                        .Select(name => new UdonApiEnumConstantRecord { name = name, value = "0" })
                        .ToList()
                }
                : null;
            return new UdonBindingSourceTypeRecord
            {
                runtimeName = runtimeName,
                clrNamespace = type.Namespace ?? string.Empty,
                baseRuntimeName = type.BaseType?.FullName,
                baseType = type.BaseType == null ? null : ToExternTypeRef(type.BaseType),
                shape = type.IsEnum ? "Enum" : type.IsValueType ? "Value" : "Reference",
                genericArity = type.IsGenericTypeDefinition ? type.GetGenericArguments().Length : 0,
                isStaticApiContainer = type.IsClass && type.IsAbstract && type.IsSealed,
                @enum = enumRecord
            };
        }

        private static UdonBindingSourceMember ProjectReflectionMember(
            string hostRuntimeName,
            UdonApiMemberModel member)
        {
            var callable = member.Callable ?? (member.Member is PropertyInfo property
                ? member.Kind == UdonApiMemberKind.PropertySetter ? property.SetMethod : property.GetMethod
                : null);
            var parameters = callable != null
                ? callable.GetParameters().Select(ToExternParameter).ToArray()
                : member.OperatorParameterTypes?.Select((type, index) => new ExternParameterRecord
                {
                    name = "arg" + index,
                    type = ToExternTypeRef(type),
                    passingMode = "Value"
                }).ToArray() ?? Array.Empty<ExternParameterRecord>();
            if (member.Kind == UdonApiMemberKind.FieldSetter && member.Member is FieldInfo field)
                parameters = new[]
                {
                    new ExternParameterRecord { name = "value", type = ToExternTypeRef(field.FieldType), passingMode = "Value" }
                };
            var returnType = member.Callable is MethodInfo method
                ? ToExternTypeRef(method.ReturnType)
                : member.IsSyntheticOperator
                    ? ToExternTypeRef(member.OperatorReturnType)
                    : member.Kind == UdonApiMemberKind.Constructor
                        ? Named(hostRuntimeName)
                        : GetReflectionValueType(member);
            if (member.Kind == UdonApiMemberKind.PropertySetter || member.Kind == UdonApiMemberKind.FieldSetter)
                returnType = Named("System.Void");
            var sourceKind = member.IsOperator ? "Operator" : member.Kind.ToString();
            return new UdonBindingSourceMember(
                hostRuntimeName,
                member.ClrDeclaringType?.FullName ?? hostRuntimeName,
                member.MemberName,
                sourceKind,
                member.Kind,
                callable?.IsStatic ?? (member.Member as FieldInfo)?.IsStatic ?? member.IsOperator,
                ClrMemberId.Format(member),
                member.ExternSignature,
                member.DisplaySignature,
                parameters,
                returnType,
                member.Callable is MethodInfo generic && generic.IsGenericMethodDefinition
                    ? generic.GetGenericArguments().Select(ToGenericParameter).ToArray()
                    : Array.Empty<UdonApiGenericParameterRecord>(),
                member.IsOperator,
                member.IsUdonExposed,
                member.SkipReason);
        }

        private static ExternTypeRef GetReflectionValueType(UdonApiMemberModel member)
        {
            if (member.Member is PropertyInfo property)
                return ToExternTypeRef(property.PropertyType);
            if (member.Member is FieldInfo field)
                return ToExternTypeRef(field.FieldType);
            return Named("System.Void");
        }

        private static UdonApiGenericParameterRecord ToGenericParameter(Type type) =>
            new() { name = type.Name };

        private static ExternParameterRecord ToExternParameter(ParameterInfo parameter)
        {
            var passing = parameter.ParameterType.IsByRef
                ? parameter.IsOut ? "Out" : parameter.IsIn ? "In" : "Ref"
                : "Value";
            return new ExternParameterRecord
            {
                name = parameter.Name ?? string.Empty,
                type = ToExternTypeRef(parameter.ParameterType),
                passingMode = passing,
                isParams = Attribute.IsDefined(parameter, typeof(ParamArrayAttribute))
            };
        }

        private static ExternParameterRecord ToExternParameter(UdonApiParameterRecord parameter)
        {
            return new ExternParameterRecord
            {
                name = parameter?.name ?? string.Empty,
                type = ToExternTypeRef(parameter?.type),
                isParams = parameter?.@params == true,
                passingMode = parameter?.passing switch
                {
                    "out" => "Out",
                    "ref" => "Ref",
                    "in" => "In",
                    _ => "Value"
                }
            };
        }

        private static ExternTypeRef ToExternTypeRef(Type type)
        {
            if (type == null)
                return null;
            if (type.IsByRef)
                type = type.GetElementType();
            if (type.IsArray)
                return new ExternTypeRef { kind = "Array", element = ToExternTypeRef(type.GetElementType()) };
            if (type.IsGenericParameter)
                return new ExternTypeRef
                {
                    kind = "GenericParameter",
                    scope = type.DeclaringMethod == null ? "Type" : "Method",
                    ordinal = type.GenericParameterPosition
                };
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                return new ExternTypeRef
                {
                    kind = "ConstructedGeneric",
                    definition = Named(type.GetGenericTypeDefinition().FullName ?? type.GetGenericTypeDefinition().Name),
                    arguments = type.GetGenericArguments().Select(ToExternTypeRef).ToList()
                };
            return Named(type.FullName ?? type.Name);
        }

        private static ExternTypeRef ToExternTypeRef(UdonApiTypeRef type)
        {
            if (type == null)
                return null;
            if (type.named != null)
                return Named(type.named);
            if (type.array != null)
            {
                if ((type.rank ?? 1) != 1)
                    return new ExternTypeRef { kind = "UnsupportedArray", element = ToExternTypeRef(type.array) };
                return new ExternTypeRef { kind = "Array", element = ToExternTypeRef(type.array) };
            }
            if (type.typeParameter.HasValue)
                return new ExternTypeRef { kind = "GenericParameter", scope = "Type", ordinal = type.typeParameter.Value };
            if (type.methodParameter.HasValue)
                return new ExternTypeRef { kind = "GenericParameter", scope = "Method", ordinal = type.methodParameter.Value };
            if (type.generic != null)
                return new ExternTypeRef
                {
                    kind = "ConstructedGeneric",
                    definition = Named(type.generic),
                    arguments = (type.arguments ?? new List<UdonApiTypeRef>()).Select(ToExternTypeRef).ToList()
                };
            return new ExternTypeRef { kind = "Invalid" };
        }

        private static ExternTypeRef Named(string runtimeName) =>
            new() { kind = "Named", runtimeName = runtimeName };

        private static string BuildRuntimeName(string clrNamespace, IReadOnlyList<string> path)
        {
            var root = string.IsNullOrEmpty(clrNamespace) ? path[0] : clrNamespace + "." + path[0];
            return path.Count == 1 ? root : root + "+" + string.Join("+", path.Skip(1));
        }

        private static string BuildClrSignature(
            string runtimeName,
            string name,
            UdonApiMemberKind kind,
            IReadOnlyList<ExternParameterRecord> parameters)
        {
            var owner = runtimeName.Replace('+', '.');
            if (kind == UdonApiMemberKind.PropertyGetter || kind == UdonApiMemberKind.PropertySetter ||
                kind == UdonApiMemberKind.FieldGetter || kind == UdonApiMemberKind.FieldSetter ||
                kind == UdonApiMemberKind.Event)
                return owner + "." + name;
            var argumentTypes = string.Join(",", parameters.Select(parameter =>
                FormatClrType(parameter.type) +
                (parameter.passingMode == "Ref" || parameter.passingMode == "Out" || parameter.passingMode == "In" ? "&" : string.Empty)));
            return kind == UdonApiMemberKind.Constructor
                ? owner + "(" + argumentTypes + ")"
                : owner + "." + name + "(" + argumentTypes + ")";
        }

        private static string FormatClrType(ExternTypeRef type) => FormatClrType(type, canonical: false);

        private static string FormatClrType(ExternTypeRef type, bool canonical)
        {
            if (type == null)
                return "<missing>";
            return type.kind switch
            {
                "Named" => (canonical ? type.runtimeName : type.runtimeName?.Replace('+', '.')) ?? "<missing>",
                "Array" => FormatClrType(type.element, canonical) + "[]",
                "GenericParameter" => (type.scope == "Method" ? "!!" : "!") + type.ordinal,
                "ConstructedGeneric" => FormatClrType(type.definition, canonical) + "<" +
                    string.Join(",", (type.arguments ?? new List<ExternTypeRef>()).Select(argument => FormatClrType(argument, canonical))) + ">",
                _ => "<unsupported>"
            };
        }
    }
}
