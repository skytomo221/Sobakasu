using System;
using System.Collections.Generic;
using System.Reflection;
using VRC.Udon.Editor;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class ExternCandidate
  {
    public MethodInfo MethodInfo { get; }
    public string ExternSignature { get; }
    public bool IsCallable { get; }
    public string RejectionReason { get; }
    public string DisplayName =>
        $"{MethodInfo.DeclaringType?.FullName}.{MethodInfo.Name}";

    public ExternCandidate(
        MethodInfo methodInfo,
        string externSignature,
        bool isCallable,
        string rejectionReason)
    {
      MethodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
      ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature));
      IsCallable = isCallable;
      RejectionReason = rejectionReason ?? string.Empty;
    }
  }

  internal sealed class ExternCatalog
  {
    private readonly IReadOnlyDictionary<Type, TypeSymbol> _typeSymbolsByClrType;
    private readonly IReadOnlyDictionary<string, TypeSymbol> _typesByQualifiedName;

    public NamespaceSymbol GlobalNamespace { get; }

    public ExternCatalog(
        NamespaceSymbol globalNamespace,
        IReadOnlyDictionary<Type, TypeSymbol> typeSymbolsByClrType,
        IReadOnlyDictionary<string, TypeSymbol> typesByQualifiedName)
    {
      GlobalNamespace = globalNamespace ?? throw new ArgumentNullException(nameof(globalNamespace));
      _typeSymbolsByClrType = typeSymbolsByClrType ??
          throw new ArgumentNullException(nameof(typeSymbolsByClrType));
      _typesByQualifiedName = typesByQualifiedName ??
          throw new ArgumentNullException(nameof(typesByQualifiedName));
    }

    public bool TryGetTypeSymbol(Type clrType, out TypeSymbol typeSymbol)
    {
      return _typeSymbolsByClrType.TryGetValue(clrType, out typeSymbol);
    }

    public bool TryGetTypeSymbol(string qualifiedName, out TypeSymbol typeSymbol)
    {
      return _typesByQualifiedName.TryGetValue(qualifiedName, out typeSymbol);
    }

    public bool TryLookupSymbol(string qualifiedPath, out Symbol symbol)
    {
      symbol = null;
      if (string.IsNullOrWhiteSpace(qualifiedPath))
        return false;

      var segments = qualifiedPath.Split('.');
      symbol = GlobalNamespace;

      for (var index = 0; index < segments.Length; index++)
      {
        var segment = segments[index];
        if (symbol is NamespaceSymbol namespaceSymbol)
        {
          symbol = namespaceSymbol.Lookup(segment);
        }
        else if (symbol is TypeSymbol typeSymbol && index == segments.Length - 1)
        {
          symbol = typeSymbol.GetMethodGroup(segment);
        }
        else
        {
          symbol = null;
        }

        if (symbol == null)
          return false;
      }

      return true;
    }
  }

  internal sealed class UdonExposedNodeCache
  {
    private static readonly Lazy<UdonExposedNodeCache> DefaultInstance =
        new(CreateDefault);

    private readonly HashSet<string> _exposedSignatures;

    public static UdonExposedNodeCache Default => DefaultInstance.Value;

    public UdonExposedNodeCache(IReadOnlyCollection<string> exposedSignatures)
    {
      if (exposedSignatures == null)
        throw new ArgumentNullException(nameof(exposedSignatures));

      _exposedSignatures = new HashSet<string>(exposedSignatures, StringComparer.Ordinal);
    }

    public bool IsExposed(string signature)
    {
      return !string.IsNullOrEmpty(signature) &&
             _exposedSignatures.Contains(signature);
    }

    public bool IsTypeExposed(Type type)
    {
      if (type == null)
        return false;

      if (type == typeof(void))
        return true;

      var typeName = UdonExternSignatureFormatter.GetUdonTypeName(type);
      return UdonEditorManager.Instance.GetTypeFromTypeString(typeName) != null;
    }

    private static UdonExposedNodeCache CreateDefault()
    {
      UdonEditorManager.Instance.GetNodeRegistries();

      var signatures = new List<string>();
      foreach (var nodeDefinition in UdonEditorManager.Instance.GetNodeDefinitions())
        signatures.Add(nodeDefinition.fullName);

      return new UdonExposedNodeCache(signatures);
    }
  }

  internal static class UdonExternSignatureFormatter
  {
    private static readonly Dictionary<Type, string> TypeNameCache = new();
    private static readonly object TypeNameCacheGate = new();

    public static string GetUdonMethodName(MethodBase methodInfo)
    {
      if (methodInfo == null)
        throw new ArgumentNullException(nameof(methodInfo));

      var methodSourceType = methodInfo.DeclaringType;
      var sourceTypeName = methodSourceType?.FullName ??
          $"{methodSourceType?.Namespace}{methodSourceType?.Name}";
      var functionNamespace = SanitizeTypeName(sourceTypeName)
          .Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver")
          .Replace("UdonSharpUdonSharpBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

      var methodName = $"__{methodInfo.Name.Trim('_').TrimStart('.')}";
      var parameters = methodInfo.GetParameters();
      var parameterString = string.Empty;

      if (parameters.Length > 0)
      {
        parameterString = "_";
        foreach (var parameter in parameters)
          parameterString += $"_{GetUdonTypeName(parameter.ParameterType)}";
      }
      else if (methodInfo.IsConstructor)
      {
        parameterString = "__";
      }

      var returnString = methodInfo.IsConstructor
          ? $"__{GetUdonTypeName(methodSourceType)}"
          : $"__{GetUdonTypeName(((MethodInfo)methodInfo).ReturnType)}";

      return $"{functionNamespace}.{methodName}{parameterString}{returnString}";
    }

    public static string GetUdonTypeName(Type externType)
    {
      if (externType == null)
        throw new ArgumentNullException(nameof(externType));

      lock (TypeNameCacheGate)
      {
        if (TypeNameCache.TryGetValue(externType, out var foundTypeName))
          return foundTypeName;
      }

      var originalType = externType;
      var externTypeName = GetNameWithoutGenericArity(originalType);
      while (externType.IsArray || externType.IsByRef)
        externType = externType.GetElementType();

      var typeNamespace = externType.Namespace ?? string.Empty;
      if (externType.DeclaringType != null)
      {
        var declaringTypeNamespace = string.Empty;
        var declaringType = externType.DeclaringType;
        while (declaringType != null)
        {
          declaringTypeNamespace = $"{declaringType.Name}.{declaringTypeNamespace}";
          declaringType = declaringType.DeclaringType;
        }

        typeNamespace += $".{declaringTypeNamespace}";
      }

      if (externTypeName == "T" || externTypeName == "T[]")
        typeNamespace = string.Empty;

      var fullTypeName = SanitizeTypeName($"{typeNamespace}.{externTypeName}");
      foreach (var genericType in externType.GetGenericArguments())
        fullTypeName += GetUdonTypeName(genericType);

      if (fullTypeName == "SystemCollectionsGenericListT")
      {
        fullTypeName = "ListT";
      }
      else if (fullTypeName == "SystemCollectionsGenericIEnumerableT")
      {
        fullTypeName = "IEnumerableT";
      }

      lock (TypeNameCacheGate)
        TypeNameCache[originalType] = fullTypeName;

      return fullTypeName;
    }

    public static string SanitizeTypeName(string typeName)
    {
      return (typeName ?? string.Empty).Replace(",", "")
          .Replace(".", "")
          .Replace("[]", "Array")
          .Replace("&", "Ref")
          .Replace("+", "");
    }

    private static string GetNameWithoutGenericArity(Type type)
    {
      var name = type.Name;
      var tickIndex = name.IndexOf('`');
      return tickIndex >= 0
          ? name.Substring(0, tickIndex)
          : name;
    }
  }

  internal sealed class ReflectionExternCatalogBuilder
  {
    private static readonly string[] DefaultNamespacePrefixes =
    {
      "UnityEngine",
      "System",
      "VRC",
      "TMPro"
    };

    private readonly UdonExposedNodeCache _exposedNodeCache;
    private readonly Dictionary<Type, TypeSymbol> _typeSymbolsByClrType = new();
    private readonly Dictionary<string, TypeSymbol> _typesByQualifiedName =
        new(StringComparer.Ordinal);
    private readonly NamespaceSymbol _globalNamespace =
        new("<global>", "");

    public ReflectionExternCatalogBuilder(UdonExposedNodeCache exposedNodeCache)
    {
      _exposedNodeCache = exposedNodeCache ??
          throw new ArgumentNullException(nameof(exposedNodeCache));

      SeedBuiltInTypes();
    }

    public ExternCatalog BuildDefaultCatalog()
    {
      return BuildCatalog(DefaultNamespacePrefixes);
    }

    public ExternCatalog BuildCatalog(IReadOnlyList<string> namespacePrefixes)
    {
      if (namespacePrefixes == null)
        throw new ArgumentNullException(nameof(namespacePrefixes));

      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        if (assembly.IsDynamic)
          continue;

        foreach (var type in GetLoadableTypes(assembly))
        {
          if (!ShouldIncludeType(type, namespacePrefixes))
            continue;

          BuildType(type);
        }
      }

      return new ExternCatalog(
          _globalNamespace,
          new Dictionary<Type, TypeSymbol>(_typeSymbolsByClrType),
          new Dictionary<string, TypeSymbol>(_typesByQualifiedName, StringComparer.Ordinal));
    }

    private void BuildType(Type clrType)
    {
      var typeSymbol = GetOrCreateTypeSymbol(clrType);
      AddTypeToNamespaceTree(clrType, typeSymbol);
      AddMethods(clrType, typeSymbol);
      AddUnsupportedMembers(clrType, typeSymbol);
    }

    private void AddMethods(Type clrType, TypeSymbol typeSymbol)
    {
      const BindingFlags methodFlags =
          BindingFlags.Public |
          BindingFlags.Static |
          BindingFlags.Instance |
          BindingFlags.DeclaredOnly;

      foreach (var method in clrType.GetMethods(methodFlags))
      {
        if (method.IsSpecialName)
          continue;

        if (!method.IsStatic)
        {
          typeSymbol.AddUnsupportedImportMember(
              method.Name,
              "Instance methods are not supported by use imports in v1.");
          continue;
        }

        var externSignature = UdonExternSignatureFormatter.GetUdonMethodName(method);
        if (TryGetUnsupportedStaticMethodReason(method, out var unsupportedReason))
        {
          typeSymbol.AddRejectedCandidate(
              method.Name,
              new ExternCandidate(method, externSignature, false, unsupportedReason));
          continue;
        }

        if (!AreSignatureTypesExposed(method))
        {
          typeSymbol.AddRejectedCandidate(
              method.Name,
              new ExternCandidate(
                  method,
                  externSignature,
                  false,
                  "One or more signature types are not exposed to Udon."));
          continue;
        }

        if (!_exposedNodeCache.IsExposed(externSignature))
        {
          typeSymbol.AddRejectedCandidate(
              method.Name,
              new ExternCandidate(
                  method,
                  externSignature,
                  false,
                  "The computed extern signature is not exposed to Udon."));
          continue;
        }

        typeSymbol.AddMethod(CreateExternMethodSymbol(typeSymbol, method, externSignature));
      }
    }

    private void AddUnsupportedMembers(Type clrType, TypeSymbol typeSymbol)
    {
      const BindingFlags memberFlags =
          BindingFlags.Public |
          BindingFlags.Static |
          BindingFlags.Instance |
          BindingFlags.DeclaredOnly;

      foreach (var property in clrType.GetProperties(memberFlags))
      {
        typeSymbol.AddUnsupportedImportMember(
            property.Name,
            property.GetMethod != null && property.GetMethod.IsStatic ||
            property.SetMethod != null && property.SetMethod.IsStatic
                ? "Static properties are not supported by use imports in v1."
                : "Instance properties are not supported by use imports in v1.");
      }

      foreach (var field in clrType.GetFields(memberFlags))
      {
        typeSymbol.AddUnsupportedImportMember(
            field.Name,
            field.IsStatic
                ? "Static fields are not supported by use imports in v1."
                : "Instance fields are not supported by use imports in v1.");
      }
    }

    private ExternMethodSymbol CreateExternMethodSymbol(
        TypeSymbol containingType,
        MethodInfo method,
        string externSignature)
    {
      var parameters = new List<ParameterSymbol>();
      var methodParameters = method.GetParameters();
      for (var index = 0; index < methodParameters.Length; index++)
      {
        parameters.Add(new ParameterSymbol(
            methodParameters[index].Name ?? $"arg{index}",
            GetOrCreateTypeSymbol(methodParameters[index].ParameterType),
            index));
      }

      return new ExternMethodSymbol(
          method.Name,
          containingType,
          parameters,
          GetOrCreateTypeSymbol(method.ReturnType),
          method,
          externSignature);
    }

    private bool AreSignatureTypesExposed(MethodInfo method)
    {
      if (!_exposedNodeCache.IsTypeExposed(method.ReturnType))
        return false;

      foreach (var parameter in method.GetParameters())
      {
        if (!_exposedNodeCache.IsTypeExposed(parameter.ParameterType))
          return false;
      }

      return true;
    }

    private static bool TryGetUnsupportedStaticMethodReason(
        MethodInfo method,
        out string reason)
    {
      if (method.IsGenericMethod || method.ContainsGenericParameters)
      {
        reason = "Generic methods are not supported in v1.";
        return true;
      }

      if (method.ReturnType.IsByRef || method.ReturnType.IsPointer)
      {
        reason = "Pointer and by-ref return types are not supported in v1.";
        return true;
      }

      foreach (var parameter in method.GetParameters())
      {
        if (parameter.ParameterType.IsByRef || parameter.ParameterType.IsPointer)
        {
          reason = "ref, out, and pointer parameters are not supported in v1.";
          return true;
        }

        if (parameter.IsOptional)
        {
          reason = "Optional parameters are not supported in v1.";
          return true;
        }

        if ((parameter.Attributes & ParameterAttributes.HasFieldMarshal) != 0)
        {
          reason = "Marshalled parameters are not supported in v1.";
          return true;
        }

        if (Attribute.IsDefined(parameter, typeof(ParamArrayAttribute)))
        {
          reason = "params parameters are not supported in v1.";
          return true;
        }
      }

      reason = null;
      return false;
    }

    private void AddTypeToNamespaceTree(Type clrType, TypeSymbol typeSymbol)
    {
      var currentNamespace = _globalNamespace;
      var namespaceSegments = (clrType.Namespace ?? string.Empty).Split('.');

      foreach (var segment in namespaceSegments)
      {
        if (string.IsNullOrEmpty(segment))
          continue;

        currentNamespace = currentNamespace.GetOrAddNamespace(segment);
      }

      currentNamespace.AddType(typeSymbol);
    }

    private TypeSymbol GetOrCreateTypeSymbol(Type clrType)
    {
      if (_typeSymbolsByClrType.TryGetValue(clrType, out var existingTypeSymbol))
        return existingTypeSymbol;

      TypeSymbol typeSymbol;
      if (clrType == typeof(void))
      {
        typeSymbol = TypeSymbol.Void;
      }
      else if (clrType == typeof(string))
      {
        typeSymbol = TypeSymbol.String;
      }
      else if (clrType == typeof(bool))
      {
        typeSymbol = TypeSymbol.Bool;
      }
      else if (clrType == typeof(char))
      {
        typeSymbol = TypeSymbol.Char;
      }
      else if (clrType == typeof(sbyte))
      {
        typeSymbol = TypeSymbol.I8;
      }
      else if (clrType == typeof(byte))
      {
        typeSymbol = TypeSymbol.U8;
      }
      else if (clrType == typeof(short))
      {
        typeSymbol = TypeSymbol.I16;
      }
      else if (clrType == typeof(ushort))
      {
        typeSymbol = TypeSymbol.U16;
      }
      else if (clrType == typeof(int))
      {
        typeSymbol = TypeSymbol.I32;
      }
      else if (clrType == typeof(uint))
      {
        typeSymbol = TypeSymbol.U32;
      }
      else if (clrType == typeof(long))
      {
        typeSymbol = TypeSymbol.I64;
      }
      else if (clrType == typeof(ulong))
      {
        typeSymbol = TypeSymbol.U64;
      }
      else if (clrType == typeof(float))
      {
        typeSymbol = TypeSymbol.F32;
      }
      else if (clrType == typeof(double))
      {
        typeSymbol = TypeSymbol.F64;
      }
      else if (clrType == typeof(object))
      {
        typeSymbol = TypeSymbol.Object;
      }
      else if (clrType.IsArray)
      {
        typeSymbol = TypeSymbol.Array(GetOrCreateTypeSymbol(clrType.GetElementType()));
      }
      else
      {
        var qualifiedName = (clrType.FullName ?? clrType.Name).Replace('+', '.');
        typeSymbol = TypeSymbol.CreateNamed(
            GetSimpleTypeName(clrType),
            qualifiedName,
            !clrType.IsValueType);
      }

      _typeSymbolsByClrType[clrType] = typeSymbol;
      if (typeSymbol.TypeKind == TypeKind.Named)
        _typesByQualifiedName[typeSymbol.QualifiedName] = typeSymbol;

      return typeSymbol;
    }

    private static IEnumerable<Type> GetLoadableTypes(System.Reflection.Assembly assembly)
    {
      try
      {
        return assembly.GetTypes();
      }
      catch (ReflectionTypeLoadException ex)
      {
        return ex.Types;
      }
    }

    private static bool ShouldIncludeType(Type type, IReadOnlyList<string> namespacePrefixes)
    {
      if (type == null ||
          type.IsNested ||
          type.IsGenericTypeDefinition ||
          type.ContainsGenericParameters ||
          string.IsNullOrWhiteSpace(type.Namespace))
      {
        return false;
      }

      if (!type.IsPublic && !type.IsNestedPublic)
        return false;

      foreach (var namespacePrefix in namespacePrefixes)
      {
        if (type.Namespace == namespacePrefix ||
            type.Namespace.StartsWith(namespacePrefix + ".", StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
    }

    private static string GetSimpleTypeName(Type clrType)
    {
      var name = clrType.Name;
      var tickIndex = name.IndexOf('`');
      return tickIndex >= 0
          ? name.Substring(0, tickIndex)
          : name;
    }

    private void SeedBuiltInTypes()
    {
      _typeSymbolsByClrType[typeof(void)] = TypeSymbol.Void;
      _typeSymbolsByClrType[typeof(string)] = TypeSymbol.String;
      _typeSymbolsByClrType[typeof(bool)] = TypeSymbol.Bool;
      _typeSymbolsByClrType[typeof(char)] = TypeSymbol.Char;
      _typeSymbolsByClrType[typeof(sbyte)] = TypeSymbol.I8;
      _typeSymbolsByClrType[typeof(byte)] = TypeSymbol.U8;
      _typeSymbolsByClrType[typeof(short)] = TypeSymbol.I16;
      _typeSymbolsByClrType[typeof(ushort)] = TypeSymbol.U16;
      _typeSymbolsByClrType[typeof(int)] = TypeSymbol.I32;
      _typeSymbolsByClrType[typeof(uint)] = TypeSymbol.U32;
      _typeSymbolsByClrType[typeof(long)] = TypeSymbol.I64;
      _typeSymbolsByClrType[typeof(ulong)] = TypeSymbol.U64;
      _typeSymbolsByClrType[typeof(float)] = TypeSymbol.F32;
      _typeSymbolsByClrType[typeof(double)] = TypeSymbol.F64;
      _typeSymbolsByClrType[typeof(object)] = TypeSymbol.Object;
      _typesByQualifiedName[TypeSymbol.Object.QualifiedName] = TypeSymbol.Object;
    }
  }

  internal static class SobakasuBuiltInEnvironment
  {
    private static readonly Lazy<SobakasuCompilationEnvironment> DefaultEnvironment =
        new(CreateDefault);

    public static SobakasuCompilationEnvironment Default => DefaultEnvironment.Value;

    private static SobakasuCompilationEnvironment CreateDefault()
    {
      var catalog = new ReflectionExternCatalogBuilder(UdonExposedNodeCache.Default)
          .BuildDefaultCatalog();
      var compatibilitySymbols = new Dictionary<string, Symbol>(StringComparer.Ordinal);

      if (catalog.TryLookupSymbol("UnityEngine.Debug", out var debugType))
        compatibilitySymbols["Debug"] = debugType;

      return new SobakasuCompilationEnvironment(catalog, compatibilitySymbols);
    }
  }
}
