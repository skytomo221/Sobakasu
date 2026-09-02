using System;
using System.Collections.Generic;
using System.Reflection;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
  internal interface IUdonApiExposure
  {
    IReadOnlyCollection<string> ExposedSignatures { get; }
    bool IsTypeExposed(Type type);
    bool IsMemberExposed(string externSignature);
  }

  internal sealed class InstalledUdonApiExposure : IUdonApiExposure
  {
    private readonly UdonExposedNodeCache _cache;

    public InstalledUdonApiExposure(UdonExposedNodeCache cache)
    {
      _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public IReadOnlyCollection<string> ExposedSignatures => _cache.ExposedSignatures;

    public bool IsTypeExposed(Type type)
    {
      return _cache.IsTypeExposed(type);
    }

    public bool IsMemberExposed(string externSignature)
    {
      return _cache.IsExposed(externSignature);
    }
  }

  internal sealed class UdonBindingTypeFormatter
  {
    private readonly ExternCatalog _externCatalog;

    public UdonBindingTypeFormatter(ExternCatalog externCatalog = null)
    {
      _externCatalog = externCatalog;
    }

    public bool CanDeclareType(Type type, out string reason)
    {
      if (type == null)
      {
        reason = "The CLR type is missing.";
        return false;
      }

      if (ReflectionExternCatalogBuilder.TryGetBuiltInTypeSymbol(type, out var builtInType))
      {
        if (builtInType.IsCanonicalExternPrimitive)
        {
          reason = null;
          return true;
        }

        reason = "Built-in Sobakasu types cannot declare an external type binding.";
        return false;
      }

      if (type.IsGenericType ||
          type.IsGenericTypeDefinition ||
          type.ContainsGenericParameters)
      {
        reason = "Generic CLR types are not supported by external type bindings.";
        return false;
      }

      if (string.IsNullOrWhiteSpace(type.Namespace))
      {
        reason = "Types without a CLR namespace are not supported by the current extern catalog.";
        return false;
      }

      var wrapperName = ReflectionExternCatalogBuilder.GetSimpleTypeName(type);
      if (!SobakasuNameUtility.IsIdentifier(wrapperName))
      {
        reason = $"'{wrapperName}' is not a valid Sobakasu type identifier.";
        return false;
      }

      if (_externCatalog != null &&
          !_externCatalog.TryGetTypeSymbol(type, out _))
      {
        reason = "The type is not available in the current Sobakasu extern catalog.";
        return false;
      }

      reason = null;
      return true;
    }

    public bool TryFormat(
        Type type,
        Type declaringType,
        out string typeName,
        out string reason)
    {
      typeName = null;
      if (type == null)
      {
        reason = "The CLR type is missing.";
        return false;
      }

      if (type.IsPointer)
      {
        reason = $"Pointer type '{GetDisplayTypeName(type)}' is unsupported.";
        return false;
      }

      if (type.IsByRef)
        type = type.GetElementType();

      if (type.IsGenericParameter)
      {
        typeName = type.Name;
        reason = null;
        return true;
      }

      if (ReflectionExternCatalogBuilder.TryGetBuiltInTypeSymbol(
              type,
              out var builtInType))
      {
        typeName = builtInType.Name;
        reason = null;
        return true;
      }

      if (type.IsArray)
      {
        if (type.GetArrayRank() != 1 ||
            type != type.GetElementType().MakeArrayType())
        {
          reason = $"Array shape '{GetDisplayTypeName(type)}' is unsupported.";
          return false;
        }

        if (!TryFormat(
                type.GetElementType(),
                declaringType,
                out var elementName,
                out reason))
        {
          return false;
        }

        typeName = $"[{elementName}]";
        return true;
      }

      if (type.IsGenericType)
      {
        var definition = type.IsGenericTypeDefinition
            ? type
            : type.GetGenericTypeDefinition();
        var definitionName = (definition.FullName ?? definition.Name).Replace('+', '.');
        var tickIndex = definitionName.IndexOf('`');
        if (tickIndex >= 0)
          definitionName = definitionName.Substring(0, tickIndex);
        var arguments = type.GetGenericArguments();
        var formattedArguments = new string[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
          if (!TryFormat(arguments[index], declaringType,
                  out formattedArguments[index], out reason))
            return false;
        }
        typeName = $"{definitionName}<{string.Join(", ", formattedArguments)}>";
        reason = null;
        return true;
      }

      if (_externCatalog != null &&
          !_externCatalog.TryGetTypeSymbol(type, out _))
      {
        reason =
            $"Type '{GetDisplayTypeName(type)}' is not available in the current Sobakasu extern catalog.";
        return false;
      }

      if (type == declaringType)
      {
        typeName = "Self";
        reason = null;
        return true;
      }

      var qualifiedName = (type.FullName ?? type.Name).Replace('+', '.');
      var segments = qualifiedName.Split('.');
      foreach (var segment in segments)
      {
        if (!SobakasuNameUtility.IsIdentifier(segment))
        {
          reason = $"Type '{qualifiedName}' cannot be represented as a Sobakasu type path.";
          return false;
        }
      }

      typeName = qualifiedName;
      reason = null;
      return true;
    }

    private static string GetDisplayTypeName(Type type)
    {
      return (type.FullName ?? type.Name).Replace('+', '.');
    }
  }

  internal sealed class UdonApiDiscovery
  {
    private readonly IUdonApiExposure _exposure;
    private readonly UdonBindingTypeFormatter _typeFormatter;

    public UdonApiDiscovery(
        IUdonApiExposure exposure,
        UdonBindingTypeFormatter typeFormatter)
    {
      _exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
      _typeFormatter = typeFormatter ??
          throw new ArgumentNullException(nameof(typeFormatter));
    }

    public UdonApiModel Discover()
    {
      var types = new HashSet<Type>();
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        if (assembly.IsDynamic)
          continue;

        foreach (var type in GetLoadableTypes(assembly))
        {
          if (type != null && (type.IsPublic || type.IsNestedPublic))
            types.Add(type);
        }
      }

      var sortedTypes = new List<Type>(types);
      sortedTypes.Sort(CompareTypes);
      return Discover(sortedTypes);
    }

    public UdonApiModel Discover(IReadOnlyList<Type> candidateTypes)
    {
      if (candidateTypes == null)
        throw new ArgumentNullException(nameof(candidateTypes));

      var exposedTypes = new List<Type>();
      var seenTypes = new HashSet<Type>();
      foreach (var type in candidateTypes)
      {
        if (type == null ||
            (!type.IsPublic && !type.IsNestedPublic) ||
            !seenTypes.Add(type) ||
            !_exposure.IsTypeExposed(type))
        {
          continue;
        }

        exposedTypes.Add(type);
      }

      exposedTypes.Sort(CompareTypes);
      var models = new List<UdonApiTypeModel>();
      foreach (var type in exposedTypes)
        models.Add(DiscoverType(type));

      return new UdonApiModel(
          models,
          new List<string>(_exposure.ExposedSignatures));
    }

    private UdonApiTypeModel DiscoverType(Type type)
    {
      var wrapperName = ReflectionExternCatalogBuilder.TryGetBuiltInTypeSymbol(
          type,
          out var builtInType)
          ? builtInType.Name
          : ReflectionExternCatalogBuilder.GetSimpleTypeName(type);
      var model = new UdonApiTypeModel(type, wrapperName);
      if (!_typeFormatter.CanDeclareType(type, out var typeReason))
        model.SkipReason = typeReason;

      try
      {
        DiscoverMethods(model);
        DiscoverConstructors(model);
        DiscoverProperties(model);
        DiscoverFields(model);
        DiscoverEvents(model);
      }
      catch (Exception exception)
      {
        model.SkipReason =
            $"Reflection failed while enumerating members: {exception.GetType().Name}: {exception.Message}";
      }

      model.SortMembers();
      if (!model.IsGenerated)
      {
        model.SkipGeneratedMembers(
            $"Declaring type was skipped: {model.SkipReason}");
      }

      return model;
    }

    private void DiscoverMethods(UdonApiTypeModel type)
    {
      const BindingFlags flags =
          BindingFlags.Public |
          BindingFlags.Static |
          BindingFlags.Instance |
          BindingFlags.FlattenHierarchy;

      foreach (var method in type.ClrType.GetMethods(flags))
      {
        if (method.IsSpecialName &&
            !method.Name.StartsWith("op_", StringComparison.Ordinal))
        {
          continue;
        }

        AddMethod(
            type,
            method,
            method,
            method.IsStatic
                ? UdonApiMemberKind.StaticMethod
                : UdonApiMemberKind.InstanceMethod);
      }
    }

    private void DiscoverConstructors(UdonApiTypeModel type)
    {
      const BindingFlags flags =
          BindingFlags.Public |
          BindingFlags.Instance |
          BindingFlags.DeclaredOnly;

      foreach (var constructor in type.ClrType.GetConstructors(flags))
      {
        var externSignature =
            UdonExternSignatureFormatter.GetUdonMethodName(constructor);
        var isUdonExposed = _exposure.IsMemberExposed(externSignature);
        var member = new UdonApiMemberModel(
            type.ClrType,
            constructor,
            constructor,
            UdonApiMemberKind.Constructor,
            externSignature,
            FormatCallable(constructor),
            isUdonExposed);

        if (TryGetUnsupportedCallableReason(constructor, out var reason) ||
            !AreSignatureTypesSupported(constructor, out reason) ||
            !isUdonExposed)
        {
          member.SkipReason = reason ??
              "The computed constructor signature is not exposed to Udon.";
        }

        type.AddMember(member);
      }
    }

    private void DiscoverProperties(UdonApiTypeModel type)
    {
      const BindingFlags flags =
          BindingFlags.Public |
          BindingFlags.Static |
          BindingFlags.Instance |
          BindingFlags.FlattenHierarchy;

      foreach (var property in type.ClrType.GetProperties(flags))
      {
        var indexerReason = property.GetIndexParameters().Length > 0
            ? "Indexed properties are not supported by the current Sobakasu extern syntax."
            : null;

        if (property.GetMethod != null && property.GetMethod.IsPublic)
        {
          AddMethod(
              type,
              property,
              property.GetMethod,
              UdonApiMemberKind.PropertyGetter,
              indexerReason);
        }

        if (property.SetMethod != null && property.SetMethod.IsPublic)
        {
          AddMethod(
              type,
              property,
              property.SetMethod,
              UdonApiMemberKind.PropertySetter,
              indexerReason);
        }
      }
    }

    private void DiscoverFields(UdonApiTypeModel type)
    {
      const BindingFlags flags =
          BindingFlags.Public |
          BindingFlags.Static |
          BindingFlags.Instance |
          BindingFlags.FlattenHierarchy;

      foreach (var field in type.ClrType.GetFields(flags))
      {
        AddField(type, field, isSetter: false);
        if (!field.IsInitOnly && !field.IsLiteral)
          AddField(type, field, isSetter: true);
      }
    }

    private void DiscoverEvents(UdonApiTypeModel type)
    {
      const BindingFlags flags =
          BindingFlags.Public |
          BindingFlags.Static |
          BindingFlags.Instance |
          BindingFlags.FlattenHierarchy;

      foreach (var eventInfo in type.ClrType.GetEvents(flags))
      {
        type.AddMember(new UdonApiMemberModel(
            type.ClrType,
            eventInfo,
            null,
            UdonApiMemberKind.Event,
            string.Empty,
            $"event {GetTypeName(eventInfo.EventHandlerType)} {eventInfo.Name}",
            false)
        {
          SkipReason =
              "CLR events are not supported by the current Sobakasu extern syntax."
        });
      }
    }

    private void AddMethod(
        UdonApiTypeModel type,
        MemberInfo publicMember,
        MethodInfo method,
        UdonApiMemberKind kind,
        string initialSkipReason = null)
    {
      var externSignature = UdonExternSignatureFormatter.GetUdonMethodName(method);
      var isUdonExposed = _exposure.IsMemberExposed(externSignature);
      var member = new UdonApiMemberModel(
          type.ClrType,
          publicMember,
          method,
          kind,
          externSignature,
          FormatCallable(method),
          isUdonExposed);
      var reason = initialSkipReason;

      if (reason == null &&
          ReflectionExternCatalogBuilder.TryGetUnsupportedMethodReason(
              method,
              out var unsupportedReason))
      {
        reason = unsupportedReason;
      }

      if (reason == null)
        AreSignatureTypesSupported(method, out reason);

      if (reason == null && !isUdonExposed)
      {
        reason = "The computed extern signature is not exposed to Udon.";
      }

      member.SkipReason = reason;
      type.AddMember(member);
    }

    private void AddField(
        UdonApiTypeModel type,
        FieldInfo field,
        bool isSetter)
    {
      var externSignature = ReflectionExternCatalogBuilder.BuildFieldExternSignature(
          field,
          isSetter);
      var isUdonExposed = _exposure.IsMemberExposed(externSignature);
      var member = new UdonApiMemberModel(
          type.ClrType,
          field,
          null,
          isSetter
              ? UdonApiMemberKind.FieldSetter
              : UdonApiMemberKind.FieldGetter,
          externSignature,
          $"{GetTypeName(field.FieldType)} {field.Name}",
          isUdonExposed);

      if (!_typeFormatter.TryFormat(
              field.FieldType,
              type.ClrType,
              out _,
              out var reason))
      {
        member.SkipReason = reason;
      }
      else if (!_exposure.IsTypeExposed(field.FieldType))
      {
        member.SkipReason = "The field type is not exposed to Udon.";
      }
      else if (!isUdonExposed)
      {
        member.SkipReason = isSetter
            ? "The computed field setter signature is not exposed to Udon."
            : "The computed field getter signature is not exposed to Udon.";
      }

      type.AddMember(member);
    }

    private bool AreSignatureTypesSupported(
        MethodBase callable,
        out string reason)
    {
      if (callable is MethodInfo method &&
          !IsSignatureTypeSupported(method.ReturnType, callable.DeclaringType, out reason))
      {
        return false;
      }

      if (callable is ConstructorInfo &&
          !IsSignatureTypeSupported(callable.DeclaringType, callable.DeclaringType, out reason))
      {
        return false;
      }

      foreach (var parameter in callable.GetParameters())
      {
        if (!IsSignatureTypeSupported(
                parameter.ParameterType,
                callable.DeclaringType,
                out reason))
        {
          return false;
        }
      }

      reason = null;
      return true;
    }

    private bool IsSignatureTypeSupported(
        Type signatureType,
        Type declaringType,
        out string reason)
    {
      var exposedType = signatureType.IsByRef
          ? signatureType.GetElementType()
          : signatureType;
      if (!_typeFormatter.TryFormat(
              exposedType,
              declaringType,
              out _,
              out reason))
      {
        return false;
      }

      if (!IsSignaturePatternExposed(exposedType))
      {
        reason = $"Signature type '{GetTypeName(signatureType)}' is not exposed to Udon.";
        return false;
      }

      reason = null;
      return true;
    }

    private bool IsSignaturePatternExposed(Type type)
    {
      if (type.IsByRef)
        type = type.GetElementType();
      if (type.IsGenericParameter)
        return true;
      if (type.IsArray)
        return type.GetArrayRank() == 1 && IsSignaturePatternExposed(type.GetElementType());
      if (type.IsGenericType)
      {
        foreach (var argument in type.GetGenericArguments())
        {
          if (!IsSignaturePatternExposed(argument))
            return false;
        }
        return true;
      }
      return _exposure.IsTypeExposed(type);
    }

    private static bool TryGetUnsupportedCallableReason(
        MethodBase callable,
        out string reason)
    {
      if (callable is MethodInfo method &&
          ReflectionExternCatalogBuilder.TryGetUnsupportedMethodReason(
              method,
              out reason))
      {
        return true;
      }

      foreach (var parameter in callable.GetParameters())
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

    private static string FormatCallable(MethodBase callable)
    {
      var parameters = callable.GetParameters();
      var parameterNames = new string[parameters.Length];
      for (var index = 0; index < parameters.Length; index++)
      {
        var modifier = parameters[index].IsOut
            ? "out "
            : parameters[index].ParameterType.IsByRef
                ? "ref "
                : string.Empty;
        parameterNames[index] =
            $"{modifier}{GetTypeName(parameters[index].ParameterType)} {parameters[index].Name}";
      }

      var declaringType = GetTypeName(callable.DeclaringType);
      if (callable is ConstructorInfo)
        return $"{declaringType}({string.Join(", ", parameterNames)})";

      var method = (MethodInfo)callable;
      var staticPrefix = method.IsStatic ? "static " : string.Empty;
      return
          $"{staticPrefix}{GetTypeName(method.ReturnType)} " +
          $"{declaringType}.{method.Name}({string.Join(", ", parameterNames)})";
    }

    private static string GetTypeName(Type type)
    {
      if (type == null)
        return "<unknown>";
      return (type.FullName ?? type.Name).Replace('+', '.');
    }

    private static int CompareTypes(Type left, Type right)
    {
      var result = string.CompareOrdinal(
          left.FullName ?? left.Name,
          right.FullName ?? right.Name);
      return result != 0
          ? result
          : string.CompareOrdinal(left.Assembly.FullName, right.Assembly.FullName);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
      try
      {
        return assembly.GetTypes();
      }
      catch (ReflectionTypeLoadException exception)
      {
        return exception.Types;
      }
    }
  }
}
