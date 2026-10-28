using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog
{
    internal static class UdonApiCatalogLoader
    {
        private const int SupportedFormatVersion = 2;

        public static ExternCatalog Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("The Udon API catalog is empty.");
            JObject root;
            try
            {
                using var reader = new JsonTextReader(new StringReader(json));
                root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            catch (JsonException exception) { throw new InvalidDataException("The Udon API catalog is invalid JSON.", exception); }
            ValidateJsonShape(root, null);
            UdonApiCatalogData data;
            try
            {
                data = root.ToObject<UdonApiCatalogData>(JsonSerializer.Create(new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                    Converters = { new UdonApiTypeRefJsonConverter() }
                }));
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                throw new InvalidDataException("The Udon API catalog has an invalid v2 record.", exception);
            }
            return Load(data);
        }

        public static ExternCatalog Load(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            return Load(reader.ReadToEnd());
        }

        private static void ValidateJsonShape(JToken token, string propertyName)
        {
            if (token.Type == JTokenType.Null) throw new InvalidDataException("Null is prohibited in the Udon API catalog.");
            if (token is JArray array)
            {
                if (array.Count == 0) throw new InvalidDataException("Empty arrays are prohibited in the Udon API catalog.");
                foreach (var item in array) ValidateJsonShape(item, propertyName);
            }
            else if (token is JObject obj)
            {
                if (!obj.Properties().Any() && propertyName != "constructors") throw new InvalidDataException("Empty objects are prohibited in the Udon API catalog.");
                foreach (var property in obj.Properties())
                {
                    if (property.Value.Type == JTokenType.Boolean && !(bool)property.Value &&
                        (property.Name == "abstract" || property.Name == "sealed" || property.Name == "hasPublicParameterlessConstructor" || property.Name == "referenceType" || property.Name == "nonNullableValueType" || property.Name == "defaultConstructor" || property.Name == "params"))
                        throw new InvalidDataException($"Optional boolean '{property.Name}' must be omitted when false.");
                    ValidateJsonShape(property.Value, property.Name);
                }
            }
        }

        private static ExternCatalog Load(UdonApiCatalogData data)
        {
            if (data == null || data.formatVersion != SupportedFormatVersion)
                throw new InvalidDataException($"Unsupported Udon API catalog format version '{data?.formatVersion}'.");
            if (data.target == null || string.IsNullOrEmpty(data.target.unityVersion) || string.IsNullOrEmpty(data.target.vrchatSdkVersion))
                throw new InvalidDataException("The catalog target is missing required version strings.");
            if (data.namespaces == null || data.namespaces.Count == 0)
                throw new InvalidDataException("The catalog must contain at least one namespace record.");

            var declarations = EnumerateTypes(data).ToList();
            if (data.namespaces != null && data.namespaces.Select(item => item?.name).Distinct(StringComparer.Ordinal).Count() != data.namespaces.Count)
                throw new InvalidDataException("The catalog contains duplicate namespaces.");
            if (data.unmodeledUdonSignatures != null && data.unmodeledUdonSignatures.Any(string.IsNullOrEmpty))
                throw new InvalidDataException("Unmodeled Udon signatures must be non-empty.");
            if (data.unmodeledUdonSignatures != null && data.unmodeledUdonSignatures.Distinct(StringComparer.Ordinal).Count() != data.unmodeledUdonSignatures.Count)
                throw new InvalidDataException("The catalog contains duplicate unmodeled Udon signatures.");
            UdonApiCatalogExposureReconciler.Reconcile(data);
            var types = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal);
            var declarationByIdentity = new Dictionary<string, TypeDeclaration>(StringComparer.Ordinal);
            var directInheritance = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var metadata = new Dictionary<TypeSymbol, ExternTypeMetadata>();
            var sourceTypes = new Dictionary<ExternSourceTypeKey, TypeSymbol>();
            var global = new NamespaceSymbol("<global>", "");

            // Pass 1: create all nominal declarations before resolving any TypeRef.
            foreach (var declaration in declarations)
            {
                var id = declaration.Identity;
                if (string.IsNullOrEmpty(id) || !declarationByIdentity.TryAdd(id, declaration))
                    throw new InvalidDataException($"The catalog contains a duplicate or invalid nominal type '{id}'.");
                ValidatePath(declaration.Path);
                var genericParameters = declaration.Record.genericParameters ?? new List<UdonApiGenericParameterRecord>();
                ValidateArity(declaration.Path, genericParameters.Count, id);
                var symbol = TryGetBuiltIn(id, out var builtIn)
                    ? builtIn
                    : TypeSymbol.CreateNamed(GetSimpleName(id), GetQualifiedName(id),
                        declaration.Kind == "class" || declaration.Kind == "interface",
                        RuntimeTypeIdentity.Named(id), isExternalBinding: true);
                if (symbol.IsGenericDefinition)
                    throw new InvalidDataException($"The catalog declares '{id}' more than once.");
                types.Add(id, symbol);
                AddToNamespace(global, declaration.Namespace, declaration.Path, symbol);
                var parameters = new TypeSymbol[genericParameters.Count];
                for (var ordinal = 0; ordinal < parameters.Length; ordinal++)
                {
                    ValidateGenericParameter(genericParameters[ordinal]);
                    parameters[ordinal] = TypeSymbol.CreateGenericParameter(genericParameters[ordinal].name, symbol, ordinal, id);
                }
                if (parameters.Length > 0) symbol.SetGenericParameters(parameters);
                var key = new ExternSourceTypeKey(ExternCatalog.GetSourceQualifiedName(id), parameters.Length);
                if (!sourceTypes.TryAdd(key, symbol))
                    throw new InvalidDataException($"The catalog contains duplicate source type '{key.QualifiedName}'.");
            }

            // Pass 2: resolve the graph and source-level members.
            foreach (var declaration in declarations)
            {
                var symbol = types[declaration.Identity];
                foreach (var genericParameter in declaration.Record.genericParameters ?? new List<UdonApiGenericParameterRecord>())
                    foreach (var constraint in genericParameter.constraints ?? new List<UdonApiTypeRef>())
                        ResolveType(constraint, types, symbol.GenericParameters, null, declaration.Identity);
                var supertypeRefs = GetDirectSupertypes(declaration.Record);
                var supertypes = new List<RuntimeTypeIdentity>();
                var directDefinitions = new List<string>();
                var directIdentities = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reference in supertypeRefs)
                {
                    var resolved = ResolveType(reference, types, symbol.GenericParameters, null, declaration.Identity);
                    var definitionIdentity = GetReferencedDefinitionIdentity(reference);
                    var resolvedIdentity = resolved.RuntimeTypeIdentity ?? RuntimeTypeIdentity.Named(definitionIdentity);
                    if (resolvedIdentity.Equals(symbol.RuntimeTypeIdentity))
                        throw new InvalidDataException($"Type '{declaration.Identity}' has invalid direct inheritance reference '{TypeKey(reference)}'.");
                    if (!declarationByIdentity.TryGetValue(definitionIdentity, out var targetDeclaration) ||
                        (reference == (declaration.Record as UdonApiClassRecord)?.baseType
                            ? targetDeclaration.Kind != "class"
                            : targetDeclaration.Kind != "interface") || !directIdentities.Add(TypeKey(reference)))
                        throw new InvalidDataException($"Type '{declaration.Identity}' has an invalid or duplicate direct inheritance reference.");
                    supertypes.Add(resolvedIdentity);
                    directDefinitions.Add(definitionIdentity);
                }
                var implicitBase = declaration.Kind == "struct" ? "System.ValueType" :
                    declaration.Kind == "enum" ? "System.Enum" : null;
                if (implicitBase != null && types.TryGetValue(implicitBase, out var baseSymbol))
                {
                    supertypes.Add(baseSymbol.RuntimeTypeIdentity ?? RuntimeTypeIdentity.Named(implicitBase));
                    directDefinitions.Add(implicitBase);
                }
                directInheritance.Add(declaration.Identity, directDefinitions);
                var isExposed = declaration.Exposure == "exposed";
                if (!isExposed && (declaration.Record.exposed != null || declaration.Record.unexposed != null))
                    throw new InvalidDataException($"Unexposed type '{declaration.Identity}' cannot contain a member catalog.");
                ExternEnumMetadata enumMetadata = null;
                if (declaration.Record is UdonApiEnumTypeRecord enumRecord)
                {
                    ValidateEnum(enumRecord, declaration.Identity);
                    var constants = new Dictionary<string, ExternEnumConstant>(StringComparer.Ordinal);
                    foreach (var constant in enumRecord.constants ?? new List<UdonApiEnumConstantRecord>())
                        constants.Add(constant.name, new ExternEnumConstant(constant.name, constant.value));
                    enumMetadata = new ExternEnumMetadata(ResolveType(enumRecord.underlyingType, types, null, null, declaration.Identity), constants);
                }
                metadata.Add(symbol, new ExternTypeMetadata(symbol.RuntimeTypeIdentity ?? RuntimeTypeIdentity.Named(declaration.Identity),
                    ParseShape(declaration.Kind, declaration.Identity), symbol.GenericParameters.Count(), supertypes, isExposed,
                    declaration.Kind == "struct" || declaration.Kind == "enum" ||
                        declaration.Record is UdonApiClassRecord classRecord && !classRecord.@abstract && classRecord.hasPublicParameterlessConstructor,
                    enumMetadata));
            }

            ValidateDirectInheritance(directInheritance);

            var memberGroups = new Dictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>>();
            var operatorGroups = new Dictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>>();
            foreach (var declaration in declarations.Where(item => item.Exposure == "exposed"))
            {
                var exposedMembers = ValidateMemberExposure(declaration.Record.exposed, true);
                var unexposedMembers = ValidateMemberExposure(declaration.Record.unexposed, false);
                if (exposedMembers.Overlaps(unexposedMembers))
                    throw new InvalidDataException($"Type '{declaration.Identity}' contains the same member in exposed and unexposed sections.");
                if (declaration.Record.exposed != null) ProjectExposure(declaration, declaration.Record.exposed, true, types, memberGroups, operatorGroups);
                if (declaration.Record.unexposed != null) ProjectExposure(declaration, declaration.Record.unexposed, false, types, memberGroups, operatorGroups);
            }

            var arrays = new Dictionary<RuntimeTypeIdentity, ArrayIntrinsicSymbols>();
            var modeledSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var groups in memberGroups.Values)
                foreach (var group in groups.Values)
                    foreach (var method in group.Methods.OfType<ExternMethodSymbol>()) modeledSignatures.Add(method.ExternSignature);
            foreach (var capability in data.capabilities?.arrays ?? new List<ArrayCapabilityRecord>())
            {
                ValidateArrayCapability(capability, types);
                var array = ResolveType(capability.type, types, null, null, "array capability");
                if (array.RuntimeTypeIdentity == null || arrays.ContainsKey(array.RuntimeTypeIdentity))
                    throw new InvalidDataException("The catalog contains a duplicate array capability.");
                var index = types["System.Int32"];
                arrays.Add(array.RuntimeTypeIdentity, new ArrayIntrinsicSymbols(capability.constructor, capability.getter, capability.setter, capability.length, index));
                modeledSignatures.Add(capability.constructor); modeledSignatures.Add(capability.getter);
                modeledSignatures.Add(capability.setter); modeledSignatures.Add(capability.length);
            }

            var sync = new Dictionary<RuntimeTypeIdentity, SynchronizationCapability>();
            foreach (var capability in data.capabilities?.synchronization ?? new List<SynchronizationCapabilityRecord>())
            {
                var type = ResolveType(capability.type, types, null, null, "synchronization capability");
                if (type.RuntimeTypeIdentity == null || sync.ContainsKey(type.RuntimeTypeIdentity))
                    throw new InvalidDataException("The catalog contains a duplicate synchronization capability.");
                var modes = capability.modes ?? throw new InvalidDataException("Synchronization modes are required.");
                if (modes.Count == 0 || modes.Distinct(StringComparer.Ordinal).Count() != modes.Count)
                    throw new InvalidDataException("Synchronization modes must be non-empty and unique.");
                var allowed = new HashSet<string>(modes, StringComparer.Ordinal);
                if (modes.Any(mode => mode != "none" && mode != "linear" && mode != "smooth"))
                    throw new InvalidDataException("The catalog contains an invalid synchronization mode.");
                sync.Add(type.RuntimeTypeIdentity, new SynchronizationCapability(allowed.Contains("none"), allowed.Contains("linear"), allowed.Contains("smooth")));
            }
            foreach (var signature in data.unmodeledUdonSignatures ?? new List<string>())
                if (modeledSignatures.Contains(signature)) throw new InvalidDataException("A modeled capability signature is also unmodeled.");

            // Constraint checks need transitive closure, but persisted edges stay direct.
            CloseSupertypes(metadata);
            var immutableMembers = memberGroups.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, MethodGroupSymbol>)new Dictionary<string, MethodGroupSymbol>(pair.Value, StringComparer.Ordinal));
            var immutableOperators = operatorGroups.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, MethodGroupSymbol>)new Dictionary<string, MethodGroupSymbol>(pair.Value, StringComparer.Ordinal));
            modeledSignatures.UnionWith(data.unmodeledUdonSignatures ?? new List<string>());
            return new ExternCatalog(global, types, sourceTypes, metadata, immutableMembers, immutableOperators, arrays, sync, modeledSignatures);
        }

        private static HashSet<MemberIdentity> ValidateMemberExposure(UdonApiMemberExposure exposure, bool exposed)
        {
            var identities = new HashSet<MemberIdentity>();
            if (exposure == null) return identities;
            void Add(MemberIdentity identity, string signature)
            {
                if (!identities.Add(identity)) throw new InvalidDataException("The catalog contains a duplicate member identity.");
                if (exposed ? string.IsNullOrEmpty(signature) : signature != null)
                    throw new InvalidDataException(exposed ? "An exposed member has no Udon signature." : "An unexposed member has an Udon signature.");
            }
            foreach (var constructor in exposure.constructors ?? new List<UdonApiConstructorRecord>())
            {
                ValidateParameters(constructor.parameters);
                Add(new MemberIdentity("constructor", null, null, 0, constructor.parameters, null), constructor.udonSignature);
            }
            ValidateSet(exposure.instance, "instance"); ValidateSet(exposure.@static, "static");
            foreach (var op in exposure.operators ?? new List<UdonApiOperatorRecord>())
            {
                if (string.IsNullOrEmpty(op.name) || op.parameters == null || op.parameters.Count == 0 || op.returnType == null) throw new InvalidDataException("The catalog contains an invalid operator.");
                ValidateParameters(op.parameters);
                Add(new MemberIdentity("operator", null, op.name, 0, op.parameters, op.returnType), op.udonSignature);
            }
            return identities;
            void ValidateSet(UdonApiMemberSet set, string scope)
            {
                if (set == null) return;
                foreach (var method in set.methods ?? new List<UdonApiMethodRecord>())
                {
                    if (string.IsNullOrEmpty(method.name) || method.returnType == null) throw new InvalidDataException("The catalog contains an invalid method.");
                    ValidateParameters(method.parameters);
                    Add(new MemberIdentity("method|" + scope, null, method.name, method.genericParameters?.Count ?? 0, method.parameters, null), method.udonSignature);
                }
                ValidateProperty(set.propertyGetters, "propertyGetter"); ValidateProperty(set.propertySetters, "propertySetter");
                ValidateField(set.fieldGetters, "fieldGetter"); ValidateField(set.fieldSetters, "fieldSetter");
                foreach (var evt in set.events ?? new List<UdonApiEventRecord>()) { if (string.IsNullOrEmpty(evt.name) || evt.type == null) throw new InvalidDataException("The catalog contains an invalid event."); Add(new MemberIdentity("event|" + scope, evt.type, evt.name, 0, null, null), null); }
                void ValidateProperty(List<UdonApiPropertyAccessorRecord> records, string accessorKind)
                {
                    foreach (var item in records ?? new List<UdonApiPropertyAccessorRecord>())
                    {
                        if (string.IsNullOrEmpty(item.name) || item.type == null) throw new InvalidDataException("The catalog contains an invalid property accessor.");
                        ValidateParameters(item.parameters);
                        Add(new MemberIdentity(accessorKind + "|" + scope, item.type, item.name, 0, item.parameters, null), item.udonSignature);
                    }
                }
                void ValidateField(List<UdonApiFieldAccessorRecord> records, string accessorKind)
                {
                    foreach (var item in records ?? new List<UdonApiFieldAccessorRecord>())
                    {
                        if (string.IsNullOrEmpty(item.name) || item.type == null) throw new InvalidDataException("The catalog contains an invalid field accessor.");
                        Add(new MemberIdentity(accessorKind + "|" + scope, item.type, item.name, 0, null, null), item.udonSignature);
                    }
                }
            }
        }

        private sealed class MemberIdentity : System.IEquatable<MemberIdentity>
        {
            private readonly string kind;
            private readonly UdonApiTypeRef memberType;
            private readonly string name;
            private readonly int genericArity;
            private readonly List<UdonApiParameterRecord> parameters;
            private readonly UdonApiTypeRef returnType;

            public MemberIdentity(string kind, UdonApiTypeRef memberType, string name, int genericArity,
                List<UdonApiParameterRecord> parameters, UdonApiTypeRef returnType)
            {
                this.kind = kind; this.memberType = memberType; this.name = name;
                this.genericArity = genericArity; this.parameters = parameters; this.returnType = returnType;
            }

            public bool Equals(MemberIdentity other)
            {
                if (other == null || kind != other.kind || name != other.name || genericArity != other.genericArity ||
                    !TypeRefsEqual(memberType, other.memberType) || !TypeRefsEqual(returnType, other.returnType)) return false;
                var count = parameters?.Count ?? 0;
                if (count != (other.parameters?.Count ?? 0)) return false;
                for (var index = 0; index < count; index++)
                    if (parameters[index].passing != other.parameters[index].passing || !TypeRefsEqual(parameters[index].type, other.parameters[index].type)) return false;
                return true;
            }

            public override bool Equals(object obj) => Equals(obj as MemberIdentity);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = System.StringComparer.Ordinal.GetHashCode(kind ?? string.Empty);
                    hash = hash * 397 ^ System.StringComparer.Ordinal.GetHashCode(name ?? string.Empty);
                    hash = hash * 397 ^ genericArity;
                    hash = hash * 397 ^ TypeRefHash(memberType);
                    hash = hash * 397 ^ TypeRefHash(returnType);
                    foreach (var parameter in parameters ?? new List<UdonApiParameterRecord>())
                    {
                        hash = hash * 397 ^ TypeRefHash(parameter.type);
                        hash = hash * 397 ^ System.StringComparer.Ordinal.GetHashCode(parameter.passing ?? string.Empty);
                    }
                    return hash;
                }
            }
        }

        private static bool TypeRefsEqual(UdonApiTypeRef left, UdonApiTypeRef right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.named != right.named || left.typeParameter != right.typeParameter ||
                left.methodParameter != right.methodParameter || left.generic != right.generic || (left.rank ?? 1) != (right.rank ?? 1) ||
                !TypeRefsEqual(left.array, right.array)) return false;
            var count = left.arguments?.Count ?? 0;
            if (count != (right.arguments?.Count ?? 0)) return false;
            for (var index = 0; index < count; index++)
                if (!TypeRefsEqual(left.arguments[index], right.arguments[index])) return false;
            return true;
        }

        private static int TypeRefHash(UdonApiTypeRef value)
        {
            if (value == null) return 0;
            unchecked
            {
                var hash = System.StringComparer.Ordinal.GetHashCode(value.named ?? value.generic ?? string.Empty);
                hash = hash * 397 ^ (value.typeParameter ?? -1);
                hash = hash * 397 ^ (value.methodParameter ?? -1);
                hash = hash * 397 ^ (value.rank ?? 1);
                hash = hash * 397 ^ TypeRefHash(value.array);
                foreach (var argument in value.arguments ?? new List<UdonApiTypeRef>()) hash = hash * 397 ^ TypeRefHash(argument);
                return hash;
            }
        }

        private static void ValidateParameters(List<UdonApiParameterRecord> parameters)
        {
            foreach (var parameter in parameters ?? new List<UdonApiParameterRecord>())
            {
                if (parameter == null || string.IsNullOrEmpty(parameter.name) || parameter.type == null || (parameter.passing != null && parameter.passing != "ref" && parameter.passing != "out" && parameter.passing != "in"))
                    throw new InvalidDataException("The catalog contains an invalid parameter.");
                if (parameter.@params && (parameter.passing != null || parameter.type.array == null))
                    throw new InvalidDataException("A params parameter must be an array and cannot have a passing mode.");
            }
        }

        private static string TypeKey(UdonApiTypeRef type) => type?.named ?? (type?.array != null ? "[]" + TypeKey(type.array) : type?.typeParameter.HasValue == true ? "!" + type.typeParameter : type?.methodParameter.HasValue == true ? "!!" + type.methodParameter : type?.generic + "<" + string.Join(",", (type?.arguments ?? new List<UdonApiTypeRef>()).Select(TypeKey)) + ">");

        private sealed class TypeDeclaration
        {
            public string Namespace; public List<string> Path; public string Kind; public string Exposure; public UdonApiTypeRecord Record;
            public string Identity => (string.IsNullOrEmpty(Namespace) ? "" : Namespace + ".") + string.Join("+", Path);
        }

        private static IEnumerable<TypeDeclaration> EnumerateTypes(UdonApiCatalogData data)
        {
            foreach (var ns in data.namespaces ?? new List<UdonApiNamespaceRecord>())
            {
                if (ns == null || ns.name == null || ns.name.StartsWith(".", StringComparison.Ordinal) || ns.name.EndsWith(".", StringComparison.Ordinal) || ns.name.Contains("..")) throw new InvalidDataException("The catalog contains an invalid namespace.");
                foreach (var exposure in new[] { "exposed", "unexposed" })
                {
                    var set = exposure == "exposed" ? ns.exposed : ns.unexposed;
                    if (set == null) continue;
                    foreach (var pair in new (string kind, IEnumerable<UdonApiTypeRecord> records)[]
                    {
                        ("class", set.classes), ("interface", set.interfaces), ("struct", set.structs), ("enum", set.enums)
                    })
                        foreach (var record in pair.records ?? Enumerable.Empty<UdonApiTypeRecord>())
                            yield return new TypeDeclaration { Namespace = ns.name, Path = record?.path, Kind = pair.kind, Exposure = exposure, Record = record };
                }
            }
        }

        private static void ProjectExposure(TypeDeclaration declaration, UdonApiMemberExposure exposure, bool isExposed,
            IReadOnlyDictionary<string, TypeSymbol> types, IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> members,
            IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> operators)
        {
            var host = types[declaration.Identity];
            foreach (var constructor in exposure.constructors ?? new List<UdonApiConstructorRecord>())
                AddCallable(host, "new", ExternMemberKind.Constructor, true, constructor.parameters, null, host, constructor.udonSignature, isExposed, types, members, null);
            ProjectSet(exposure.instance, false);
            ProjectSet(exposure.@static, true);
            foreach (var op in exposure.operators ?? new List<UdonApiOperatorRecord>())
                AddCallable(host, op.name, ExternMemberKind.Operator, true, op.parameters, op.returnType, null, op.udonSignature, isExposed, types, members, operators);

            void ProjectSet(UdonApiMemberSet set, bool isStatic)
            {
                if (set == null) return;
                foreach (var method in set.methods ?? new List<UdonApiMethodRecord>())
                    AddCallable(host, method.name, ExternMemberKind.Method, isStatic, method.parameters, method.returnType, null, method.udonSignature, isExposed, types, members, null, method.genericParameters);
                ProjectAccessors(set.propertyGetters, ExternMemberKind.Getter);
                ProjectAccessors(set.propertySetters, ExternMemberKind.Setter);
                ProjectFields(set.fieldGetters, ExternMemberKind.Getter);
                ProjectFields(set.fieldSetters, ExternMemberKind.Setter);
                foreach (var evt in set.events ?? new List<UdonApiEventRecord>())
                {
                    ResolveType(evt.type, types, host.GenericParameters, null, $"{host.RuntimeQualifiedName}.{evt.name}");
                    AddRejected(host, evt.name, "Event", isStatic, types, members);
                }
                void ProjectAccessors(List<UdonApiPropertyAccessorRecord> records, ExternMemberKind kind)
                {
                    foreach (var accessor in records ?? new List<UdonApiPropertyAccessorRecord>())
                    {
                        var parameters = accessor.parameters ?? new List<UdonApiParameterRecord>();
                        var source = new List<UdonApiParameterRecord>(parameters);
                        if (kind == ExternMemberKind.Setter) source.Add(new UdonApiParameterRecord { name = "value", type = accessor.type });
                        AddCallable(host, accessor.name, kind, isStatic, source, kind == ExternMemberKind.Getter ? accessor.type : TypeSymbol.Unit, null, accessor.udonSignature, isExposed, types, members, null, null, parameters.Count);
                    }
                }
                void ProjectFields(List<UdonApiFieldAccessorRecord> records, ExternMemberKind kind)
                {
                    foreach (var field in records ?? new List<UdonApiFieldAccessorRecord>())
                    {
                        var source = kind == ExternMemberKind.Setter ? new[] { new UdonApiParameterRecord { name = "value", type = field.type } } : Array.Empty<UdonApiParameterRecord>();
                        object returnType = kind == ExternMemberKind.Getter ? field.type : TypeSymbol.Unit;
                        AddCallable(host, field.name, kind, isStatic, source, returnType, null, field.udonSignature, isExposed, types, members, null);
                    }
                }
            }
        }

        private static void AddCallable(TypeSymbol host, string name, ExternMemberKind kind, bool isStatic,
            IReadOnlyList<UdonApiParameterRecord> sourceParameters, object returnTypeReference, TypeSymbol fixedReturn,
            string signature, bool isExposed, IReadOnlyDictionary<string, TypeSymbol> types,
            IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> members, IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> operators,
            IReadOnlyList<UdonApiGenericParameterRecord> genericDeclarations = null, int indexParameterCount = -1)
        {
            var methodName = kind == ExternMemberKind.Constructor ? "new" : name;
            var group = GetGroup(members, host, methodName);
            var generic = new TypeSymbol[genericDeclarations?.Count ?? 0];
            for (var i = 0; i < generic.Length; i++) generic[i] = TypeSymbol.CreateGenericParameter(genericDeclarations[i].name, host, i, $"{host.RuntimeQualifiedName}.{methodName}");
            foreach (var source in sourceParameters ?? Array.Empty<UdonApiParameterRecord>())
                ResolveType(source.type, types, host.GenericParameters, generic, $"{host.RuntimeQualifiedName}.{methodName}");
            if (returnTypeReference is UdonApiTypeRef sourceReturn)
                ResolveType(sourceReturn, types, host.GenericParameters, generic, $"{host.RuntimeQualifiedName}.{methodName}");
            foreach (var declaration in genericDeclarations ?? Array.Empty<UdonApiGenericParameterRecord>())
                foreach (var constraint in declaration.constraints ?? new List<UdonApiTypeRef>())
                    ResolveType(constraint, types, host.GenericParameters, generic, $"{host.RuntimeQualifiedName}.{methodName}");
            if (!isExposed)
            {
                group.AddRejectedCandidate(new ExternCandidate($"{host.RuntimeQualifiedName}.{methodName}", string.Empty, false, "The member is not exposed to Udon."));
                return;
            }
            var abi = new List<ExternParameterSymbol>();
            var logical = new List<ParameterSymbol>();
            if (!isStatic) logical.Add(new ParameterSymbol("self", host, 0));
            if (generic.Length > 0)
            {
                if (!types.TryGetValue("System.Type", out var runtimeTypeType)) throw new InvalidDataException("Generic methods require the System.Type declaration in the Catalog closure.");
                foreach (var parameter in generic) abi.Add(new ExternParameterSymbol(parameter.Name, runtimeTypeType, ExternParameterPassingMode.GenericTypeArgument, -1));
            }
            foreach (var source in sourceParameters ?? Array.Empty<UdonApiParameterRecord>())
            {
                var mode = source.passing switch { "ref" => ExternParameterPassingMode.Ref, "out" => ExternParameterPassingMode.Out, "in" => ExternParameterPassingMode.In, _ => ExternParameterPassingMode.Normal };
                var type = source.type == null ? null : ResolveType(source.type, types, host.GenericParameters, generic, $"{host.RuntimeQualifiedName}.{methodName}");
                var ordinal = -1;
                if (mode != ExternParameterPassingMode.Out) { ordinal = logical.Count; logical.Add(new ParameterSymbol(source.name, type, ordinal)); }
                abi.Add(new ExternParameterSymbol(source.name, type, mode, ordinal));
            }
            var result = fixedReturn ?? (returnTypeReference is TypeSymbol fixedType ? fixedType : returnTypeReference is UdonApiTypeRef resultRef ? ResolveType(resultRef, types, host.GenericParameters, generic, $"{host.RuntimeQualifiedName}.{methodName}") : TypeSymbol.Unit);
            var genericConstraints = new List<ExternGenericParameterConstraint>();
            for (var i = 0; i < generic.Length; i++)
            {
                var item = genericDeclarations[i];
                var constraints = (item.constraints ?? new List<UdonApiTypeRef>()).Select(reference => ResolveType(reference, types, host.GenericParameters, generic, methodName)).ToList();
                genericConstraints.Add(new ExternGenericParameterConstraint(generic[i], item.referenceType, item.nonNullableValueType, item.defaultConstructor, constraints));
            }
            var logicalReturn = BuildLogicalReturnType(result, abi);
            var method = new ExternMethodSymbol(methodName, host, logical, logicalReturn, null, signature, isStatic, kind, abi, result, generic, genericConstraints);
            group.AddMethod(method);
            if (kind == ExternMemberKind.Operator && operators != null && logical.Count > 0)
            {
                var operand = logical[0].Type;
                if (!operators.TryGetValue(operand, out var groups)) operators.Add(operand, groups = new Dictionary<string, MethodGroupSymbol>(StringComparer.Ordinal));
                if (!groups.TryGetValue(name, out var opGroup)) groups.Add(name, opGroup = new MethodGroupSymbol(name, operand));
                opGroup.AddMethod(method);
            }
        }

        private static void AddRejected(TypeSymbol host, string name, string kind, bool isStatic,
            IReadOnlyDictionary<string, TypeSymbol> types, IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> members) =>
            GetGroup(members, host, name).AddRejectedCandidate(new ExternCandidate($"{host.RuntimeQualifiedName}.{name}", string.Empty, false, "The member is not exposed to Udon."));

        private static MethodGroupSymbol GetGroup(IDictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> groups, TypeSymbol host, string name)
        {
            if (!groups.TryGetValue(host, out var methods)) groups.Add(host, methods = new Dictionary<string, MethodGroupSymbol>(StringComparer.Ordinal));
            if (!methods.TryGetValue(name, out var group)) methods.Add(name, group = new MethodGroupSymbol(name, host));
            return group;
        }

        private static TypeSymbol ResolveType(UdonApiTypeRef reference, IReadOnlyDictionary<string, TypeSymbol> types,
            IReadOnlyList<TypeSymbol> typeParameters, IReadOnlyList<TypeSymbol> methodParameters, string owner)
        {
            if (reference == null) throw new InvalidDataException($"'{owner}' has a missing TypeRef.");
            if (reference.named != null && types.TryGetValue(reference.named, out var named)) return named;
            if (reference.typeParameter.HasValue)
            {
                if (reference.typeParameter.Value < 0 || typeParameters == null || reference.typeParameter.Value >= typeParameters.Count)
                    throw new InvalidDataException($"'{owner}' has an invalid type generic parameter ordinal.");
                return typeParameters[reference.typeParameter.Value];
            }
            if (reference.methodParameter.HasValue)
            {
                if (reference.methodParameter.Value < 0 || methodParameters == null || reference.methodParameter.Value >= methodParameters.Count)
                    throw new InvalidDataException($"'{owner}' has an invalid method generic parameter ordinal.");
                return methodParameters[reference.methodParameter.Value];
            }
            if (reference.array != null) return TypeSymbol.Array(ResolveType(reference.array, types, typeParameters, methodParameters, owner));
            if (reference.generic != null && types.TryGetValue(reference.generic, out var definition))
            {
                var args = (reference.arguments ?? new List<UdonApiTypeRef>()).Select(item => ResolveType(item, types, typeParameters, methodParameters, owner)).ToArray();
                if (!definition.IsGenericDefinition || definition.GenericParameters.Count != args.Length) throw new InvalidDataException($"'{owner}' has a generic arity mismatch.");
                return definition.Construct(args);
            }
            throw new InvalidDataException($"'{owner}' references an unknown or invalid TypeRef.");
        }

        private static IEnumerable<UdonApiTypeRef> GetDirectSupertypes(UdonApiTypeRecord record)
        {
            if (record is UdonApiClassRecord cls && cls.baseType != null) yield return cls.baseType;
            IEnumerable<UdonApiTypeRef> interfaces = record switch { UdonApiClassRecord c => c.interfaces, UdonApiInterfaceRecord i => i.interfaces, UdonApiStructRecord s => s.interfaces, _ => null };
            foreach (var item in interfaces ?? Enumerable.Empty<UdonApiTypeRef>()) yield return item;
        }

        private static void ValidateDirectInheritance(IReadOnlyDictionary<string, List<string>> directInheritance)
        {
            var completed = new HashSet<string>(StringComparer.Ordinal);
            var active = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string identity)
            {
                if (completed.Contains(identity)) return;
                if (!active.Add(identity)) throw new InvalidDataException("The catalog contains an inheritance cycle.");
                foreach (var parent in directInheritance[identity]) Visit(parent);
                active.Remove(identity);
                completed.Add(identity);
            }
            foreach (var identity in directInheritance.Keys) Visit(identity);

            foreach (var item in directInheritance)
            {
                foreach (var directParent in item.Value)
                {
                    var inherited = new HashSet<string>(StringComparer.Ordinal);
                    var pending = new Stack<string>(directInheritance[directParent]);
                    while (pending.Count > 0)
                    {
                        var parent = pending.Pop();
                        if (!inherited.Add(parent)) continue;
                        foreach (var ancestor in directInheritance[parent]) pending.Push(ancestor);
                    }
                    if (item.Value.Any(parent => parent != directParent && inherited.Contains(parent)))
                        throw new InvalidDataException("A type redundantly declares a transitive base or interface.");
                }
            }
        }

        private static TypeSymbol BuildLogicalReturnType(TypeSymbol abiReturnType, IReadOnlyList<ExternParameterSymbol> parameters)
        {
            var outputs = new List<TypeSymbol>();
            if (abiReturnType != TypeSymbol.Unit) outputs.Add(abiReturnType);
            foreach (var parameter in parameters)
                if (parameter.PassingMode == ExternParameterPassingMode.Ref || parameter.PassingMode == ExternParameterPassingMode.Out)
                    outputs.Add(parameter.LogicalOutputType);
            return outputs.Count == 0 ? TypeSymbol.Unit : outputs.Count == 1 ? outputs[0] : TypeSymbol.Tuple(outputs);
        }

        private static string GetReferencedDefinitionIdentity(UdonApiTypeRef reference)
        {
            if (reference?.named != null) return reference.named;
            if (reference?.generic != null) return reference.generic;
            throw new InvalidDataException("Inheritance must reference a named type declaration.");
        }

        private static void CloseSupertypes(IDictionary<TypeSymbol, ExternTypeMetadata> metadata)
        {
            foreach (var item in metadata.ToArray())
            {
                var closure = new HashSet<RuntimeTypeIdentity>(item.Value.Supertypes);
                var queue = new Queue<RuntimeTypeIdentity>(closure);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var definition = current.Kind == RuntimeTypeIdentityKind.ConstructedGeneric
                        ? current.GenericDefinition : current;
                    var parent = metadata.Values.FirstOrDefault(candidate => candidate.Identity.Equals(definition));
                    if (parent == null) continue;
                    foreach (var super in parent.Supertypes) if (closure.Add(super)) queue.Enqueue(super);
                }
                metadata[item.Key] = new ExternTypeMetadata(item.Value.Identity, item.Value.Shape, item.Value.GenericArity, closure, item.Value.IsAbiAvailable, item.Value.SatisfiesDefaultConstructorConstraint, item.Value.Enum);
            }
        }

        private static void ValidateJsonPropertyNames(JObject obj, params string[] names)
        {
            var allowed = new HashSet<string>(names, StringComparer.Ordinal);
            if (obj.Properties().Any(property => !allowed.Contains(property.Name))) throw new InvalidDataException("The catalog contains an unknown property.");
        }

        private static void ValidatePath(IReadOnlyList<string> path)
        {
            if (path == null || path.Count == 0 || path.Any(segment => string.IsNullOrEmpty(segment) || segment.Contains(".") || segment.Contains("+"))) throw new InvalidDataException("The catalog contains an invalid type path.");
        }

        private static void ValidateArity(IReadOnlyList<string> path, int count, string identity)
        {
            var total = 0;
            foreach (var segment in path)
            {
                var tick = segment.LastIndexOf('`');
                if (tick < 0) continue;
                if (!int.TryParse(segment.Substring(tick + 1), out var arity) || arity <= 0) throw new InvalidDataException($"Type '{identity}' has an invalid metadata arity.");
                total += arity;
            }
            if (total != count) throw new InvalidDataException($"Type '{identity}' generic arity does not match its path.");
        }

        private static void ValidateGenericParameter(UdonApiGenericParameterRecord value)
        {
            if (value == null || string.IsNullOrEmpty(value.name) || (value.variance != null && value.variance != "in" && value.variance != "out")) throw new InvalidDataException("The catalog contains an invalid generic parameter.");
        }

        private static void ValidateEnum(UdonApiEnumTypeRecord value, string owner)
        {
            if (value.underlyingType?.named == null || !new[] { "System.SByte", "System.Byte", "System.Int16", "System.UInt16", "System.Int32", "System.UInt32", "System.Int64", "System.UInt64" }.Contains(value.underlyingType.named)) throw new InvalidDataException($"Enum '{owner}' has an invalid underlying type.");
            var constants = value.constants ?? new List<UdonApiEnumConstantRecord>();
            if (constants.Any(item => item == null || string.IsNullOrEmpty(item.name) || !IsValidEnumValue(item.value, value.underlyingType.named)) || constants.Select(item => item.name).Distinct(StringComparer.Ordinal).Count() != constants.Count) throw new InvalidDataException($"Enum '{owner}' has duplicate or invalid constants.");
        }

        private static bool IsValidEnumValue(string value, string underlyingType)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (!BigInteger.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) ||
                !string.Equals(value, parsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)) return false;
            var signed = underlyingType == "System.SByte" || underlyingType == "System.Int16" || underlyingType == "System.Int32" || underlyingType == "System.Int64";
            var bits = underlyingType == "System.SByte" || underlyingType == "System.Byte" ? 8 :
                underlyingType == "System.Int16" || underlyingType == "System.UInt16" ? 16 :
                underlyingType == "System.Int32" || underlyingType == "System.UInt32" ? 32 : 64;
            var minimum = signed ? -(BigInteger.One << (bits - 1)) : BigInteger.Zero;
            var maximum = signed ? (BigInteger.One << (bits - 1)) - 1 : (BigInteger.One << bits) - 1;
            return parsed >= minimum && parsed <= maximum;
        }

        private static void ValidateArrayCapability(ArrayCapabilityRecord value, IReadOnlyDictionary<string, TypeSymbol> types)
        {
            if (value?.type?.array == null || value.type.rank.HasValue || new[] { value.constructor, value.getter, value.setter, value.length }.Any(string.IsNullOrEmpty) || !types.ContainsKey("System.Int32")) throw new InvalidDataException("The catalog contains an invalid array capability.");
        }

        private static ExternTypeShape ParseShape(string kind, string identity) => identity == "System.Void" ? ExternTypeShape.Void : kind switch { "struct" => ExternTypeShape.Value, "enum" => ExternTypeShape.Enum, "class" or "interface" => ExternTypeShape.Reference, _ => throw new InvalidDataException("Invalid type kind.") };

        private static void AddToNamespace(NamespaceSymbol global, string ns, IReadOnlyList<string> path, TypeSymbol type)
        {
            var current = global;
            foreach (var segment in string.IsNullOrEmpty(ns) ? Array.Empty<string>() : ns.Split('.')) current = current.GetOrAddNamespace(segment);
            for (var index = 0; index < path.Count - 1; index++) current = current.GetOrAddNamespace(path[index]);
            current.AddType(type);
        }

        private static string GetQualifiedName(string runtimeName) => runtimeName.Replace('+', '.');
        private static string GetSimpleName(string runtimeName) { var name = GetQualifiedName(runtimeName); var dot = name.LastIndexOf('.'); return dot >= 0 ? name.Substring(dot + 1) : name; }
        private static bool TryGetBuiltIn(string name, out TypeSymbol symbol)
        {
            symbol = name switch { "System.Void" => TypeSymbol.Unit, "System.String" => TypeSymbol.String, "System.Boolean" => TypeSymbol.Bool, "System.Char" => TypeSymbol.Char, "System.SByte" => TypeSymbol.I8, "System.Byte" => TypeSymbol.U8, "System.Int16" => TypeSymbol.I16, "System.UInt16" => TypeSymbol.U16, "System.Int32" => TypeSymbol.I32, "System.UInt32" => TypeSymbol.U32, "System.Int64" => TypeSymbol.I64, "System.UInt64" => TypeSymbol.U64, "System.Single" => TypeSymbol.F32, "System.Double" => TypeSymbol.F64, "System.Object" => TypeSymbol.Object, _ => null };
            return symbol != null;
        }
    }

    public sealed class UdonApiTypeRefJsonConverter : JsonConverter<UdonApiTypeRef>
    {
        public override UdonApiTypeRef ReadJson(JsonReader reader, Type objectType, UdonApiTypeRef existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            if (token.Type == JTokenType.String) return UdonApiTypeRef.Named((string)token);
            if (token is not JObject obj) throw new JsonSerializationException("TypeRef must be a string or object.");
            ValidateObject(obj);
            var result = new UdonApiTypeRef();
            if (obj["array"] != null) { result.array = obj["array"].ToObject<UdonApiTypeRef>(serializer); result.rank = (int?)obj["rank"]; }
            else if (obj["typeParameter"] != null) result.typeParameter = (int)obj["typeParameter"];
            else if (obj["methodParameter"] != null) result.methodParameter = (int)obj["methodParameter"];
            else { result.generic = (string)obj["generic"]; result.arguments = obj["arguments"]?.ToObject<List<UdonApiTypeRef>>(serializer); }
            return result;
        }
        private static void ValidateObject(JObject obj)
        {
            var keys = obj.Properties().Select(property => property.Name).ToArray();
            var discriminatorCount = keys.Count(key => key == "array" || key == "typeParameter" || key == "methodParameter" || key == "generic");
            if (discriminatorCount != 1 || keys.Any(key => !new[] { "array", "rank", "typeParameter", "methodParameter", "generic", "arguments" }.Contains(key)) ||
                (obj["array"] == null && obj["rank"] != null) || (obj["generic"] == null && obj["arguments"] != null) ||
                ((int?)obj["rank"] == 1) || ((int?)obj["rank"] < 1) ||
                (obj["generic"] != null && (obj["arguments"] is not JArray args || args.Count == 0)))
                throw new JsonSerializationException("Invalid TypeRef union.");
        }
        public override void WriteJson(JsonWriter writer, UdonApiTypeRef value, JsonSerializer serializer)
        {
            if (value.named != null) { writer.WriteValue(value.named); return; }
            JObject obj;
            if (value.array != null) { obj = new JObject { ["array"] = JToken.FromObject(value.array, serializer) }; if (value.rank.HasValue) obj["rank"] = value.rank.Value; }
            else if (value.typeParameter.HasValue) obj = new JObject { ["typeParameter"] = value.typeParameter.Value };
            else if (value.methodParameter.HasValue) obj = new JObject { ["methodParameter"] = value.methodParameter.Value };
            else { obj = new JObject { ["generic"] = value.generic, ["arguments"] = JArray.FromObject(value.arguments, serializer) }; }
            obj.WriteTo(writer);
        }
    }
}
