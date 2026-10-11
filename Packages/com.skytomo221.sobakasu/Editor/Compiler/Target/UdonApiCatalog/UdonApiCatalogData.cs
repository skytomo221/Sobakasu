using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog
{
    // Persisted v2 DTOs intentionally describe CLR source metadata. Compiler ABI
    // operands are projected by UdonApiCatalogLoader and never serialized here.
    [Serializable]
    public sealed class UdonApiCatalogData
    {
        [JsonProperty(Order = 1)] public int formatVersion = 2;
        [JsonProperty(Order = 2)] public UdonApiCatalogTarget target = new();
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiNamespaceRecord> namespaces;
        [JsonProperty(Order = 4, NullValueHandling = NullValueHandling.Ignore)] public UdonApiCapabilities capabilities;
        [JsonProperty(Order = 5, NullValueHandling = NullValueHandling.Ignore)] public List<string> unmodeledUdonSignatures;
    }

    [Serializable]
    public sealed class UdonApiCatalogTarget
    {
        [JsonProperty(Order = 1)] public string unityVersion;
        [JsonProperty(Order = 2)] public string vrchatSdkVersion;
    }

    [Serializable]
    public sealed class UdonApiNamespaceRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public UdonApiTypeSet exposed;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public UdonApiTypeSet unexposed;
    }

    [Serializable]
    public sealed class UdonApiTypeSet
    {
        [JsonProperty(Order = 1, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiClassRecord> classes;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiInterfaceRecord> interfaces;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiStructRecord> structs;
        [JsonProperty(Order = 4, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiEnumTypeRecord> enums;
    }

    [Serializable]
    public abstract class UdonApiTypeRecord
    {
        [JsonProperty(Order = 1)] public List<string> path;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiGenericParameterRecord> genericParameters;
        [JsonProperty(Order = 100, NullValueHandling = NullValueHandling.Ignore)] public UdonApiMemberExposure exposed;
        [JsonProperty(Order = 101, NullValueHandling = NullValueHandling.Ignore)] public UdonApiMemberExposure unexposed;
    }

    [Serializable]
    public sealed class UdonApiClassRecord : UdonApiTypeRecord
    {
        [JsonProperty(Order = 3, DefaultValueHandling = DefaultValueHandling.Ignore)] public bool @abstract;
        [JsonProperty("sealed", Order = 4, DefaultValueHandling = DefaultValueHandling.Ignore)] public bool @sealed;
        [JsonProperty(Order = 5, DefaultValueHandling = DefaultValueHandling.Ignore)] public bool hasPublicParameterlessConstructor;
        [JsonProperty(Order = 6, NullValueHandling = NullValueHandling.Ignore)] public UdonApiTypeRef baseType;
        [JsonProperty(Order = 7, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiTypeRef> interfaces;
    }

    [Serializable]
    public sealed class UdonApiInterfaceRecord : UdonApiTypeRecord
    {
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiTypeRef> interfaces;
    }

    [Serializable]
    public sealed class UdonApiStructRecord : UdonApiTypeRecord
    {
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiTypeRef> interfaces;
    }

    [Serializable]
    public sealed class UdonApiEnumTypeRecord : UdonApiTypeRecord
    {
        [JsonProperty(Order = 2)] public UdonApiTypeRef underlyingType;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiEnumConstantRecord> constants;
    }

    [Serializable]
    public sealed class UdonApiEnumConstantRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2)] public string value;
    }

    [Serializable]
    public sealed class UdonApiTypeRef
    {
        [JsonProperty(Order = 1, NullValueHandling = NullValueHandling.Ignore)] public string named;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public UdonApiTypeRef array;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public int? rank;
        [JsonProperty(Order = 4, NullValueHandling = NullValueHandling.Ignore)] public int? typeParameter;
        [JsonProperty(Order = 5, NullValueHandling = NullValueHandling.Ignore)] public int? methodParameter;
        [JsonProperty(Order = 6, NullValueHandling = NullValueHandling.Ignore)] public string generic;
        [JsonProperty(Order = 7, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiTypeRef> arguments;

        // A named TypeRef is represented directly by a JSON string, not an object.
        public static UdonApiTypeRef Named(string name) => new() { named = name };
    }

    [Serializable]
    public sealed class UdonApiGenericParameterRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public string variance;
        [JsonProperty(Order = 3, DefaultValueHandling = DefaultValueHandling.Ignore)] public bool referenceType;
        [JsonProperty(Order = 4, DefaultValueHandling = DefaultValueHandling.Ignore)] public bool nonNullableValueType;
        [JsonProperty(Order = 5, DefaultValueHandling = DefaultValueHandling.Ignore)] public bool defaultConstructor;
        [JsonProperty(Order = 6, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiTypeRef> constraints;
    }

    [Serializable]
    public sealed class UdonApiParameterRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2)] public UdonApiTypeRef type;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public string passing;
        [JsonProperty(Order = 4, DefaultValueHandling = DefaultValueHandling.Ignore)] public bool @params;
    }

    [Serializable]
    public sealed class UdonApiMemberExposure
    {
        [JsonProperty(Order = 1, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiConstructorRecord> constructors;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public UdonApiMemberSet instance;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public UdonApiMemberSet @static;
        [JsonProperty(Order = 4, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiOperatorRecord> operators;
    }

    [Serializable]
    public sealed class UdonApiMemberSet
    {
        [JsonProperty(Order = 1, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiMethodRecord> methods;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiPropertyAccessorRecord> propertyGetters;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiPropertyAccessorRecord> propertySetters;
        [JsonProperty(Order = 4, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiFieldAccessorRecord> fieldGetters;
        [JsonProperty(Order = 5, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiFieldAccessorRecord> fieldSetters;
        [JsonProperty(Order = 6, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiEventRecord> events;
    }

    [Serializable]
    public sealed class UdonApiConstructorRecord
    {
        [JsonProperty(Order = 1, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiParameterRecord> parameters;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public string udonSignature;
    }

    [Serializable]
    public sealed class UdonApiMethodRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiGenericParameterRecord> genericParameters;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiParameterRecord> parameters;
        [JsonProperty(Order = 4)] public UdonApiTypeRef returnType;
        [JsonProperty(Order = 5, NullValueHandling = NullValueHandling.Ignore)] public string udonSignature;
    }

    [Serializable]
    public sealed class UdonApiPropertyAccessorRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2)] public UdonApiTypeRef type;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public List<UdonApiParameterRecord> parameters;
        [JsonProperty(Order = 4, NullValueHandling = NullValueHandling.Ignore)] public string udonSignature;
    }

    [Serializable]
    public sealed class UdonApiFieldAccessorRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2)] public UdonApiTypeRef type;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public string udonSignature;
    }

    [Serializable]
    public sealed class UdonApiEventRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2)] public UdonApiTypeRef type;
    }

    [Serializable]
    public sealed class UdonApiOperatorRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2)] public List<UdonApiParameterRecord> parameters;
        [JsonProperty(Order = 3)] public UdonApiTypeRef returnType;
        [JsonProperty(Order = 4, NullValueHandling = NullValueHandling.Ignore)] public string udonSignature;
    }

    [Serializable]
    public sealed class UdonApiCapabilities
    {
        [JsonProperty(Order = 1, NullValueHandling = NullValueHandling.Ignore)] public List<ArrayCapabilityRecord> arrays;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public List<SynchronizationCapabilityRecord> synchronization;
    }

    [Serializable]
    public sealed class ArrayCapabilityRecord
    {
        [JsonProperty(Order = 1)] public UdonApiTypeRef type;
        [JsonProperty(Order = 2)] public string constructor;
        [JsonProperty(Order = 3)] public string getter;
        [JsonProperty(Order = 4)] public string setter;
        [JsonProperty(Order = 5)] public string length;
    }

    [Serializable]
    public sealed class SynchronizationCapabilityRecord
    {
        [JsonProperty(Order = 1)] public UdonApiTypeRef type;
        [JsonProperty(Order = 2)] public List<string> modes;
    }
}
