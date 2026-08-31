using System;
using System.Collections.Generic;
using System.Reflection;
using VRC.Udon.Editor;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class ExternCandidate
  {
    public MemberInfo MemberInfo { get; }
    public MethodInfo MethodInfo => MemberInfo as MethodInfo;
    public string ExternSignature { get; }
    public bool IsCallable { get; }
    public string RejectionReason { get; }
    public string DisplayName =>
        $"{MemberInfo.DeclaringType?.FullName}.{MemberInfo.Name}";

    public ExternCandidate(
        MemberInfo methodInfo,
        string externSignature,
        bool isCallable,
        string rejectionReason)
    {
      MemberInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
      ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature));
      IsCallable = isCallable;
      RejectionReason = rejectionReason ?? string.Empty;
    }
  }

  internal sealed class ExternCatalog
  {
    private readonly IReadOnlyDictionary<Type, TypeSymbol> _typeSymbolsByClrType;
    private readonly IReadOnlyDictionary<string, TypeSymbol> _typesByQualifiedName;
    private readonly Dictionary<TypeSymbol, Type> _clrTypesByTypeSymbol;
    private readonly UdonExposedNodeCache _exposedNodeCache;

    public NamespaceSymbol GlobalNamespace { get; }

    public ExternCatalog(
        NamespaceSymbol globalNamespace,
        IReadOnlyDictionary<Type, TypeSymbol> typeSymbolsByClrType,
        IReadOnlyDictionary<string, TypeSymbol> typesByQualifiedName,
        UdonExposedNodeCache exposedNodeCache = null)
    {
      GlobalNamespace = globalNamespace ?? throw new ArgumentNullException(nameof(globalNamespace));
      _typeSymbolsByClrType = typeSymbolsByClrType ??
          throw new ArgumentNullException(nameof(typeSymbolsByClrType));
      _typesByQualifiedName = typesByQualifiedName ??
          throw new ArgumentNullException(nameof(typesByQualifiedName));
      _exposedNodeCache = exposedNodeCache;
      _clrTypesByTypeSymbol = new Dictionary<TypeSymbol, Type>();

      foreach (var pair in _typeSymbolsByClrType)
      {
        if (!_clrTypesByTypeSymbol.ContainsKey(pair.Value))
          _clrTypesByTypeSymbol.Add(pair.Value, pair.Key);
      }
    }

    public bool TryGetTypeSymbol(Type clrType, out TypeSymbol typeSymbol)
    {
      return _typeSymbolsByClrType.TryGetValue(clrType, out typeSymbol);
    }

    public bool TryGetTypeSymbol(string qualifiedName, out TypeSymbol typeSymbol)
    {
      return _typesByQualifiedName.TryGetValue(qualifiedName, out typeSymbol);
    }

    public bool TryGetClrType(TypeSymbol typeSymbol, out Type clrType)
    {
      if (typeSymbol == null)
      {
        clrType = null;
        return false;
      }

      if (_clrTypesByTypeSymbol.TryGetValue(typeSymbol, out clrType))
        return true;

      if (typeSymbol.TypeKind == TypeKind.Array &&
          TryGetClrType(typeSymbol.ElementType, out var elementType))
      {
        clrType = elementType.MakeArrayType();
        return true;
      }

      if (_typesByQualifiedName.TryGetValue(
              typeSymbol.RuntimeQualifiedName,
              out var runtimeTypeSymbol) &&
          _clrTypesByTypeSymbol.TryGetValue(runtimeTypeSymbol, out clrType))
      {
        return true;
      }

      clrType = null;
      return false;
    }

    public TypeSymbol GetRuntimeTypeSymbol(TypeSymbol typeSymbol)
    {
      if (typeSymbol == null)
        return TypeSymbol.Error;

      return _typesByQualifiedName.TryGetValue(
          typeSymbol.RuntimeQualifiedName,
          out var runtimeType)
          ? runtimeType
          : typeSymbol;
    }

    public bool IsTypeExposed(TypeSymbol typeSymbol)
    {
      return TryGetClrType(typeSymbol, out var clrType) &&
             (_exposedNodeCache?.IsTypeExposed(clrType) ?? true);
    }

    public bool TryGetArrayIntrinsics(
        TypeSymbol arrayType,
        out ArrayIntrinsicSymbols intrinsics,
        out string reason)
    {
      intrinsics = null;
      reason = null;
      if (arrayType == null || arrayType.TypeKind != TypeKind.Array)
      {
        reason = "The requested type is not an array.";
        return false;
      }

      if (!TryGetClrType(arrayType, out var arrayClrType) ||
          !TryGetClrType(arrayType.ElementType, out var elementClrType))
      {
        reason = $"CLR ABI type '{arrayType.RuntimeQualifiedName}' could not be constructed.";
        return false;
      }

      if (!(_exposedNodeCache?.IsTypeExposed(arrayClrType) ?? true))
      {
        reason = $"Udon does not expose ABI type '{arrayClrType.FullName}'.";
        return false;
      }

      var arrayName = UdonExternSignatureFormatter.GetUdonTypeName(arrayClrType);
      var elementName = UdonExternSignatureFormatter.GetUdonTypeName(elementClrType);
      var constructor = $"{arrayName}.__ctor__SystemInt32__{arrayName}";
      var getter = $"{arrayName}.__Get__SystemInt32__{elementName}";
      var setter = $"{arrayName}.__Set__SystemInt32_{elementName}__SystemVoid";
      var length = $"{arrayName}.__get_Length__SystemInt32";

      if (!IsArrayExternExposed(constructor) || !IsArrayExternExposed(length))
      {
        reason = $"Udon does not expose construction and length operations for '{arrayClrType.FullName}'.";
        return false;
      }

      if (!IsArrayExternExposed(getter) || !IsArrayExternExposed(setter))
      {
        if (typeof(UnityEngine.Object).IsAssignableFrom(elementClrType))
        {
          var objectArrayName = UdonExternSignatureFormatter.GetUdonTypeName(typeof(object[]));
          var objectName = UdonExternSignatureFormatter.GetUdonTypeName(typeof(object));
          var objectGetter = $"{objectArrayName}.__Get__SystemInt32__{objectName}";
          var objectSetter = $"{objectArrayName}.__Set__SystemInt32_{objectName}__SystemVoid";
          if (IsArrayExternExposed(objectGetter) && IsArrayExternExposed(objectSetter))
          {
            getter = objectGetter;
            setter = objectSetter;
          }
          else
          {
            reason = $"Udon does not expose getter and setter operations for '{arrayClrType.FullName}'.";
            return false;
          }
        }
        else
        {
          reason = $"Udon does not expose getter and setter operations for '{arrayClrType.FullName}'.";
          return false;
        }
      }

      intrinsics = new ArrayIntrinsicSymbols(
          constructor,
          getter,
          setter,
          length,
          TypeSymbol.I32);
      return true;
    }

    public bool IsPublicArrayType(TypeSymbol arrayType)
    {
      return arrayType != null &&
          arrayType.TypeKind == TypeKind.Array &&
          IsTypeExposed(arrayType);
    }

    private bool IsArrayExternExposed(string signature)
    {
      return _exposedNodeCache?.IsExposed(signature) ?? true;
    }

    public MethodGroupSymbol GetExternalMethodGroup(
        TypeSymbol typeSymbol,
        string memberName)
    {
      return GetRuntimeTypeSymbol(typeSymbol).GetMethodGroup(memberName);
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

    public IReadOnlyList<string> GetUnaryOperatorSignatures(
        string operatorName,
        TypeSymbol operandType,
        TypeSymbol resultType)
    {
      if (!TryGetClrType(operandType, out var operandClrType) ||
          !TryGetClrType(resultType, out var resultClrType))
      {
        return Array.Empty<string>();
      }

      return GetOperatorSignatures(
          operandClrType,
          operatorName,
          new[] { operandClrType },
          resultClrType);
    }

    public IReadOnlyList<string> GetBinaryOperatorSignatures(
        string operatorName,
        TypeSymbol leftType,
        TypeSymbol rightType,
        TypeSymbol resultType)
    {
      if (!TryGetClrType(leftType, out var leftClrType) ||
          !TryGetClrType(rightType, out var rightClrType) ||
          !TryGetClrType(resultType, out var resultClrType))
      {
        return Array.Empty<string>();
      }

      return GetOperatorSignatures(
          leftClrType,
          operatorName,
          new[] { leftClrType, rightClrType },
          resultClrType);
    }

    private IReadOnlyList<string> GetOperatorSignatures(
        Type declaringClrType,
        string operatorName,
        IReadOnlyList<Type> parameterTypes,
        Type resultClrType)
    {
      if (_exposedNodeCache == null ||
          string.IsNullOrWhiteSpace(operatorName) ||
          declaringClrType == null ||
          resultClrType == null)
      {
        return Array.Empty<string>();
      }

      var signatures = new List<string>();
      var operatorNames = GetOperatorNameVariants(operatorName);
      const BindingFlags operatorFlags = BindingFlags.Public | BindingFlags.Static;

      foreach (var method in declaringClrType.GetMethods(operatorFlags))
      {
        if (!method.IsSpecialName ||
            !string.Equals(method.Name, operatorName, StringComparison.Ordinal))
        {
          continue;
        }

        if (!HasExactParameterSignature(method, parameterTypes, resultClrType))
          continue;

        var signature = UdonExternSignatureFormatter.GetUdonMethodName(method);
        if (_exposedNodeCache.IsExposed(signature))
          AddUnique(signatures, signature);
      }

      foreach (var operatorNameVariant in operatorNames)
      {
        var exactSignature = BuildOperatorExternSignature(
            declaringClrType,
            operatorNameVariant,
            parameterTypes,
            resultClrType);
        if (_exposedNodeCache.IsExposed(exactSignature))
          AddUnique(signatures, exactSignature);
      }

      var expectedSuffix = BuildOperatorExternSignatureSuffix(parameterTypes, resultClrType);
      foreach (var exposedSignature in _exposedNodeCache.ExposedSignatures)
      {
        if (!exposedSignature.EndsWith(expectedSuffix, StringComparison.Ordinal))
        {
          continue;
        }

        foreach (var operatorNameVariant in operatorNames)
        {
          var operatorMarker = $".__{operatorNameVariant}__";
          if (exposedSignature.IndexOf(operatorMarker, StringComparison.Ordinal) >= 0)
            AddUnique(signatures, exposedSignature);
        }
      }

      return signatures.ToArray();
    }

    private static bool HasExactParameterSignature(
        MethodInfo method,
        IReadOnlyList<Type> parameterTypes,
        Type resultClrType)
    {
      if (method.ReturnType != resultClrType)
        return false;

      var parameters = method.GetParameters();
      if (parameters.Length != parameterTypes.Count)
        return false;

      for (var index = 0; index < parameters.Length; index++)
      {
        if (parameters[index].ParameterType != parameterTypes[index])
          return false;
      }

      return true;
    }

    private static string BuildOperatorExternSignature(
        Type declaringClrType,
        string operatorName,
        IReadOnlyList<Type> parameterTypes,
        Type resultClrType)
    {
      return $"{UdonExternSignatureFormatter.GetUdonTypeName(declaringClrType)}.__{operatorName}{BuildOperatorExternSignatureSuffix(parameterTypes, resultClrType)}";
    }

    private static string BuildOperatorExternSignatureSuffix(
        IReadOnlyList<Type> parameterTypes,
        Type resultClrType)
    {
      var suffix = "__";
      for (var index = 0; index < parameterTypes.Count; index++)
      {
        if (index > 0)
          suffix += "_";

        suffix += UdonExternSignatureFormatter.GetUdonTypeName(parameterTypes[index]);
      }

      suffix += $"__{UdonExternSignatureFormatter.GetUdonTypeName(resultClrType)}";
      return suffix;
    }

    private static IReadOnlyList<string> GetOperatorNameVariants(string operatorName)
    {
      var operatorNames = new List<string>();
      AddUnique(operatorNames, operatorName);

      switch (operatorName)
      {
        case "op_Multiply":
          AddUnique(operatorNames, "op_Multiplication");
          break;

        case "op_Modulus":
          AddUnique(operatorNames, "op_Remainder");
          break;

        case "op_BitwiseAnd":
          AddUnique(operatorNames, "op_LogicalAnd");
          break;

        case "op_BitwiseOr":
          AddUnique(operatorNames, "op_LogicalOr");
          break;

        case "op_ExclusiveOr":
          AddUnique(operatorNames, "op_LogicalXor");
          break;

        case "op_LogicalNot":
          AddUnique(operatorNames, "op_UnaryNegation");
          break;

        case "op_UnaryNegation":
          AddUnique(operatorNames, "op_UnaryMinus");
          break;

        case "op_OnesComplement":
          AddUnique(operatorNames, "op_BitwiseNot");
          break;
      }

      return operatorNames.ToArray();
    }

    private static void AddUnique(ICollection<string> signatures, string signature)
    {
      if (string.IsNullOrEmpty(signature) || signatures.Contains(signature))
        return;

      signatures.Add(signature);
    }
  }

  internal sealed class UdonExposedNodeCache
  {
    private static readonly Lazy<UdonExposedNodeCache> DefaultInstance =
        new(CreateDefault);

    private readonly HashSet<string> _exposedSignatures;

    public static UdonExposedNodeCache Default => DefaultInstance.Value;
    public IReadOnlyCollection<string> ExposedSignatures => _exposedSignatures;

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
          new Dictionary<string, TypeSymbol>(_typesByQualifiedName, StringComparer.Ordinal),
          _exposedNodeCache);
    }

    private void BuildType(Type clrType)
    {
      var typeSymbol = GetOrCreateTypeSymbol(clrType);
      AddTypeToNamespaceTree(clrType, typeSymbol);
      AddMethods(clrType, typeSymbol);
      AddConstructors(clrType, typeSymbol);
      AddProperties(clrType, typeSymbol);
      AddFields(clrType, typeSymbol);
    }

    private void AddMethods(Type clrType, TypeSymbol typeSymbol)
    {
      const BindingFlags methodFlags =
          BindingFlags.Public |
          BindingFlags.Static |
          BindingFlags.Instance |
          BindingFlags.FlattenHierarchy;

      foreach (var method in clrType.GetMethods(methodFlags))
      {
        if (method.IsSpecialName &&
            !method.Name.StartsWith("op_", StringComparison.Ordinal))
          continue;

        var externSignature = UdonExternSignatureFormatter.GetUdonMethodName(method);
        if (TryGetUnsupportedMethodReason(method, out var unsupportedReason))
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

    private void AddConstructors(Type clrType, TypeSymbol typeSymbol)
    {
      const BindingFlags constructorFlags =
          BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

      foreach (var constructor in clrType.GetConstructors(constructorFlags))
      {
        var externSignature = UdonExternSignatureFormatter.GetUdonMethodName(constructor);
        if (!AreSignatureTypesExposed(constructor))
        {
          typeSymbol.AddRejectedCandidate(
              "new",
              new ExternCandidate(
                  constructor,
                  externSignature,
                  false,
                  "One or more constructor signature types are not exposed to Udon."));
          continue;
        }

        if (!_exposedNodeCache.IsExposed(externSignature))
        {
          typeSymbol.AddRejectedCandidate(
              "new",
              new ExternCandidate(
                  constructor,
                  externSignature,
                  false,
                  "The computed constructor signature is not exposed to Udon."));
          continue;
        }

        typeSymbol.AddMethod(CreateExternConstructorSymbol(
            typeSymbol,
            constructor,
            externSignature));
      }
    }

    private void AddProperties(Type clrType, TypeSymbol typeSymbol)
    {
      const BindingFlags memberFlags =
          BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance |
          BindingFlags.FlattenHierarchy;

      foreach (var property in clrType.GetProperties(memberFlags))
      {
        if (property.GetMethod != null)
          AddAccessor(typeSymbol, property.Name, property.GetMethod);

        if (property.SetMethod != null)
          AddAccessor(typeSymbol, property.Name, property.SetMethod);
      }
    }

    private void AddAccessor(
        TypeSymbol typeSymbol,
      string publicName,
      MethodInfo accessor)
    {
      var externSignature = UdonExternSignatureFormatter.GetUdonMethodName(accessor);
      string rejectionReason = null;
      if (TryGetUnsupportedMethodReason(accessor, out var unsupportedReason))
        rejectionReason = unsupportedReason;
      else if (!AreSignatureTypesExposed(accessor))
        rejectionReason = "One or more accessor signature types are not exposed to Udon.";
      else if (!_exposedNodeCache.IsExposed(externSignature))
        rejectionReason = "The computed accessor signature is not exposed to Udon.";

      if (rejectionReason != null)
      {
        typeSymbol.AddRejectedCandidate(
            publicName,
            new ExternCandidate(
                accessor,
                externSignature,
                false,
                rejectionReason));
        return;
      }

      typeSymbol.AddMethod(CreateExternMethodSymbol(
          typeSymbol,
          accessor,
          externSignature,
          publicName));
    }

    private void AddFields(Type clrType, TypeSymbol typeSymbol)
    {
      const BindingFlags memberFlags =
          BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance |
          BindingFlags.FlattenHierarchy;

      foreach (var field in clrType.GetFields(memberFlags))
      {
        if (!_exposedNodeCache.IsTypeExposed(field.FieldType))
        {
          typeSymbol.AddRejectedCandidate(
              field.Name,
              new ExternCandidate(
                  field,
                  BuildFieldExternSignature(field, isSetter: false),
                  false,
                  "The field type is not exposed to Udon."));

          if (!field.IsInitOnly && !field.IsLiteral)
          {
            typeSymbol.AddRejectedCandidate(
                field.Name,
                new ExternCandidate(
                    field,
                    BuildFieldExternSignature(field, isSetter: true),
                    false,
                    "The field type is not exposed to Udon."));
          }

          continue;
        }

        var getterSignature = BuildFieldExternSignature(field, isSetter: false);
        if (_exposedNodeCache.IsExposed(getterSignature))
        {
          typeSymbol.AddMethod(CreateExternFieldSymbol(
              typeSymbol,
              field,
              getterSignature,
              isSetter: false));
        }
        else
        {
          typeSymbol.AddRejectedCandidate(
              field.Name,
              new ExternCandidate(
                  field,
                  getterSignature,
                  false,
                  "The computed field getter signature is not exposed to Udon."));
        }

        if (field.IsInitOnly || field.IsLiteral)
          continue;

        var setterSignature = BuildFieldExternSignature(field, isSetter: true);
        if (_exposedNodeCache.IsExposed(setterSignature))
        {
          typeSymbol.AddMethod(CreateExternFieldSymbol(
              typeSymbol,
              field,
              setterSignature,
              isSetter: true));
        }
        else
        {
          typeSymbol.AddRejectedCandidate(
              field.Name,
              new ExternCandidate(
                  field,
                  setterSignature,
                  false,
                  "The computed field setter signature is not exposed to Udon."));
        }
      }
    }

    private ExternMethodSymbol CreateExternMethodSymbol(
        TypeSymbol containingType,
        MethodInfo method,
        string externSignature,
        string publicName = null)
    {
      var parameters = new List<ParameterSymbol>();
      var abiParameters = new List<ExternParameterSymbol>();
      if (!method.IsStatic)
      {
        parameters.Add(new ParameterSymbol(
            "self",
            containingType,
            parameters.Count));
      }

      var methodParameters = method.GetParameters();
      for (var index = 0; index < methodParameters.Length; index++)
      {
        var parameter = methodParameters[index];
        var passingMode = GetPassingMode(parameter);
        var clrType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()
            : parameter.ParameterType;
        var parameterType = GetOrCreateTypeSymbol(clrType);
        var logicalInputOrdinal = -1;
        if (passingMode != ExternParameterPassingMode.Out)
        {
          logicalInputOrdinal = parameters.Count;
          parameters.Add(new ParameterSymbol(
              parameter.Name ?? $"arg{index}",
              parameterType,
              logicalInputOrdinal));
        }
        abiParameters.Add(new ExternParameterSymbol(
            parameter.Name ?? $"arg{index}",
            parameterType,
            passingMode,
            logicalInputOrdinal));
      }

      var abiReturnType = GetOrCreateTypeSymbol(method.ReturnType);
      var logicalReturnType = BuildLogicalReturnType(abiReturnType, abiParameters);

      return new ExternMethodSymbol(
          publicName ?? method.Name,
          containingType,
          parameters,
          logicalReturnType,
          method,
          externSignature,
          memberKind: method.Name.StartsWith("op_", StringComparison.Ordinal)
              ? ExternMemberKind.Operator
              : method.Name.StartsWith("get_", StringComparison.Ordinal)
                  ? ExternMemberKind.Getter
                  : method.Name.StartsWith("set_", StringComparison.Ordinal)
                      ? ExternMemberKind.Setter
                      : ExternMemberKind.Method,
          abiParameters: abiParameters,
          abiReturnType: abiReturnType);
    }

    private ExternMethodSymbol CreateExternConstructorSymbol(
        TypeSymbol containingType,
        ConstructorInfo constructor,
        string externSignature)
    {
      var parameters = new List<ParameterSymbol>();
      var abiParameters = new List<ExternParameterSymbol>();
      foreach (var parameter in constructor.GetParameters())
      {
        var passingMode = GetPassingMode(parameter);
        var clrType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()
            : parameter.ParameterType;
        var parameterType = GetOrCreateTypeSymbol(clrType);
        var logicalInputOrdinal = -1;
        if (passingMode != ExternParameterPassingMode.Out)
        {
          logicalInputOrdinal = parameters.Count;
          parameters.Add(new ParameterSymbol(
              parameter.Name ?? $"arg{logicalInputOrdinal}",
              parameterType,
              logicalInputOrdinal));
        }
        abiParameters.Add(new ExternParameterSymbol(
            parameter.Name ?? $"arg{abiParameters.Count}",
            parameterType,
            passingMode,
            logicalInputOrdinal));
      }

      var logicalReturnType = BuildLogicalReturnType(containingType, abiParameters);

      return new ExternMethodSymbol(
          "new",
          containingType,
          parameters,
          logicalReturnType,
          constructor,
          externSignature,
          isStatic: true,
          memberKind: ExternMemberKind.Constructor,
          abiParameters: abiParameters,
          abiReturnType: containingType);
    }

    private ExternMethodSymbol CreateExternFieldSymbol(
        TypeSymbol containingType,
        FieldInfo field,
        string externSignature,
        bool isSetter)
    {
      var parameters = new List<ParameterSymbol>();
      var abiParameters = new List<ExternParameterSymbol>();
      if (!field.IsStatic)
      {
        parameters.Add(new ParameterSymbol(
            "self",
            containingType,
            parameters.Count));
      }

      if (isSetter)
      {
        var logicalInputOrdinal = parameters.Count;
        var fieldType = GetOrCreateTypeSymbol(field.FieldType);
        parameters.Add(new ParameterSymbol(
            "value",
            fieldType,
            logicalInputOrdinal));
        abiParameters.Add(new ExternParameterSymbol(
            "value",
            fieldType,
            ExternParameterPassingMode.Normal,
            logicalInputOrdinal));
      }

      var abiReturnType = isSetter
          ? TypeSymbol.Unit
          : GetOrCreateTypeSymbol(field.FieldType);

      return new ExternMethodSymbol(
          field.Name,
          containingType,
          parameters,
          abiReturnType,
          null,
          externSignature,
          isStatic: field.IsStatic,
          memberKind: isSetter
              ? ExternMemberKind.Setter
              : ExternMemberKind.Getter,
          abiParameters: abiParameters,
          abiReturnType: abiReturnType);
    }

    private static ExternParameterPassingMode GetPassingMode(ParameterInfo parameter)
    {
      if (parameter.IsOut)
        return ExternParameterPassingMode.Out;
      if (!parameter.ParameterType.IsByRef)
        return ExternParameterPassingMode.Normal;
      if (parameter.IsIn)
        return ExternParameterPassingMode.In;
      return ExternParameterPassingMode.Ref;
    }

    internal static TypeSymbol BuildLogicalReturnType(
        TypeSymbol abiReturnType,
        IReadOnlyList<ExternParameterSymbol> parameters)
    {
      var outputs = new List<TypeSymbol>();
      if (abiReturnType != TypeSymbol.Unit)
        outputs.Add(abiReturnType);
      foreach (var parameter in parameters)
      {
        if (parameter.PassingMode == ExternParameterPassingMode.Ref ||
            parameter.PassingMode == ExternParameterPassingMode.Out)
        {
          outputs.Add(parameter.LogicalOutputType);
        }
      }

      if (outputs.Count == 0)
        return TypeSymbol.Unit;
      if (outputs.Count == 1)
        return outputs[0];
      return TypeSymbol.Tuple(outputs);
    }

    internal static string BuildFieldExternSignature(FieldInfo field, bool isSetter)
    {
      var declaringType = UdonExternSignatureFormatter.GetUdonTypeName(field.DeclaringType);
      var fieldType = UdonExternSignatureFormatter.GetUdonTypeName(field.FieldType);
      return isSetter
          ? $"{declaringType}.__set_{field.Name}__{fieldType}"
          : $"{declaringType}.__get_{field.Name}__{fieldType}";
    }

    private bool AreSignatureTypesExposed(MethodInfo method)
    {
      if (!_exposedNodeCache.IsTypeExposed(method.ReturnType))
        return false;

      foreach (var parameter in method.GetParameters())
      {
        var parameterType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()
            : parameter.ParameterType;
        if (!_exposedNodeCache.IsTypeExposed(parameterType))
          return false;
      }

      return true;
    }

    private bool AreSignatureTypesExposed(ConstructorInfo constructor)
    {
      if (!_exposedNodeCache.IsTypeExposed(constructor.DeclaringType))
        return false;

      foreach (var parameter in constructor.GetParameters())
      {
        var parameterType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()
            : parameter.ParameterType;
        if (!_exposedNodeCache.IsTypeExposed(parameterType))
          return false;
      }

      return true;
    }

    internal static bool TryGetUnsupportedMethodReason(
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
        if (parameter.ParameterType.IsPointer)
        {
          reason = "Pointer parameters are not supported.";
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
      if (TryGetBuiltInTypeSymbol(clrType, out var builtInType))
      {
        typeSymbol = builtInType;
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

    internal static string GetSimpleTypeName(Type clrType)
    {
      var name = clrType.Name;
      var tickIndex = name.IndexOf('`');
      return tickIndex >= 0
          ? name.Substring(0, tickIndex)
          : name;
    }

    internal static bool TryGetBuiltInTypeSymbol(
        Type clrType,
        out TypeSymbol typeSymbol)
    {
      if (clrType == typeof(void))
        typeSymbol = TypeSymbol.Unit;
      else if (clrType == typeof(string))
        typeSymbol = TypeSymbol.String;
      else if (clrType == typeof(bool))
        typeSymbol = TypeSymbol.Bool;
      else if (clrType == typeof(char))
        typeSymbol = TypeSymbol.Char;
      else if (clrType == typeof(sbyte))
        typeSymbol = TypeSymbol.I8;
      else if (clrType == typeof(byte))
        typeSymbol = TypeSymbol.U8;
      else if (clrType == typeof(short))
        typeSymbol = TypeSymbol.I16;
      else if (clrType == typeof(ushort))
        typeSymbol = TypeSymbol.U16;
      else if (clrType == typeof(int))
        typeSymbol = TypeSymbol.I32;
      else if (clrType == typeof(uint))
        typeSymbol = TypeSymbol.U32;
      else if (clrType == typeof(long))
        typeSymbol = TypeSymbol.I64;
      else if (clrType == typeof(ulong))
        typeSymbol = TypeSymbol.U64;
      else if (clrType == typeof(float))
        typeSymbol = TypeSymbol.F32;
      else if (clrType == typeof(double))
        typeSymbol = TypeSymbol.F64;
      else if (clrType == typeof(object))
        typeSymbol = TypeSymbol.Object;
      else
      {
        typeSymbol = null;
        return false;
      }

      return true;
    }

    private void SeedBuiltInTypes()
    {
      _typeSymbolsByClrType[typeof(void)] = TypeSymbol.Unit;
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

      foreach (var pair in _typeSymbolsByClrType)
      {
        var qualifiedName = pair.Key.FullName ?? pair.Key.Name;
        _typesByQualifiedName[qualifiedName] = pair.Value;
      }
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
      return new SobakasuCompilationEnvironment(catalog);
    }
  }
}
