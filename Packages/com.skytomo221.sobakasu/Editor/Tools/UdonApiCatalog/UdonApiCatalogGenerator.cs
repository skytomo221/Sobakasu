using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;
using Skytomo221.Sobakasu.Tools.UdonApi;
using UnityEditor;
using UnityEngine;
using StandardLibraryGeneratorTool = Skytomo221.Sobakasu.Tools.StandardLibraryGenerator.StandardLibraryGenerator;

namespace Skytomo221.Sobakasu.Tools.UdonApiCatalog
{
    internal sealed class UdonApiCatalogGenerationResult
    {
        public UdonApiCatalogData Catalog { get; }
        public string Json { get; }

        public UdonApiCatalogGenerationResult(UdonApiCatalogData catalog, string json)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Json = json ?? throw new ArgumentNullException(nameof(json));
        }
    }

    internal sealed class UdonApiCatalogGenerator
    {
        public const string OutputDirectoryName = "UdonApiCatalog~";
        public const string OutputFileName = "udon-api-catalog.json";

        public static string DefaultOutputPath => Path.Combine(
            StandardLibraryGeneratorTool.PackageRoot,
            OutputDirectoryName,
            OutputFileName);

        public UdonApiCatalogGenerationResult Generate()
        {
            var exposure = new InstalledUdonApiExposure(UdonExposedNodeCache.Default);
            var targetTypes = CollectTargetTypes(exposure);
            var discovery = new UdonApiDiscovery(exposure);
            var model = discovery.Discover(targetTypes);
            var catalog = Project(model, targetTypes, exposure);
            return new UdonApiCatalogGenerationResult(catalog, Serialize(catalog));
        }

        internal UdonApiCatalogGenerationResult Generate(
            IReadOnlyList<Type> candidateTypes,
            IUdonApiExposure exposure)
        {
            if (candidateTypes == null)
                throw new ArgumentNullException(nameof(candidateTypes));
            if (exposure == null)
                throw new ArgumentNullException(nameof(exposure));

            var targetTypes = CollectTargetTypes(candidateTypes, exposure);
            // Explicit candidates are exposed API roots. Related base,
            // interface, signature and constraint types belong to the metadata
            // closure and must not become exposed surfaces merely by reachability.
            var model = new UdonApiDiscovery(exposure)
                .Discover(candidateTypes);
            var catalog = Project(model, targetTypes, exposure);
            return new UdonApiCatalogGenerationResult(catalog, Serialize(catalog));
        }

        public void GenerateToFile(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("An output path is required.", nameof(outputPath));

            var result = Generate();
            var fullPath = Path.GetFullPath(outputPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException("The output path has no parent directory.");
            Directory.CreateDirectory(parent);
            File.WriteAllText(fullPath, result.Json, new UTF8Encoding(false));
        }

        internal static string Serialize(UdonApiCatalogData catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            Sort(catalog);
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                Culture = CultureInfo.InvariantCulture
            };
            var json = JsonConvert.SerializeObject(catalog, settings);
            return json.Replace("\r\n", "\n").Replace('\r', '\n') + "\n";
        }

        internal static UdonApiCatalogData Deserialize(string json)
        {
            var catalog = JsonConvert.DeserializeObject<UdonApiCatalogData>(json);
            if (catalog == null)
                throw new InvalidDataException("The Udon API catalog is empty.");
            return catalog;
        }

        private static List<Type> CollectTargetTypes(IUdonApiExposure exposure)
        {
            var allTypes = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;
                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (IsPublicType(type))
                        allTypes.Add(type);
                }
            }
            return CollectTargetTypes(allTypes, exposure);
        }

        private static List<Type> CollectTargetTypes(
            IReadOnlyList<Type> candidateTypes,
            IUdonApiExposure exposure)
        {
            var pending = new Queue<Type>();
            var seen = new HashSet<Type>();
            foreach (var type in candidateTypes)
            {
                if (IsPublicType(type) && exposure.IsTypeExposed(type))
                    pending.Enqueue(type);
            }

            // Canonical primitive declarations are required even when a particular
            // Udon node set happens not to mention every primitive in its surface.
            foreach (var primitive in CanonicalPrimitiveTypes)
                pending.Enqueue(primitive);

            while (pending.Count > 0)
            {
                var pendingType = pending.Dequeue();
                EnqueueConstructedGenericArguments(pendingType, pending);
                var type = NormalizeType(pendingType);
                if (type == null || !seen.Add(type))
                    continue;
                EnqueueRelatedTypes(type, pending);
            }

            var targetTypes = new HashSet<Type>(seen);
            foreach (var primitive in CanonicalPrimitiveTypes)
                targetTypes.Add(primitive);

            var result = new List<Type>(targetTypes);
            result.Sort(CompareTypes);
            return result;
        }

        private static readonly Type[] CanonicalPrimitiveTypes =
        {
            typeof(void), typeof(string), typeof(bool), typeof(char),
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(object), typeof(Type)
        };

        private static void EnqueueRelatedTypes(Type type, Queue<Type> pending)
        {
            if (type.BaseType != null)
                pending.Enqueue(type.BaseType);
            foreach (var interfaceType in type.GetInterfaces())
                pending.Enqueue(interfaceType);
            foreach (var argument in type.GetGenericArguments())
            {
                pending.Enqueue(argument);
                foreach (var constraint in argument.GetGenericParameterConstraints())
                    pending.Enqueue(constraint);
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.FlattenHierarchy;
            foreach (var method in type.GetMethods(flags))
                EnqueueCallableTypes(method, pending);
            foreach (var constructor in type.GetConstructors(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                EnqueueCallableTypes(constructor, pending);
            foreach (var property in type.GetProperties(flags))
                pending.Enqueue(property.PropertyType);
            foreach (var field in type.GetFields(flags))
                pending.Enqueue(field.FieldType);
        }

        private static void EnqueueCallableTypes(MethodBase callable, Queue<Type> pending)
        {
            if (callable is MethodInfo method)
                pending.Enqueue(method.ReturnType);
            foreach (var parameter in callable.GetParameters())
                pending.Enqueue(parameter.ParameterType);
            if (callable is MethodInfo genericMethod && genericMethod.IsGenericMethodDefinition)
            {
                foreach (var genericParameter in genericMethod.GetGenericArguments())
                {
                    foreach (var constraint in
                             genericParameter.GetGenericParameterConstraints())
                    {
                        pending.Enqueue(constraint);
                    }
                }
            }
        }

        private static void EnqueueConstructedGenericArguments(Type type, Queue<Type> pending)
        {
            while (type != null && (type.IsByRef || type.IsArray))
                type = type.GetElementType();
            if (type == null || !type.IsGenericType || type.IsGenericTypeDefinition)
                return;
            foreach (var argument in type.GetGenericArguments())
                pending.Enqueue(argument);
        }

        private static UdonApiCatalogData Project(
            UdonApiModel model,
            IReadOnlyList<Type> targetTypes,
            IUdonApiExposure exposure)
        {
            var catalog = new UdonApiCatalogData
            {
                target = new UdonApiCatalogTarget
                {
                    unityVersion = Application.unityVersion,
                    vrchatSdkVersion = GetVrchatSdkVersion()
                }
            };
            var abiAvailableTypes = CollectAbiAvailableTypes(model);
            foreach (var type in targetTypes)
            {
                catalog.types.Add(CreateTypeRecord(type));
                if (!abiAvailableTypes.Contains(type))
                    catalog.unexposedClrTypeNames.Add(GetRuntimeName(type));
            }

            var matchedSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in model.Types)
            {
                foreach (var member in type.Members)
                {
                    if (member.IsUdonExposed)
                    {
                        catalog.members.Add(CreateMemberRecord(member));
                    }
                    else
                    {
                        catalog.unexposedMembers.Add(CreateUnexposedMemberRecord(member));
                    }

                    if (member.IsUdonExposed && !string.IsNullOrEmpty(member.ExternSignature))
                        matchedSignatures.Add(member.ExternSignature);
                }
            }
            foreach (var signature in exposure.ExposedSignatures)
            {
                if (!matchedSignatures.Contains(signature))
                    catalog.unmatchedUdonSignatures.Add(signature);
            }
            AddArrayCapabilities(catalog, model, exposure);
            Sort(catalog);
            return catalog;
        }

        private static HashSet<Type> CollectAbiAvailableTypes(
            UdonApiModel model)
        {
            var result = new HashSet<Type>();

            foreach (var type in model.Types)
            {
                AddAbiAvailableType(type.ClrType, result);
                foreach (var member in type.Members)
                {
                    if (!member.IsUdonExposed)
                        continue;

                    if (member.Callable is MethodInfo method)
                        AddAbiAvailableType(method.ReturnType, result);
                    if (member.Callable != null)
                    {
                        foreach (var parameter in member.Callable.GetParameters())
                            AddAbiAvailableType(parameter.ParameterType, result);
                        if (member.Callable is MethodInfo genericMethod &&
                            genericMethod.IsGenericMethodDefinition)
                        {
                            AddAbiAvailableType(typeof(Type), result);
                            foreach (var genericParameter in
                                     genericMethod.GetGenericArguments())
                            {
                                foreach (var constraint in
                                         genericParameter.GetGenericParameterConstraints())
                                {
                                    AddAbiAvailableType(constraint, result);
                                }
                            }
                        }
                    }

                    if (member.IsSyntheticOperator)
                    {
                        foreach (var parameterType in member.OperatorParameterTypes)
                            AddAbiAvailableType(parameterType, result);
                        AddAbiAvailableType(member.OperatorReturnType, result);
                    }
                    else if (member.Member is FieldInfo field)
                    {
                        AddAbiAvailableType(field.FieldType, result);
                    }
                }
            }

            return result;
        }

        private static void AddAbiAvailableType(
            Type type,
            ISet<Type> result)
        {
            if (type == null || type.IsGenericParameter)
                return;
            while (type.IsByRef)
                type = type.GetElementType();
            if (type == null)
                return;
            if (type.IsArray)
            {
                AddAbiAvailableType(type.GetElementType(), result);
                return;
            }
            if (type.IsGenericType)
            {
                var definition = type.IsGenericTypeDefinition
                    ? type
                    : type.GetGenericTypeDefinition();
                result.Add(definition);
                if (!type.IsGenericTypeDefinition)
                {
                    foreach (var argument in type.GetGenericArguments())
                        AddAbiAvailableType(argument, result);
                }
                return;
            }
            result.Add(type);
        }

        private static UdonApiTypeRecord CreateTypeRecord(Type type)
        {
            return new UdonApiTypeRecord
            {
                runtimeName = GetRuntimeName(type),
                shape = GetShape(type),
                genericArity = type.IsGenericTypeDefinition
                    ? type.GetGenericArguments().Length
                    : 0,
                supertypes = GetSupertypes(type),
                satisfiesDefaultConstructorConstraint = SatisfiesDefaultConstructorConstraint(type),
                @enum = CreateEnumRecord(type)
            };
        }

        private static UdonApiMemberRecord CreateMemberRecord(UdonApiMemberModel member)
        {
            var callable = member.Callable;
            var record = new UdonApiMemberRecord
            {
                hostType = CreateTypeRef(member.SurfaceType),
                clrDeclaringType = CreateTypeRef(member.ClrDeclaringType),
                name = member.MemberName,
                kind = GetMemberKind(member),
                origin = member.IsSyntheticOperator ? "SyntheticUdon" : "Clr",
                isStatic = callable?.IsStatic ??
                    (member.Member as FieldInfo)?.IsStatic ??
                    (member.Member as EventInfo)?.AddMethod?.IsStatic ??
                    member.Kind == UdonApiMemberKind.StaticMethod,
                clrSignature = ClrMemberId.Format(member),
                externSignature = string.IsNullOrEmpty(member.ExternSignature)
                    ? null
                    : member.ExternSignature,
                abiReturnType = GetAbiReturnType(member)
            };
            if (callable != null)
            {
                AddGenericParameters(record.genericParameters, callable);
                AddAbiParameters(record.abiParameters, callable);
            }
            else if (member.IsSyntheticOperator)
            {
                for (var index = 0; index < member.OperatorParameterTypes.Count; index++)
                {
                    record.abiParameters.Add(new ExternParameterRecord
                    {
                        name = $"arg{index}",
                        type = CreateTypeRef(member.OperatorParameterTypes[index]),
                        passingMode = "Normal"
                    });
                }
            }
            else if (member.Member is FieldInfo field &&
                     member.Kind == UdonApiMemberKind.FieldSetter)
            {
                record.abiParameters.Add(new ExternParameterRecord
                {
                    name = "value",
                    type = CreateTypeRef(field.FieldType),
                    passingMode = "Normal"
                });
            }
            return record;
        }

        private static UdonApiUnexposedMemberRecord CreateUnexposedMemberRecord(
            UdonApiMemberModel member)
        {
            return new UdonApiUnexposedMemberRecord
            {
                hostType = GetRuntimeName(member.SurfaceType),
                name = member.MemberName,
                kind = member.Kind.ToString(),
                externSignature = member.ExternSignature
            };
        }

        private static void AddGenericParameters(
            List<UdonApiGenericParameterRecord> records,
            MethodBase callable)
        {
            if (!(callable is MethodInfo method) || !method.IsGenericMethodDefinition)
                return;
            foreach (var parameter in method.GetGenericArguments())
            {
                var attributes = parameter.GenericParameterAttributes;
                var constraints = new List<ExternTypeRef>();
                foreach (var constraint in parameter.GetGenericParameterConstraints())
                    constraints.Add(CreateTypeRef(constraint));
                records.Add(new UdonApiGenericParameterRecord
                {
                    name = parameter.Name,
                    referenceTypeConstraint = (attributes &
                        GenericParameterAttributes.ReferenceTypeConstraint) != 0,
                    nonNullableValueTypeConstraint = (attributes &
                        GenericParameterAttributes.NotNullableValueTypeConstraint) != 0,
                    defaultConstructorConstraint = (attributes &
                        GenericParameterAttributes.DefaultConstructorConstraint) != 0,
                    typeConstraints = constraints
                });
            }
        }

        private static void AddAbiParameters(
            List<ExternParameterRecord> records,
            MethodBase callable)
        {
            if (callable is MethodInfo method && method.IsGenericMethodDefinition)
            {
                foreach (var genericParameter in method.GetGenericArguments())
                {
                    records.Add(new ExternParameterRecord
                    {
                        name = genericParameter.Name,
                        type = CreateTypeRef(typeof(Type)),
                        passingMode = "GenericTypeArgument"
                    });
                }
            }
            foreach (var parameter in callable.GetParameters())
            {
                records.Add(new ExternParameterRecord
                {
                    name = parameter.Name ?? $"arg{records.Count}",
                    type = CreateTypeRef(parameter.ParameterType),
                    passingMode = GetPassingMode(parameter)
                });
            }
        }

        private static ExternTypeRef GetAbiReturnType(UdonApiMemberModel member)
        {
            if (member.IsSyntheticOperator)
                return CreateTypeRef(member.OperatorReturnType);
            if (member.Callable is ConstructorInfo)
                return CreateTypeRef(member.SurfaceType);
            if (member.Callable is MethodInfo method)
                return CreateTypeRef(method.ReturnType);
            if (member.Member is FieldInfo field)
                return member.Kind == UdonApiMemberKind.FieldSetter
                    ? CreateTypeRef(typeof(void))
                    : CreateTypeRef(field.FieldType);
            return null;
        }

        private static void AddArrayCapabilities(
            UdonApiCatalogData catalog,
            UdonApiModel model,
            IUdonApiExposure exposure)
        {
            var arrays = new HashSet<Type>();
            foreach (var type in model.Types)
            {
                foreach (var member in type.Members)
                {
                    if (!member.IsUdonExposed)
                        continue;
                    if (member.Callable is MethodInfo method)
                        AddArrayType(method.ReturnType, arrays);
                    if (member.Callable != null)
                    {
                        foreach (var parameter in member.Callable.GetParameters())
                            AddArrayType(parameter.ParameterType, arrays);
                    }
                    if (member.Member is FieldInfo field)
                        AddArrayType(field.FieldType, arrays);
                }
            }
            // Array ABI is a target capability in its own right. Seed canonical
            // scalar arrays even when no discovered SDK member happens to mention
            // a particular array type (notably System.Boolean[]).
            foreach (var primitive in CanonicalPrimitiveTypes)
            {
                if (primitive != typeof(void))
                    arrays.Add(primitive.MakeArrayType());
            }

            foreach (var array in arrays)
            {
                var capability = CreateArrayCapability(array, exposure);
                if (capability != null)
                    catalog.capabilities.arrays.Add(capability);
            }
        }

        private static void AddArrayType(Type type, ISet<Type> arrays)
        {
            if (type != null && type.IsByRef)
                type = type.GetElementType();
            if (type?.IsArray == true && type.GetArrayRank() == 1 && !type.ContainsGenericParameters)
                arrays.Add(type);
        }

        private static ArrayCapabilityRecord CreateArrayCapability(
            Type array,
            IUdonApiExposure exposure)
        {
            var arrayName = UdonExternSignatureFormatter.GetUdonTypeName(array);
            var element = array.GetElementType();
            var elementName = UdonExternSignatureFormatter.GetUdonTypeName(element);
            var constructor = $"{arrayName}.__ctor__SystemInt32__{arrayName}";
            var getter = $"{arrayName}.__Get__SystemInt32__{elementName}";
            var setter = $"{arrayName}.__Set__SystemInt32_{elementName}__SystemVoid";
            var length = $"{arrayName}.__get_Length__SystemInt32";
            if (!exposure.IsMemberExposed(constructor)) constructor = null;
            if (!exposure.IsMemberExposed(length)) length = null;
            if (!exposure.IsMemberExposed(getter) || !exposure.IsMemberExposed(setter))
            {
                getter = null;
                setter = null;
                if (typeof(UnityEngine.Object).IsAssignableFrom(element))
                {
                    var objectArrayName = UdonExternSignatureFormatter.GetUdonTypeName(typeof(object[]));
                    var objectName = UdonExternSignatureFormatter.GetUdonTypeName(typeof(object));
                    var objectGetter = $"{objectArrayName}.__Get__SystemInt32__{objectName}";
                    var objectSetter = $"{objectArrayName}.__Set__SystemInt32_{objectName}__SystemVoid";
                    if (exposure.IsMemberExposed(objectGetter) && exposure.IsMemberExposed(objectSetter))
                    {
                        getter = objectGetter;
                        setter = objectSetter;
                    }
                }
            }
            if (string.IsNullOrEmpty(constructor) ||
                string.IsNullOrEmpty(getter) ||
                string.IsNullOrEmpty(setter) ||
                string.IsNullOrEmpty(length))
                return null;
            return new ArrayCapabilityRecord
            {
                arrayType = CreateTypeRef(array),
                indexType = CreateTypeRef(typeof(int)),
                constructorSignature = constructor,
                getterSignature = getter,
                setterSignature = setter,
                lengthSignature = length
            };
        }

        private static ExternTypeRef CreateTypeRef(Type type)
        {
            if (type == null)
                return null;
            if (type.IsByRef)
                type = type.GetElementType();
            if (type.IsArray)
                return new ExternTypeRef { kind = "Array", element = CreateTypeRef(type.GetElementType()) };
            if (type.IsGenericParameter)
            {
                return new ExternTypeRef
                {
                    kind = "GenericParameter",
                    scope = type.DeclaringMethod == null ? "Type" : "Method",
                    ordinal = type.GenericParameterPosition
                };
            }
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var arguments = new List<ExternTypeRef>();
                foreach (var argument in type.GetGenericArguments())
                    arguments.Add(CreateTypeRef(argument));
                return new ExternTypeRef
                {
                    kind = "ConstructedGeneric",
                    definition = CreateTypeRef(type.GetGenericTypeDefinition()),
                    arguments = arguments
                };
            }
            return new ExternTypeRef { kind = "Named", runtimeName = GetRuntimeName(type) };
        }

        private static List<ExternTypeRef> GetSupertypes(Type type)
        {
            var types = new HashSet<Type>();
            for (var current = type.BaseType; current != null; current = current.BaseType)
                types.Add(current);
            foreach (var interfaceType in type.GetInterfaces())
                types.Add(interfaceType);
            var result = new List<ExternTypeRef>();
            foreach (var supertype in types)
            {
                if (!supertype.ContainsGenericParameters)
                    result.Add(CreateTypeRef(supertype));
            }
            result.Sort(CompareTypeRefs);
            return result;
        }

        private static UdonApiEnumRecord CreateEnumRecord(Type type)
        {
            if (!type.IsEnum)
                return null;
            var record = new UdonApiEnumRecord { underlyingType = CreateTypeRef(Enum.GetUnderlyingType(type)) };
            foreach (var name in Enum.GetNames(type))
            {
                var value = Enum.Parse(type, name);
                record.constants.Add(new UdonApiEnumConstantRecord
                {
                    name = name,
                    value = FormatEnumValue(value, Enum.GetUnderlyingType(type))
                });
            }
            record.constants.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return record;
        }

        private static string FormatEnumValue(object value, Type underlyingType)
        {
            switch (Type.GetTypeCode(underlyingType))
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture);
                default:
                    return Convert.ToUInt64(value, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture);
            }
        }

        private static bool SatisfiesDefaultConstructorConstraint(Type type)
        {
            if (type.IsAbstract || type.ContainsGenericParameters)
                return false;
            if (type.IsValueType)
                return Nullable.GetUnderlyingType(type) == null;
            return type.GetConstructor(Type.EmptyTypes) != null;
        }

        private static string GetShape(Type type)
        {
            if (type == typeof(void)) return "Void";
            if (type.IsEnum) return "Enum";
            return type.IsValueType ? "Value" : "Reference";
        }

        private static string GetMemberKind(UdonApiMemberModel member)
        {
            if (member.IsOperator) return "Operator";
            return member.Kind switch
            {
                UdonApiMemberKind.Constructor => "Constructor",
                UdonApiMemberKind.PropertyGetter => "Getter",
                UdonApiMemberKind.FieldGetter => "Getter",
                UdonApiMemberKind.PropertySetter => "Setter",
                UdonApiMemberKind.FieldSetter => "Setter",
                UdonApiMemberKind.Event => "Event",
                _ => "Method"
            };
        }

        private static string GetPassingMode(ParameterInfo parameter)
        {
            if (parameter.IsOut) return "Out";
            if (!parameter.ParameterType.IsByRef) return "Normal";
            return parameter.IsIn ? "In" : "Ref";
        }

        private static string GetRuntimeName(Type type)
        {
            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        private static Type NormalizeType(Type type)
        {
            while (type != null && (type.IsByRef || type.IsArray))
                type = type.GetElementType();
            if (type?.IsPointer == true)
                return null;
            if (type != null && type.IsGenericType && !type.IsGenericTypeDefinition)
                type = type.GetGenericTypeDefinition();
            return type?.IsGenericParameter == true ? null : type;
        }

        private static bool IsPublicType(Type type)
        {
            return type != null && (type.IsPublic || type.IsNestedPublic);
        }

        private static int CompareTypes(Type left, Type right)
        {
            return string.CompareOrdinal(GetRuntimeName(left), GetRuntimeName(right));
        }

        private static int CompareTypeRefs(ExternTypeRef left, ExternTypeRef right)
        {
            return string.CompareOrdinal(TypeRefSortKey(left), TypeRefSortKey(right));
        }

        private static string TypeRefSortKey(ExternTypeRef type)
        {
            if (type == null) return string.Empty;
            return type.kind + "|" + type.runtimeName + "|" + TypeRefSortKey(type.element) +
                "|" + TypeRefSortKey(type.definition) + "|" + type.ordinal;
        }

        private static string GetVrchatSdkVersion()
        {
            foreach (var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (package.name == "com.vrchat.worlds")
                    return package.version;
            }
            return string.Empty;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { return exception.Types; }
        }

        private static void Sort(UdonApiCatalogData catalog)
        {
            catalog.types.Sort((left, right) => string.CompareOrdinal(left.runtimeName, right.runtimeName));
            catalog.unexposedClrTypeNames.Sort(StringComparer.Ordinal);
            catalog.members.Sort((left, right) => string.CompareOrdinal(
                MemberSortKey(left), MemberSortKey(right)));
            catalog.unexposedMembers.Sort((left, right) => string.CompareOrdinal(
                UnexposedMemberSortKey(left), UnexposedMemberSortKey(right)));
            catalog.capabilities.arrays.Sort((left, right) => CompareTypeRefs(left.arrayType, right.arrayType));
            catalog.unmatchedUdonSignatures.Sort(StringComparer.Ordinal);
            foreach (var type in catalog.types)
                type.supertypes.Sort(CompareTypeRefs);
            foreach (var member in catalog.members)
            {
                member.genericParameters.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
                foreach (var parameter in member.genericParameters)
                    parameter.typeConstraints.Sort(CompareTypeRefs);
            }
        }

        private static string MemberSortKey(UdonApiMemberRecord member)
        {
            return TypeRefSortKey(member.hostType) + "|" + member.name + "|" + member.kind +
                "|" + member.externSignature + "|" + member.clrSignature;
        }

        private static string UnexposedMemberSortKey(UdonApiUnexposedMemberRecord member)
        {
            return member.hostType + "|" + member.name + "|" + member.kind +
                "|" + member.externSignature;
        }
    }

}
