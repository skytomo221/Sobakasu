using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skytomo221.Sobakasu.Tools.UdonApiCatalog
{
    // These records are the catalog's persisted contract.  Keep them free of Unity,
    // VRChat, and reflection types so the compiler can consume them in Phase 2.
    [Serializable]
    internal sealed class UdonApiCatalogData
    {
        [JsonProperty(Order = 1)] public int formatVersion = 1;
        [JsonProperty(Order = 2)] public UdonApiCatalogTarget target = new();
        [JsonProperty(Order = 3)] public List<UdonApiTypeRecord> types = new();
        [JsonProperty(Order = 4)] public List<string> unexposedClrTypeNames = new();
        [JsonProperty(Order = 5)] public List<UdonApiMemberRecord> members = new();
        [JsonProperty(Order = 6)] public List<UdonApiUnexposedMemberRecord> unexposedMembers = new();
        [JsonProperty(Order = 7)] public UdonApiCapabilities capabilities = new();
        [JsonProperty(Order = 8)] public List<string> unmatchedUdonSignatures = new();
    }

    [Serializable]
    internal sealed class UdonApiCatalogTarget
    {
        [JsonProperty(Order = 1)] public string unityVersion;
        [JsonProperty(Order = 2)] public string vrchatSdkVersion;
    }

    [Serializable]
    internal sealed class ExternTypeRef
    {
        [JsonProperty(Order = 1)] public string kind;
        [JsonProperty(Order = 2, NullValueHandling = NullValueHandling.Ignore)] public string runtimeName;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public ExternTypeRef element;
        [JsonProperty(Order = 4, NullValueHandling = NullValueHandling.Ignore)] public string scope;
        [JsonProperty(Order = 5, DefaultValueHandling = DefaultValueHandling.Ignore)] public int ordinal;
        [JsonProperty(Order = 6, NullValueHandling = NullValueHandling.Ignore)] public ExternTypeRef definition;
        [JsonProperty(Order = 7, NullValueHandling = NullValueHandling.Ignore)] public List<ExternTypeRef> arguments;
    }

    [Serializable]
    internal sealed class UdonApiTypeRecord
    {
        [JsonProperty(Order = 1)] public string runtimeName;
        [JsonProperty(Order = 2)] public string shape;
        [JsonProperty(Order = 3)] public int genericArity;
        [JsonProperty(Order = 4)] public List<ExternTypeRef> supertypes = new();
        [JsonProperty(Order = 5)] public bool satisfiesDefaultConstructorConstraint;
        [JsonProperty(Order = 6, NullValueHandling = NullValueHandling.Ignore)] public UdonApiEnumRecord @enum;
    }

    [Serializable]
    internal sealed class UdonApiEnumRecord
    {
        [JsonProperty(Order = 1)] public ExternTypeRef underlyingType;
        [JsonProperty(Order = 2)] public List<UdonApiEnumConstantRecord> constants = new();
    }

    [Serializable]
    internal sealed class UdonApiEnumConstantRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2)] public string value;
    }

    [Serializable]
    internal sealed class UdonApiMemberRecord
    {
        [JsonProperty(Order = 1)] public ExternTypeRef hostType;
        [JsonProperty(Order = 2)] public ExternTypeRef clrDeclaringType;
        [JsonProperty(Order = 3)] public string name;
        [JsonProperty(Order = 4)] public string kind;
        [JsonProperty(Order = 5)] public string origin;
        [JsonProperty(Order = 6)] public bool isStatic;
        [JsonProperty(Order = 7)] public string clrSignature;
        [JsonProperty(Order = 8, NullValueHandling = NullValueHandling.Ignore)] public string externSignature;
        [JsonProperty(Order = 9)] public List<UdonApiGenericParameterRecord> genericParameters = new();
        [JsonProperty(Order = 10)] public List<ExternParameterRecord> abiParameters = new();
        [JsonProperty(Order = 11, NullValueHandling = NullValueHandling.Ignore)] public ExternTypeRef abiReturnType;
    }

    [Serializable]
    internal sealed class UdonApiUnexposedMemberRecord
    {
        [JsonProperty(Order = 1)] public string hostType;
        [JsonProperty(Order = 2)] public string name;
        [JsonProperty(Order = 3)] public string kind;
        [JsonProperty(Order = 4)] public string externSignature;
    }

    [Serializable]
    internal sealed class UdonApiGenericParameterRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2)] public bool referenceTypeConstraint;
        [JsonProperty(Order = 3)] public bool nonNullableValueTypeConstraint;
        [JsonProperty(Order = 4)] public bool defaultConstructorConstraint;
        [JsonProperty(Order = 5)] public List<ExternTypeRef> typeConstraints = new();
    }

    [Serializable]
    internal sealed class ExternParameterRecord
    {
        [JsonProperty(Order = 1)] public string name;
        [JsonProperty(Order = 2)] public ExternTypeRef type;
        [JsonProperty(Order = 3)] public string passingMode;
    }

    [Serializable]
    internal sealed class UdonApiCapabilities
    {
        [JsonProperty(Order = 1)] public List<ArrayCapabilityRecord> arrays = new();
    }

    [Serializable]
    internal sealed class ArrayCapabilityRecord
    {
        [JsonProperty(Order = 1)] public ExternTypeRef arrayType;
        [JsonProperty(Order = 2)] public ExternTypeRef indexType;
        [JsonProperty(Order = 3, NullValueHandling = NullValueHandling.Ignore)] public string constructorSignature;
        [JsonProperty(Order = 4, NullValueHandling = NullValueHandling.Ignore)] public string getterSignature;
        [JsonProperty(Order = 5, NullValueHandling = NullValueHandling.Ignore)] public string setterSignature;
        [JsonProperty(Order = 6, NullValueHandling = NullValueHandling.Ignore)] public string lengthSignature;
    }
}
