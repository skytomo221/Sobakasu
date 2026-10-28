using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.UdonApi;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;
using UnityEditor;
using UnityEngine;
using StandardLibraryGeneratorTool = Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.StandardLibraryGenerator;

namespace Skytomo221.Sobakasu.Tools.UdonApiCatalog
{
    internal sealed class UdonApiCatalogGenerationResult
    {
        public UdonApiCatalogData Catalog { get; }
        public string Json { get; }
        public UdonApiCatalogGenerationResult(UdonApiCatalogData catalog, string json) { Catalog = catalog; Json = json; }
    }

    internal sealed class UdonApiCatalogGenerator
    {
        public const string OutputDirectoryName = "UdonApiCatalog~";
        public const string OutputFileName = "udon-api-catalog.json";
        public static string DefaultOutputPath => Path.Combine(StandardLibraryGeneratorTool.PackageRoot, OutputDirectoryName, OutputFileName);

        public UdonApiCatalogGenerationResult Generate()
        {
            var exposure = new InstalledUdonApiExposure(UdonExposedNodeCache.Default);
            var targets = CollectTargetTypes(AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic).SelectMany(GetLoadableTypes).Where(IsPublicType).ToArray(), exposure);
            return Build(targets, exposure);
        }

        internal UdonApiCatalogGenerationResult Generate(IReadOnlyList<Type> candidates, IUdonApiExposure exposure)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (exposure == null) throw new ArgumentNullException(nameof(exposure));
            return Build(CollectTargetTypes(candidates, exposure), exposure);
        }

        private static UdonApiCatalogGenerationResult Build(IReadOnlyList<Type> targets, IUdonApiExposure exposure)
        {
            var models = new UdonApiDiscovery(exposure).Discover(targets);
            var replacedObjectMembers = AddExposedSpecialObjectMethods(models, exposure);
            var catalog = new UdonApiCatalogData
            {
                target = new UdonApiCatalogTarget { unityVersion = Application.unityVersion, vrchatSdkVersion = GetVrchatSdkVersion() },
                namespaces = new List<UdonApiNamespaceRecord>(),
                capabilities = new UdonApiCapabilities { arrays = new List<ArrayCapabilityRecord>(), synchronization = new List<SynchronizationCapabilityRecord>() },
                unmodeledUdonSignatures = new List<string>()
            };
            var namespaces = new Dictionary<string, UdonApiNamespaceRecord>(StringComparer.Ordinal);
            var typeExposed = new HashSet<Type>(targets.Where(exposure.IsTypeExposed));
            foreach (var type in targets) AddType(type, typeExposed.Contains(type), namespaces);
            catalog.namespaces = namespaces.Values.ToList();

            var modeled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var model in models.Types)
            {
                var declaringType = NormalizeType(model.ClrType);
                if (!typeExposed.Contains(declaringType)) continue;
                foreach (var member in model.Members)
                {
                    if (replacedObjectMembers.Contains(member)) continue;
                    if (member.ClrDeclaringType != declaringType) continue; // declaration owner only
                    if (!CanRepresentMember(member))
                    {
                        Debug.LogWarning($"Skipped CLR member '{member.PhysicalFullName}' because its pointer type cannot be represented by Udon API Catalog v2 TypeRef.");
                        continue;
                    }
                    // Catalog exposure is a physical fact, independent of the
                    // Standard Library generation policy (params, storage, etc.).
                    var valid = member.IsUdonExposed;
                    var record = FindTypeRecord(namespaces, declaringType);
                    PlaceMember(record, member, valid);
                    if (valid && !string.IsNullOrEmpty(member.ExternSignature)) modeled.Add(member.ExternSignature);
                }
            }

            var arrays = CollectArrayTypes(models, targets);
            foreach (var array in arrays)
            {
                var capability = CreateArrayCapability(array, exposure);
                if (capability == null) continue;
                catalog.capabilities.arrays.Add(capability);
                modeled.Add(capability.constructor); modeled.Add(capability.getter); modeled.Add(capability.setter); modeled.Add(capability.length);
            }
            foreach (var type in targets.Concat(arrays).Where(CanQuerySynchronization).Distinct())
            {
                if (!VRC.Udon.UdonNetworkTypes.CanSync(type)) continue;
                var record = new SynchronizationCapabilityRecord { type = ToTypeRef(type), modes = new List<string> { "none" } };
                if (VRC.Udon.UdonNetworkTypes.CanSyncLinear(type)) record.modes.Add("linear");
                if (VRC.Udon.UdonNetworkTypes.CanSyncSmooth(type)) record.modes.Add("smooth");
                catalog.capabilities.synchronization.Add(record);
            }
            foreach (var signature in exposure.ExposedSignatures)
                if (!modeled.Contains(signature)) catalog.unmodeledUdonSignatures.Add(signature);

            NormalizeAndSort(catalog);
            return new UdonApiCatalogGenerationResult(catalog, Serialize(catalog));
        }

        private static HashSet<UdonApiMemberModel> AddExposedSpecialObjectMethods(UdonApiModel models, IUdonApiExposure exposure)
        {
            var replaced = new HashSet<UdonApiMemberModel>();
            var objectModel = models.Types.FirstOrDefault(item => item.ClrType == typeof(object));
            if (objectModel == null) return replaced;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var method in typeof(object).GetMethods(flags))
            {
                var signature = UdonExternSignatureFormatter.GetUdonMethodName(method);
                if (!exposure.IsMemberExposed(signature)) continue;
                var existing = objectModel.Members.FirstOrDefault(member => member.Member?.Equals(method) == true);
                if (existing != null && existing.IsUdonExposed && string.IsNullOrEmpty(existing.SkipReason)) continue;
                if (existing != null) replaced.Add(existing);
                objectModel.AddMember(new UdonApiMemberModel(typeof(object), method, method,
                    method.IsStatic ? UdonApiMemberKind.StaticMethod : UdonApiMemberKind.InstanceMethod,
                    signature, method.ToString(), true));
            }
            objectModel.SortMembers();
            return replaced;
        }

        public void GenerateToFile(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("An output path is required.", nameof(outputPath));
            var result = Generate();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            File.WriteAllText(outputPath, result.Json, new UTF8Encoding(false));
        }

        internal static string Serialize(UdonApiCatalogData catalog)
        {
            NormalizeAndSort(catalog);
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                Culture = CultureInfo.InvariantCulture,
                Converters = { new UdonApiTypeRefJsonConverter() }
            };
            return JsonConvert.SerializeObject(catalog, settings).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n') + "\n";
        }

        internal static UdonApiCatalogData Deserialize(string json) => JsonConvert.DeserializeObject<UdonApiCatalogData>(json, new JsonSerializerSettings { Converters = { new UdonApiTypeRefJsonConverter() } });

        private static void AddType(Type type, bool exposed, IDictionary<string, UdonApiNamespaceRecord> namespaces)
        {
            var ns = type.Namespace ?? string.Empty;
            if (!namespaces.TryGetValue(ns, out var record)) namespaces.Add(ns, record = new UdonApiNamespaceRecord { name = ns });
            var typeSet = exposed ? record.exposed ?? (record.exposed = new UdonApiTypeSet()) : record.unexposed ?? (record.unexposed = new UdonApiTypeSet());
            var path = GetPath(type);
            UdonApiTypeRecord typeRecord;
            if (type.IsEnum)
            {
                typeRecord = new UdonApiEnumTypeRecord { path = path, underlyingType = ToTypeRef(Enum.GetUnderlyingType(type)), constants = new List<UdonApiEnumConstantRecord>() };
                foreach (var name in Enum.GetNames(type)) ((UdonApiEnumTypeRecord)typeRecord).constants.Add(new UdonApiEnumConstantRecord { name = name, value = FormatEnumValue(Enum.Parse(type, name), Enum.GetUnderlyingType(type)) });
            }
            else if (type.IsInterface) typeRecord = new UdonApiInterfaceRecord { path = path };
            else if (type.IsValueType) typeRecord = new UdonApiStructRecord { path = path };
            else typeRecord = new UdonApiClassRecord { path = path, @abstract = type.IsAbstract, @sealed = type.IsSealed, hasPublicParameterlessConstructor = !type.IsAbstract && !type.ContainsGenericParameters && (type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null) };
            var generic = type.IsGenericTypeDefinition ? type.GetGenericArguments() : Type.EmptyTypes;
            if (generic.Length > 0) typeRecord.genericParameters = generic.Select(CreateGenericParameter).ToList();
            var directInterfaces = GetDirectInterfaces(type).Select(ToTypeRef).OrderBy(TypeRefKey, StringComparer.Ordinal).ToList();
            if (typeRecord is UdonApiClassRecord cls)
            {
                if (type.BaseType != null) cls.baseType = ToTypeRef(type.BaseType);
                if (directInterfaces.Count > 0) cls.interfaces = directInterfaces;
            }
            else if (typeRecord is UdonApiInterfaceRecord iface && directInterfaces.Count > 0) iface.interfaces = directInterfaces;
            else if (typeRecord is UdonApiStructRecord structure && directInterfaces.Count > 0) structure.interfaces = directInterfaces;
            if (typeRecord is UdonApiEnumTypeRecord enumRecord) (typeSet.enums ?? (typeSet.enums = new List<UdonApiEnumTypeRecord>())).Add(enumRecord);
            else if (typeRecord is UdonApiClassRecord classRecord) (typeSet.classes ?? (typeSet.classes = new List<UdonApiClassRecord>())).Add(classRecord);
            else if (typeRecord is UdonApiInterfaceRecord interfaceRecord) (typeSet.interfaces ?? (typeSet.interfaces = new List<UdonApiInterfaceRecord>())).Add(interfaceRecord);
            else (typeSet.structs ?? (typeSet.structs = new List<UdonApiStructRecord>())).Add((UdonApiStructRecord)typeRecord);
        }

        private static void PlaceMember(UdonApiTypeRecord type, UdonApiMemberModel member, bool exposed)
        {
            var target = exposed ? type.exposed ?? (type.exposed = new UdonApiMemberExposure()) : type.unexposed ?? (type.unexposed = new UdonApiMemberExposure());
            var signature = exposed ? member.ExternSignature : null;
            if (member.IsOperator)
            {
                var callable = member.Callable;
                var parameters = callable != null ? callable.GetParameters().Select(ToParameter).ToList() : member.OperatorParameterTypes.Select((parameter, index) => new UdonApiParameterRecord { name = $"arg{index}", type = ToTypeRef(parameter) }).ToList();
                (target.operators ?? (target.operators = new List<UdonApiOperatorRecord>())).Add(new UdonApiOperatorRecord { name = member.MemberName, parameters = parameters, returnType = ToTypeRef(callable is MethodInfo method ? method.ReturnType : member.OperatorReturnType), udonSignature = signature });
                return;
            }
            if (member.Kind == UdonApiMemberKind.Constructor)
            {
                (target.constructors ?? (target.constructors = new List<UdonApiConstructorRecord>())).Add(new UdonApiConstructorRecord { parameters = GetParameters(member.Callable), udonSignature = signature });
                return;
            }
            var isStatic = member.Callable?.IsStatic ?? (member.Member as FieldInfo)?.IsStatic ?? (member.Member as EventInfo)?.AddMethod?.IsStatic ?? member.Kind == UdonApiMemberKind.StaticMethod;
            var set = isStatic ? target.@static ?? (target.@static = new UdonApiMemberSet()) : target.instance ?? (target.instance = new UdonApiMemberSet());
            if (member.Kind == UdonApiMemberKind.Event && member.Member is EventInfo eventInfo)
            {
                var unexposed = type.unexposed ?? (type.unexposed = new UdonApiMemberExposure());
                var eventSet = isStatic ? unexposed.@static ?? (unexposed.@static = new UdonApiMemberSet()) : unexposed.instance ?? (unexposed.instance = new UdonApiMemberSet());
                (eventSet.events ?? (eventSet.events = new List<UdonApiEventRecord>())).Add(new UdonApiEventRecord { name = eventInfo.Name, type = ToTypeRef(eventInfo.EventHandlerType) });
                return;
            }
            if (member.Member is PropertyInfo property)
            {
                var accessor = new UdonApiPropertyAccessorRecord { name = property.Name, type = ToTypeRef(property.PropertyType), parameters = property.GetIndexParameters().Select(ToParameter).ToList(), udonSignature = signature };
                if (accessor.parameters.Count == 0) accessor.parameters = null;
                var list = member.Kind == UdonApiMemberKind.PropertyGetter ? set.propertyGetters ?? (set.propertyGetters = new List<UdonApiPropertyAccessorRecord>()) : set.propertySetters ?? (set.propertySetters = new List<UdonApiPropertyAccessorRecord>());
                list.Add(accessor); return;
            }
            if (member.Member is FieldInfo field)
            {
                var accessor = new UdonApiFieldAccessorRecord { name = field.Name, type = ToTypeRef(field.FieldType), udonSignature = signature };
                (member.Kind == UdonApiMemberKind.FieldGetter ? set.fieldGetters ?? (set.fieldGetters = new List<UdonApiFieldAccessorRecord>()) : set.fieldSetters ?? (set.fieldSetters = new List<UdonApiFieldAccessorRecord>())).Add(accessor); return;
            }
            if (member.Callable is MethodInfo methodInfo)
            {
                var method = new UdonApiMethodRecord { name = methodInfo.Name, parameters = GetParameters(methodInfo), returnType = ToTypeRef(methodInfo.ReturnType), udonSignature = signature };
                var generic = methodInfo.IsGenericMethodDefinition ? methodInfo.GetGenericArguments().Select(CreateGenericParameter).ToList() : null;
                if (generic?.Count > 0) method.genericParameters = generic;
                (set.methods ?? (set.methods = new List<UdonApiMethodRecord>())).Add(method);
            }
        }

        private static List<UdonApiParameterRecord> GetParameters(MethodBase method) => method == null ? null : method.GetParameters().Select(ToParameter).ToList();
        private static bool CanRepresentMember(UdonApiMemberModel member)
        {
            bool supported(Type type) => type == null || !type.IsPointer && (!type.HasElementType || supported(type.GetElementType()));
            if (member.Callable != null)
            {
                if (member.Callable is MethodInfo method && !supported(method.ReturnType)) return false;
                if (member.Callable.GetParameters().Any(parameter => !supported(parameter.ParameterType))) return false;
            }
            if (member.Member is FieldInfo field && !supported(field.FieldType)) return false;
            if (member.Member is PropertyInfo property && (!supported(property.PropertyType) || property.GetIndexParameters().Any(parameter => !supported(parameter.ParameterType)))) return false;
            if (member.Member is EventInfo eventInfo && !supported(eventInfo.EventHandlerType)) return false;
            if (member.IsSyntheticOperator && (member.OperatorParameterTypes.Any(parameter => !supported(parameter)) || !supported(member.OperatorReturnType))) return false;
            return true;
        }

        private static UdonApiParameterRecord ToParameter(ParameterInfo parameter)
        {
            var type = parameter.ParameterType;
            string passing = null;
            if (type.IsByRef) passing = parameter.IsOut ? "out" : parameter.IsIn ? "in" : "ref";
            var isParams = Attribute.IsDefined(parameter, typeof(ParamArrayAttribute));
            return new UdonApiParameterRecord { name = parameter.Name ?? string.Empty, type = ToTypeRef(type), passing = passing, @params = isParams };
        }

        private static UdonApiGenericParameterRecord CreateGenericParameter(Type parameter)
        {
            var attributes = parameter.GenericParameterAttributes;
            var record = new UdonApiGenericParameterRecord { name = parameter.Name };
            if ((attributes & GenericParameterAttributes.VarianceMask) == GenericParameterAttributes.Covariant) record.variance = "out";
            else if ((attributes & GenericParameterAttributes.VarianceMask) == GenericParameterAttributes.Contravariant) record.variance = "in";
            record.referenceType = (attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
            record.nonNullableValueType = (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;
            record.defaultConstructor = (attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0;
            var constraints = parameter.GetGenericParameterConstraints().Select(ToTypeRef).ToList();
            constraints.Sort(CompareTypeRefs);
            if (constraints.Count > 0) record.constraints = constraints;
            return record;
        }

        private static UdonApiTypeRef ToTypeRef(Type type)
        {
            if (type.IsByRef) type = type.GetElementType();
            if (type.IsArray) return new UdonApiTypeRef { array = ToTypeRef(type.GetElementType()), rank = type.GetArrayRank() == 1 ? (int?)null : type.GetArrayRank() };
            if (type.IsGenericParameter) return type.DeclaringMethod == null ? new UdonApiTypeRef { typeParameter = type.GenericParameterPosition } : new UdonApiTypeRef { methodParameter = type.GenericParameterPosition };
            if (type.IsGenericType && !type.IsGenericTypeDefinition) return new UdonApiTypeRef { generic = GetRuntimeName(type.GetGenericTypeDefinition()), arguments = type.GetGenericArguments().Select(ToTypeRef).ToList() };
            return UdonApiTypeRef.Named(GetRuntimeName(type));
        }

        private static UdonApiTypeRecord FindTypeRecord(IDictionary<string, UdonApiNamespaceRecord> namespaces, Type type)
        {
            var ns = namespaces[type.Namespace ?? string.Empty];
            foreach (var set in new[] { ns.exposed, ns.unexposed })
            {
                if (set == null) continue;
                var records = (IEnumerable<UdonApiTypeRecord>)(set.classes ?? new List<UdonApiClassRecord>()).Cast<UdonApiTypeRecord>()
                    .Concat((set.interfaces ?? new List<UdonApiInterfaceRecord>()).Cast<UdonApiTypeRecord>())
                    .Concat((set.structs ?? new List<UdonApiStructRecord>()).Cast<UdonApiTypeRecord>())
                    .Concat((set.enums ?? new List<UdonApiEnumTypeRecord>()).Cast<UdonApiTypeRecord>());
                var found = records.FirstOrDefault(record => record.path.SequenceEqual(GetPath(type)));
                if (found != null) return found;
            }
            throw new InvalidOperationException($"Missing type declaration for '{type}'.");
        }

        private static HashSet<Type> CollectArrayTypes(UdonApiModel model, IReadOnlyList<Type> targets)
        {
            var result = new HashSet<Type>();
            foreach (var type in targets)
                if (type != typeof(void) && !type.ContainsGenericParameters && !type.IsPointer)
                {
                    try { AddArray(type.MakeArrayType(), result); }
                    catch (TypeLoadException) { }
                }
            foreach (var item in model.Types)
                foreach (var member in item.Members)
                    if (member.Callable != null)
                    {
                        if (member.Callable is MethodInfo method) AddArray(method.ReturnType, result);
                        foreach (var parameter in member.Callable.GetParameters()) AddArray(parameter.ParameterType, result);
                    }
            return result;
        }

        private static void AddArray(Type type, ISet<Type> arrays) { if (type.IsByRef) type = type.GetElementType(); if (type.IsArray && type.GetArrayRank() == 1 && !type.ContainsGenericParameters) arrays.Add(type); }

        private static ArrayCapabilityRecord CreateArrayCapability(Type array, IUdonApiExposure exposure)
        {
            var name = UdonExternSignatureFormatter.GetUdonTypeName(array); var element = array.GetElementType(); var elementName = UdonExternSignatureFormatter.GetUdonTypeName(element);
            var constructor = $"{name}.__ctor__SystemInt32__{name}"; var getter = $"{name}.__Get__SystemInt32__{elementName}"; var setter = $"{name}.__Set__SystemInt32_{elementName}__SystemVoid"; var length = $"{name}.__get_Length__SystemInt32";
            if (!exposure.IsMemberExposed(getter) || !exposure.IsMemberExposed(setter))
            {
                getter = setter = null;
                if (typeof(UnityEngine.Object).IsAssignableFrom(element))
                {
                    var objectArray = UdonExternSignatureFormatter.GetUdonTypeName(typeof(object[])); var objectName = UdonExternSignatureFormatter.GetUdonTypeName(typeof(object));
                    var fallbackGetter = $"{objectArray}.__Get__SystemInt32__{objectName}"; var fallbackSetter = $"{objectArray}.__Set__SystemInt32_{objectName}__SystemVoid";
                    if (exposure.IsMemberExposed(fallbackGetter) && exposure.IsMemberExposed(fallbackSetter)) { getter = fallbackGetter; setter = fallbackSetter; }
                }
            }
            if (!exposure.IsMemberExposed(constructor) || !exposure.IsMemberExposed(length) || getter == null || setter == null) return null;
            return new ArrayCapabilityRecord { type = new UdonApiTypeRef { array = ToTypeRef(element) }, constructor = constructor, getter = getter, setter = setter, length = length };
        }

        private static IEnumerable<Type> GetDirectInterfaces(Type type)
        {
            var all = type.GetInterfaces().ToHashSet();
            if (!type.IsInterface && type.BaseType != null) all.ExceptWith(type.BaseType.GetInterfaces());
            foreach (var candidate in type.GetInterfaces())
                foreach (var inherited in candidate.GetInterfaces()) all.Remove(inherited);
            return all;
        }

        private static void NormalizeAndSort(UdonApiCatalogData data)
        {
            if (data.namespaces == null) return;
            data.namespaces.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var ns in data.namespaces)
                foreach (var set in new[] { ns.exposed, ns.unexposed })
                {
                    if (set == null) continue;
                    SortTypes(set.classes); SortTypes(set.interfaces); SortTypes(set.structs); SortTypes(set.enums);
                    foreach (var record in EnumerateRecords(set))
                    {
                        if (record.genericParameters?.Count == 0) record.genericParameters = null;
                        if (record is UdonApiEnumTypeRecord enumRecord)
                        {
                            if (enumRecord.constants?.Count == 0) enumRecord.constants = null;
                            enumRecord.constants?.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                        }
                        SortTypeRefs(record is UdonApiClassRecord cls ? cls.interfaces : record is UdonApiInterfaceRecord iface ? iface.interfaces : (record as UdonApiStructRecord)?.interfaces);
                        SortExposure(record.exposed); SortExposure(record.unexposed);
                    }
                    if (set.classes?.Count == 0) set.classes = null; if (set.interfaces?.Count == 0) set.interfaces = null; if (set.structs?.Count == 0) set.structs = null; if (set.enums?.Count == 0) set.enums = null;
                }
            data.namespaces.RemoveAll(ns => ns.exposed == null && ns.unexposed == null);
            if (data.namespaces.Count == 0) data.namespaces = null;
            if (data.capabilities != null)
            {
                if (data.capabilities.arrays?.Count == 0) data.capabilities.arrays = null;
                if (data.capabilities.synchronization?.Count == 0) data.capabilities.synchronization = null;
                data.capabilities.arrays?.Sort((a, b) => CompareTypeRefs(a.type, b.type));
                data.capabilities.synchronization?.Sort((a, b) => CompareTypeRefs(a.type, b.type));
                if (data.capabilities.synchronization != null) foreach (var sync in data.capabilities.synchronization) sync.modes.Sort((a, b) => SyncOrder(a).CompareTo(SyncOrder(b)));
                if (data.capabilities.arrays == null && data.capabilities.synchronization == null) data.capabilities = null;
            }
            if (data.unmodeledUdonSignatures?.Count == 0) data.unmodeledUdonSignatures = null;
            data.unmodeledUdonSignatures?.Sort(StringComparer.Ordinal);
        }

        private static IEnumerable<UdonApiTypeRecord> EnumerateRecords(UdonApiTypeSet set) => (set.classes ?? new List<UdonApiClassRecord>()).Cast<UdonApiTypeRecord>().Concat((set.interfaces ?? new List<UdonApiInterfaceRecord>()).Cast<UdonApiTypeRecord>()).Concat((set.structs ?? new List<UdonApiStructRecord>()).Cast<UdonApiTypeRecord>()).Concat((set.enums ?? new List<UdonApiEnumTypeRecord>()).Cast<UdonApiTypeRecord>());
        private static void SortTypes<T>(List<T> records) where T : UdonApiTypeRecord => records?.Sort((a, b) => ComparePath(a.path, b.path));
        private static int ComparePath(IReadOnlyList<string> a, IReadOnlyList<string> b) { for (var i = 0; i < Math.Min(a.Count, b.Count); i++) { var c = string.CompareOrdinal(a[i], b[i]); if (c != 0) return c; } return a.Count.CompareTo(b.Count); }
        private static void SortTypeRefs(List<UdonApiTypeRef> values) => values?.Sort(CompareTypeRefs);
        private static int CompareTypeRefs(UdonApiTypeRef left, UdonApiTypeRef right)
        {
            var leftKind = TypeRefKind(left); var rightKind = TypeRefKind(right);
            var result = leftKind.CompareTo(rightKind);
            if (result != 0) return result;
            switch (leftKind)
            {
                case 0: return string.CompareOrdinal(left.named, right.named);
                case 1:
                    result = CompareTypeRefs(left.array, right.array);
                    return result != 0 ? result : (left.rank ?? 1).CompareTo(right.rank ?? 1);
                case 2: return left.typeParameter.Value.CompareTo(right.typeParameter.Value);
                case 3: return left.methodParameter.Value.CompareTo(right.methodParameter.Value);
                default:
                    result = string.CompareOrdinal(left.generic, right.generic);
                    if (result != 0) return result;
                    return CompareTypeRefSequences(left.arguments, right.arguments);
            }
        }
        private static int TypeRefKind(UdonApiTypeRef value) => value.named != null ? 0 : value.array != null ? 1 : value.typeParameter.HasValue ? 2 : value.methodParameter.HasValue ? 3 : 4;
        private static int CompareTypeRefSequences(IReadOnlyList<UdonApiTypeRef> left, IReadOnlyList<UdonApiTypeRef> right)
        {
            var leftCount = left?.Count ?? 0; var rightCount = right?.Count ?? 0;
            for (var index = 0; index < Math.Min(leftCount, rightCount); index++)
            {
                var result = CompareTypeRefs(left[index], right[index]);
                if (result != 0) return result;
            }
            return leftCount.CompareTo(rightCount);
        }
        private static string TypeRefKey(UdonApiTypeRef type) => type == null ? "" : type.named != null ? "0|" + type.named : type.array != null ? "1|" + TypeRefKey(type.array) + "|" + (type.rank ?? 1) : type.typeParameter.HasValue ? "2|" + type.typeParameter : type.methodParameter.HasValue ? "3|" + type.methodParameter : "4|" + type.generic + "|" + string.Join(";", (type.arguments ?? new List<UdonApiTypeRef>()).Select(TypeRefKey));
        private static void SortExposure(UdonApiMemberExposure value)
        {
            if (value == null) return;
            if (value.constructors?.Count == 0) value.constructors = null;
            value.constructors?.ForEach(item => { if (item.parameters?.Count == 0) item.parameters = null; });
            value.constructors?.Sort((a, b) => CompareParametersAndSignature(a.parameters, a.udonSignature, b.parameters, b.udonSignature));
            SortSet(value.instance); SortSet(value.@static);
            if (value.operators?.Count == 0) value.operators = null;
            value.operators?.Sort((a, b) => { var result = string.CompareOrdinal(a.name, b.name); if (result == 0) result = CompareParameterTypes(a.parameters, b.parameters); if (result == 0) result = CompareParameterPassing(a.parameters, b.parameters); if (result == 0) result = CompareTypeRefs(a.returnType, b.returnType); return result != 0 ? result : string.CompareOrdinal(a.udonSignature, b.udonSignature); });
            if (IsEmpty(value.instance)) value.instance = null;
            if (IsEmpty(value.@static)) value.@static = null;
        }
        private static void SortSet(UdonApiMemberSet set)
        {
            if (set == null) return;
            if (set.methods?.Count == 0) set.methods = null; if (set.propertyGetters?.Count == 0) set.propertyGetters = null; if (set.propertySetters?.Count == 0) set.propertySetters = null; if (set.fieldGetters?.Count == 0) set.fieldGetters = null; if (set.fieldSetters?.Count == 0) set.fieldSetters = null; if (set.events?.Count == 0) set.events = null;
            if (set.methods != null) foreach (var method in set.methods) { if (method.parameters?.Count == 0) method.parameters = null; if (method.genericParameters?.Count == 0) method.genericParameters = null; }
            set.methods?.Sort((a, b) => { var result = string.CompareOrdinal(a.name, b.name); if (result == 0) result = (a.genericParameters?.Count ?? 0).CompareTo(b.genericParameters?.Count ?? 0); if (result == 0) result = CompareParameterTypes(a.parameters, b.parameters); if (result == 0) result = CompareParameterPassing(a.parameters, b.parameters); if (result == 0) result = CompareTypeRefs(a.returnType, b.returnType); return result != 0 ? result : string.CompareOrdinal(a.udonSignature, b.udonSignature); });
            SortProperties(set.propertyGetters); SortProperties(set.propertySetters);
            SortFields(set.fieldGetters); SortFields(set.fieldSetters);
            set.events?.Sort((a, b) => { var result = string.CompareOrdinal(a.name, b.name); return result != 0 ? result : CompareTypeRefs(a.type, b.type); });
        }
        private static bool IsEmpty(UdonApiMemberSet set) => set == null || set.methods == null && set.propertyGetters == null && set.propertySetters == null && set.fieldGetters == null && set.fieldSetters == null && set.events == null;
        private static void SortProperties(List<UdonApiPropertyAccessorRecord> records)
        {
            if (records == null) return;
            foreach (var record in records) if (record.parameters?.Count == 0) record.parameters = null;
            records.Sort((a, b) => { var result = string.CompareOrdinal(a.name, b.name); if (result == 0) result = CompareParameterTypes(a.parameters, b.parameters); if (result == 0) result = CompareTypeRefs(a.type, b.type); return result != 0 ? result : string.CompareOrdinal(a.udonSignature, b.udonSignature); });
        }
        private static void SortFields(List<UdonApiFieldAccessorRecord> records) => records?.Sort((a, b) => { var result = string.CompareOrdinal(a.name, b.name); if (result == 0) result = CompareTypeRefs(a.type, b.type); return result != 0 ? result : string.CompareOrdinal(a.udonSignature, b.udonSignature); });
        private static string ParametersKey(IEnumerable<UdonApiParameterRecord> parameters) => string.Join(";", (parameters ?? Enumerable.Empty<UdonApiParameterRecord>()).Select(item => TypeRefKey(item.type) + ":" + (item.passing ?? "")));
        private static int CompareParameterTypes(IReadOnlyList<UdonApiParameterRecord> left, IReadOnlyList<UdonApiParameterRecord> right)
        {
            var leftCount = left?.Count ?? 0; var rightCount = right?.Count ?? 0;
            for (var index = 0; index < Math.Min(leftCount, rightCount); index++) { var result = CompareTypeRefs(left[index].type, right[index].type); if (result != 0) return result; }
            return leftCount.CompareTo(rightCount);
        }
        private static int CompareParameterPassing(IReadOnlyList<UdonApiParameterRecord> left, IReadOnlyList<UdonApiParameterRecord> right)
        {
            var leftCount = left?.Count ?? 0; var rightCount = right?.Count ?? 0;
            for (var index = 0; index < Math.Min(leftCount, rightCount); index++) { var result = string.CompareOrdinal(left[index].passing ?? "", right[index].passing ?? ""); if (result != 0) return result; }
            return leftCount.CompareTo(rightCount);
        }
        private static int CompareParametersAndSignature(IReadOnlyList<UdonApiParameterRecord> left, string leftSignature, IReadOnlyList<UdonApiParameterRecord> right, string rightSignature)
        {
            var result = CompareParameterTypes(left, right);
            if (result == 0) result = CompareParameterPassing(left, right);
            return result != 0 ? result : string.CompareOrdinal(leftSignature, rightSignature);
        }
        private static int SyncOrder(string mode) => mode == "none" ? 0 : mode == "linear" ? 1 : 2;

        private static IReadOnlyList<Type> CollectTargetTypes(IReadOnlyList<Type> candidates, IUdonApiExposure exposure)
        {
            var pending = new Queue<Type>(candidates.Where(type => IsPublicType(type) && exposure.IsTypeExposed(type)));
            var roots = new HashSet<Type>(pending); foreach (var primitive in new[] { typeof(void), typeof(string), typeof(bool), typeof(char), typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(object), typeof(Type) }) { pending.Enqueue(primitive); roots.Add(primitive); }
            var seen = new HashSet<Type>();
            while (pending.Count > 0)
            {
                var raw = pending.Dequeue(); if (raw == null) continue;
                if (raw.IsByRef || raw.IsArray) { pending.Enqueue(raw.GetElementType()); continue; }
                if (raw.IsGenericType && !raw.IsGenericTypeDefinition)
                    foreach (var argument in raw.GetGenericArguments()) pending.Enqueue(argument);
                var type = raw.IsGenericType && !raw.IsGenericTypeDefinition ? raw.GetGenericTypeDefinition() : raw;
                if (type.IsPointer || type.IsGenericParameter || !seen.Add(type)) continue;
                if (type.DeclaringType != null) pending.Enqueue(type.DeclaringType);
                if (type.BaseType != null) pending.Enqueue(type.BaseType);
                foreach (var iface in GetDirectInterfaces(type)) pending.Enqueue(iface);
                foreach (var argument in type.GetGenericArguments()) { if (!argument.IsGenericParameter) pending.Enqueue(argument); foreach (var constraint in argument.GetGenericParameterConstraints()) pending.Enqueue(constraint); }
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (var method in type.GetMethods(flags)) { pending.Enqueue(method.ReturnType); foreach (var p in method.GetParameters()) pending.Enqueue(p.ParameterType); foreach (var gp in method.IsGenericMethodDefinition ? method.GetGenericArguments() : Type.EmptyTypes) foreach (var c in gp.GetGenericParameterConstraints()) pending.Enqueue(c); }
                foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) foreach (var p in constructor.GetParameters()) pending.Enqueue(p.ParameterType);
                foreach (var property in type.GetProperties(flags))
                {
                    pending.Enqueue(property.PropertyType);
                    foreach (var parameter in property.GetIndexParameters()) pending.Enqueue(parameter.ParameterType);
                }
                foreach (var field in type.GetFields(flags)) pending.Enqueue(field.FieldType);
                foreach (var evt in type.GetEvents(flags)) pending.Enqueue(evt.EventHandlerType);
            }
            return seen.OrderBy(GetRuntimeName, StringComparer.Ordinal).ToArray();
        }

        private static List<string> GetPath(Type type) { var stack = new Stack<string>(); for (var current = type; current != null; current = current.DeclaringType) stack.Push(current.Name); return stack.ToList(); }
        private static string GetRuntimeName(Type type) => type.FullName ?? type.Name;
        private static Type NormalizeType(Type type) { while (type.IsByRef || type.IsArray) type = type.GetElementType(); return type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type; }
        private static bool IsPublicType(Type type) => type != null && (type.IsPublic || type.IsNestedPublic);
        private static bool CanQuerySynchronization(Type type) => type != null && type != typeof(void) && !type.IsByRef && !type.IsPointer && !type.ContainsGenericParameters;
        private static string FormatEnumValue(object value, Type underlying) => Type.GetTypeCode(underlying) is TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 ? Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        private static string GetVrchatSdkVersion() { foreach (var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()) if (package.name == "com.vrchat.worlds") return package.version; return string.Empty; }
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) { try { return assembly.GetTypes(); } catch (ReflectionTypeLoadException exception) { return exception.Types.Where(type => type != null); } }
    }
}
