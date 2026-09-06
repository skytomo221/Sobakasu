using System;
using System.Collections.Generic;
using System.Reflection;
using VRC.Udon.Editor;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
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
    private readonly Dictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>>
        _operatorGroupsByFirstOperandType = new();

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

      var operatorGroups = new Dictionary<
          TypeSymbol,
          IReadOnlyDictionary<string, MethodGroupSymbol>>();
      foreach (var pair in _operatorGroupsByFirstOperandType)
      {
        operatorGroups.Add(
            pair.Key,
            new Dictionary<string, MethodGroupSymbol>(pair.Value, StringComparer.Ordinal));
      }

      return new ExternCatalog(
          _globalNamespace,
          new Dictionary<Type, TypeSymbol>(_typeSymbolsByClrType),
          new Dictionary<string, TypeSymbol>(_typesByQualifiedName, StringComparer.Ordinal),
          _exposedNodeCache,
          operatorGroups);
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
        if (method.Name.StartsWith("op_", StringComparison.Ordinal) &&
            ExternCatalog.TryResolveOperatorExternSignature(
                method,
                _exposedNodeCache.IsExposed,
                out var resolvedOperatorSignature))
        {
          externSignature = resolvedOperatorSignature;
        }
        if (TryGetUnsupportedMethodReason(method, out var unsupportedReason))
        {
          typeSymbol.AddRejectedCandidate(
              method.Name,
              new ExternCandidate(method, externSignature, false, unsupportedReason));
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

        var methodSymbol = CreateExternMethodSymbol(typeSymbol, method, externSignature);
        typeSymbol.AddMethod(methodSymbol);
        if (method.Name.StartsWith("op_", StringComparison.Ordinal))
          AddOperatorByFirstOperand(method, methodSymbol);
      }
    }

    private void AddOperatorByFirstOperand(
        MethodInfo method,
        ExternMethodSymbol methodSymbol)
    {
      var parameters = method.GetParameters();
      if (parameters.Length == 0)
        return;

      var operandClrType = parameters[0].ParameterType;
      if (operandClrType.IsByRef)
        operandClrType = operandClrType.GetElementType();
      if (operandClrType == null)
        return;

      var hostType = GetOrCreateTypeSymbol(operandClrType);
      if (!_operatorGroupsByFirstOperandType.TryGetValue(hostType, out var groups))
      {
        groups = new Dictionary<string, MethodGroupSymbol>(StringComparer.Ordinal);
        _operatorGroupsByFirstOperandType.Add(hostType, groups);
      }

      if (!groups.TryGetValue(method.Name, out var group))
      {
        group = new MethodGroupSymbol(method.Name, hostType);
        groups.Add(method.Name, group);
      }

      foreach (var existing in group.Methods)
      {
        if (existing is ExternMethodSymbol external &&
            string.Equals(
                external.ExternSignature,
                methodSymbol.ExternSignature,
                StringComparison.Ordinal))
        {
          return;
        }
      }

      group.AddMethod(methodSymbol);
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
      var genericParameters = new List<TypeSymbol>();
      var genericConstraints = new List<ExternGenericParameterConstraint>();
      if (!method.IsStatic)
      {
        parameters.Add(new ParameterSymbol(
            "self",
            containingType,
          parameters.Count));
      }

      if (method.IsGenericMethodDefinition)
      {
        foreach (var genericParameter in method.GetGenericArguments())
        {
          var parameterSymbol = GetOrCreateTypeSymbol(genericParameter);
          genericParameters.Add(parameterSymbol);
          var constraintTypes = genericParameter.GetGenericParameterConstraints();
          var constraintSymbols = new TypeSymbol[constraintTypes.Length];
          for (var index = 0; index < constraintTypes.Length; index++)
            constraintSymbols[index] = GetOrCreateTypeSymbol(constraintTypes[index]);
          genericConstraints.Add(new ExternGenericParameterConstraint(
              parameterSymbol,
              genericParameter.GenericParameterAttributes,
              constraintSymbols));
          abiParameters.Add(new ExternParameterSymbol(
              genericParameter.Name,
              GetOrCreateTypeSymbol(typeof(Type)),
              ExternParameterPassingMode.GenericTypeArgument,
              -1));
        }
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
          abiReturnType: abiReturnType,
          genericParameters: genericParameters,
          genericConstraints: genericConstraints);
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
      if (!IsExternSignatureTypeRepresentable(method.ReturnType))
        return false;

      foreach (var parameter in method.GetParameters())
      {
        var parameterType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()
            : parameter.ParameterType;
        if (!IsExternSignatureTypeRepresentable(parameterType))
          return false;
      }

      return true;
    }

    private bool IsExternSignatureTypeRepresentable(Type type)
    {
      if (type.IsByRef)
        type = type.GetElementType();
      if (type.IsGenericParameter)
        return true;
      if (type.IsArray)
        return type.GetArrayRank() == 1 &&
            IsExternSignatureTypeRepresentable(type.GetElementType());
      if (type.IsGenericType)
      {
        foreach (var argument in type.GetGenericArguments())
        {
          if (!IsExternSignatureTypeRepresentable(argument))
            return false;
        }
        return true;
      }
      return _exposedNodeCache.IsTypeExposed(type);
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
      if (method.ContainsGenericParameters && !method.IsGenericMethodDefinition)
      {
        reason = "Open generic declaring types are not supported.";
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
      else if (clrType.IsGenericParameter)
      {
        var owner = (object)clrType.DeclaringMethod ?? clrType.DeclaringType ?? clrType;
        typeSymbol = TypeSymbol.CreateGenericParameter(
            clrType.Name,
            owner,
            clrType.GenericParameterPosition,
            owner.ToString(),
            clrType);
      }
      else if (clrType.IsArray)
      {
        typeSymbol = TypeSymbol.Array(GetOrCreateTypeSymbol(clrType.GetElementType()));
      }
      else if (clrType.IsGenericType)
      {
        var definition = clrType.IsGenericTypeDefinition
            ? clrType
            : clrType.GetGenericTypeDefinition();
        TypeSymbol definitionSymbol;
        if (clrType.IsGenericTypeDefinition)
        {
          var qualifiedName = (clrType.FullName ?? clrType.Name).Replace('+', '.');
          var tickIndex = qualifiedName.IndexOf('`');
          if (tickIndex >= 0)
            qualifiedName = qualifiedName.Substring(0, tickIndex);
          definitionSymbol = TypeSymbol.CreateNamed(
              GetSimpleTypeName(clrType),
              qualifiedName,
              !clrType.IsValueType,
              clrType,
              isExternalBinding: true);
          _typeSymbolsByClrType[clrType] = definitionSymbol;
          var genericParameters = clrType.GetGenericArguments();
          var parameterSymbols = new TypeSymbol[genericParameters.Length];
          for (var index = 0; index < genericParameters.Length; index++)
            parameterSymbols[index] = GetOrCreateTypeSymbol(genericParameters[index]);
          definitionSymbol.SetGenericParameters(parameterSymbols);
          AddTypeToNamespaceTree(clrType, definitionSymbol);
        }
        else
        {
          definitionSymbol = GetOrCreateTypeSymbol(definition);
        }

        if (clrType.IsGenericTypeDefinition)
        {
          typeSymbol = definitionSymbol;
        }
        else
        {
          var genericArguments = clrType.GetGenericArguments();
          var argumentSymbols = new TypeSymbol[genericArguments.Length];
          for (var index = 0; index < genericArguments.Length; index++)
            argumentSymbols[index] = GetOrCreateTypeSymbol(genericArguments[index]);
          typeSymbol = definitionSymbol.Construct(argumentSymbols);
        }
      }
      else
      {
        var qualifiedName = (clrType.FullName ?? clrType.Name).Replace('+', '.');
        typeSymbol = TypeSymbol.CreateNamed(
            GetSimpleTypeName(clrType),
            qualifiedName,
            !clrType.IsValueType,
            clrType);
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

}
