using System;
using System.Collections.Generic;
using System.Reflection;

namespace Skytomo221.Sobakasu.Tools.UdonApiStubGenerator
{
  internal enum UdonApiGeneratedPlacement
  {
    Impl,
    TopLevel
  }

  internal enum UdonApiGeneratedProjection
  {
    Raw,
    Maybe
  }

  internal sealed class UdonApiGeneratedMemberModel
  {
    private readonly Dictionary<int, UdonApiGeneratedProjection> _outProjections = new();

    public UdonApiMemberModel Physical { get; }
    public string FunctionName { get; set; }
    public UdonApiGeneratedProjection ReturnProjection { get; set; }
    public string SkipReason { get; set; }
    public bool IsExplicitlyExcluded { get; set; }
    public bool HasDeclarationCollision { get; set; }
    public bool IsGenerated => string.IsNullOrEmpty(SkipReason);

    public UdonApiGeneratedMemberModel(UdonApiMemberModel physical)
    {
      Physical = physical ?? throw new ArgumentNullException(nameof(physical));
      SkipReason = physical.SkipReason;
    }

    public UdonApiGeneratedProjection GetOutProjection(int parameterIndex)
    {
      return _outProjections.TryGetValue(parameterIndex, out var projection)
          ? projection
          : UdonApiGeneratedProjection.Raw;
    }

    public void SetOutProjection(
        int parameterIndex,
        UdonApiGeneratedProjection projection)
    {
      _outProjections[parameterIndex] = projection;
    }
  }

  internal sealed class UdonApiGeneratedTypeModel
  {
    private readonly List<UdonApiGeneratedMemberModel> _members = new();

    public UdonApiTypeModel Physical { get; }
    public string GeneratedNamespace { get; set; }
    public UdonApiGeneratedPlacement Placement { get; set; }
    public string WrapperName { get; set; }
    public string ModuleName { get; set; }
    public string RelativePath { get; set; }
    public string SkipReason { get; set; }
    public IReadOnlyList<UdonApiGeneratedMemberModel> Members => _members;
    public bool IsGenerated => string.IsNullOrEmpty(SkipReason);

    public UdonApiGeneratedTypeModel(UdonApiTypeModel physical)
    {
      Physical = physical ?? throw new ArgumentNullException(nameof(physical));
      SkipReason = physical.SkipReason;
    }

    public void AddMember(UdonApiGeneratedMemberModel member)
    {
      _members.Add(member ?? throw new ArgumentNullException(nameof(member)));
    }

    public void SkipGeneratedMembers(string reason)
    {
      foreach (var member in _members)
      {
        if (member.IsGenerated)
          member.SkipReason = reason;
      }
    }
  }

  internal sealed class UdonApiGeneratedModel
  {
    public IReadOnlyList<UdonApiGeneratedTypeModel> Types { get; }
    public IReadOnlyCollection<string> UdonExposedSignatures { get; }
    public UdonApiStubGenerationConfig Configuration { get; }
    public string ConfigurationPath { get; }

    public UdonApiGeneratedModel(
        IReadOnlyList<UdonApiGeneratedTypeModel> types,
        IReadOnlyCollection<string> udonExposedSignatures,
        UdonApiStubGenerationConfig configuration,
        string configurationPath)
    {
      Types = types ?? throw new ArgumentNullException(nameof(types));
      UdonExposedSignatures = udonExposedSignatures ??
          throw new ArgumentNullException(nameof(udonExposedSignatures));
      Configuration = configuration ??
          throw new ArgumentNullException(nameof(configuration));
      ConfigurationPath = configurationPath ?? string.Empty;
    }
  }

  internal sealed class UdonApiStubGenerationPolicy
  {
    private static readonly HashSet<string> MemberKinds = new(
        StringComparer.Ordinal)
    {
      "constructor",
      "static_method",
      "instance_method",
      "property_getter",
      "property_setter",
      "field_getter",
      "field_setter"
    };

    public UdonApiGeneratedModel Apply(
        UdonApiModel physicalModel,
        UdonApiStubGenerationConfig configuration,
        string configurationPath)
    {
      if (physicalModel == null)
        throw new ArgumentNullException(nameof(physicalModel));
      configuration ??= UdonApiStubGenerationConfig.CreateDefault();
      configuration.Normalize();
      ResetMatchCounts(configuration);

      var errors = ValidateConfiguration(configuration);
      var generatedTypes = new List<UdonApiGeneratedTypeModel>();
      foreach (var physicalType in physicalModel.Types)
      {
        var typeRule = MatchTypeRule(configuration, physicalType);
        var namespaceRule = MatchNamespaceRule(configuration, physicalType);
        var generatedType = new UdonApiGeneratedTypeModel(physicalType)
        {
          GeneratedNamespace = ResolveNamespace(
              configuration,
              physicalType,
              typeRule,
              namespaceRule),
          Placement = ResolvePlacement(configuration, physicalType, typeRule),
          WrapperName = string.IsNullOrWhiteSpace(typeRule?.name)
              ? physicalType.WrapperName
              : typeRule.name
        };

        foreach (var physicalMember in physicalType.Members)
        {
          var matchingRules = MatchMemberRules(
              configuration,
              physicalType,
              physicalMember);
          if (matchingRules.Count > 1)
          {
            errors.Add(
                $"More than one member rule matches '{physicalMember.DisplaySignature}'.");
          }
          var memberRule = matchingRules.Count == 1 ? matchingRules[0] : null;
          var generatedMember = ApplyMemberPolicy(
              configuration,
              physicalType,
              physicalMember,
              memberRule,
              errors);
          if (generatedType.Placement == UdonApiGeneratedPlacement.TopLevel &&
              generatedMember.IsGenerated &&
            !IsStaticMember(physicalMember))
          {
            generatedMember.SkipReason =
                "Instance members cannot be published as top-level functions; " +
                "only the type's static API is generated for top_level placement.";
          }
          generatedType.AddMember(generatedMember);
        }

        generatedTypes.Add(generatedType);
      }

      AddStaleRuleErrors(configuration, errors);
      ThrowIfErrors(errors);
      return new UdonApiGeneratedModel(
          generatedTypes,
          physicalModel.UdonExposedSignatures,
          configuration,
          configurationPath);
    }

    internal static string GetMemberKind(UdonApiMemberKind kind)
    {
      return SobakasuNameUtility.ToSnakeCase(kind.ToString());
    }

    internal static string[] GetClrParameterTypes(UdonApiMemberModel member)
    {
      if (member.Callable != null)
      {
        var parameters = member.Callable.GetParameters();
        var result = new string[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
          result[index] = GetClrTypeName(parameters[index].ParameterType);
        return result;
      }

      if (member.Kind == UdonApiMemberKind.FieldSetter &&
          member.Member is FieldInfo field)
      {
        return new[] { GetClrTypeName(field.FieldType) };
      }

      return Array.Empty<string>();
    }

    internal static Type GetNormalReturnType(UdonApiMemberModel member)
    {
      switch (member.Kind)
      {
        case UdonApiMemberKind.StaticMethod:
        case UdonApiMemberKind.InstanceMethod:
        case UdonApiMemberKind.PropertyGetter:
        case UdonApiMemberKind.PropertySetter:
          return ((MethodInfo)member.Callable).ReturnType;
        case UdonApiMemberKind.FieldGetter:
          return ((FieldInfo)member.Member).FieldType;
        default:
          return typeof(void);
      }
    }

    private static UdonApiGeneratedMemberModel ApplyMemberPolicy(
        UdonApiStubGenerationConfig configuration,
        UdonApiTypeModel type,
        UdonApiMemberModel member,
        UdonApiStubMemberRule rule,
        ICollection<string> errors)
    {
      var generated = new UdonApiGeneratedMemberModel(member);
      generated.FunctionName = ResolveFunctionName(
          configuration,
          member,
          rule);

      if (rule?.exclude == true)
      {
        generated.SkipReason =
            $"Excluded by generation policy for '{GetRuleIdentity(rule)}'.";
        generated.IsExplicitlyExcluded = true;
      }

      var normalReturnType = GetNormalReturnType(member);
      if (normalReturnType != typeof(void) && !normalReturnType.IsValueType)
      {
        generated.ReturnProjection = ParseProjection(
            rule?.@return ?? configuration.defaults.reference_return);
      }
      else
      {
        generated.ReturnProjection = UdonApiGeneratedProjection.Raw;
        if (string.Equals(rule?.@return, "maybe", StringComparison.Ordinal))
        {
          errors.Add(
              $"Member rule '{GetRuleIdentity(rule)}' applies maybe return projection " +
              $"to non-reference return type '{GetClrTypeName(normalReturnType)}'.");
        }
      }

      var parameters = member.Callable?.GetParameters() ??
          Array.Empty<ParameterInfo>();
      if (generated.ReturnProjection == UdonApiGeneratedProjection.Maybe &&
          HasParameterOutputs(parameters))
      {
        errors.Add(
            $"Member rule for '{member.DisplaySignature}' combines maybe return projection " +
            "with ref/out outputs. The current compiler can only project the complete extern result.");
      }

      for (var index = 0; index < parameters.Length; index++)
      {
        var parameter = parameters[index];
        if (!parameter.IsOut)
          continue;
        var elementType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()
            : parameter.ParameterType;
        var projection = !elementType.IsValueType
            ? ParseProjection(configuration.defaults.reference_out)
            : UdonApiGeneratedProjection.Raw;
        generated.SetOutProjection(index, projection);
      }

      if (rule != null)
      {
        foreach (var outRule in rule.@out)
        {
          var parameterIndex = FindParameter(parameters, outRule.parameter);
          if (parameterIndex < 0 || !parameters[parameterIndex].IsOut)
          {
            var passingMode = parameterIndex >= 0 &&
                parameters[parameterIndex].ParameterType.IsByRef
                ? "ref"
                : "non-out";
            errors.Add(
                $"Member rule '{GetRuleIdentity(rule)}' selects '{outRule.parameter}' " +
                $"for out projection, but it is {passingMode} or does not exist.");
            continue;
          }

          var parameter = parameters[parameterIndex];
          var elementType = parameter.ParameterType.IsByRef
              ? parameter.ParameterType.GetElementType()
              : parameter.ParameterType;
          var projection = ParseProjection(outRule.projection);
          if (projection == UdonApiGeneratedProjection.Maybe &&
              elementType.IsValueType)
          {
            errors.Add(
                $"Member rule '{GetRuleIdentity(rule)}' applies maybe out projection " +
                $"to non-reference parameter '{outRule.parameter}' of type " +
                $"'{GetClrTypeName(elementType)}'.");
            continue;
          }
          generated.SetOutProjection(parameterIndex, projection);
        }
      }

      return generated;
    }

    private static List<string> ValidateConfiguration(
        UdonApiStubGenerationConfig configuration)
    {
      var errors = new List<string>();
      if (!string.Equals(configuration.version, "1", StringComparison.Ordinal))
        errors.Add($"Unsupported configuration version '{configuration.version}'. Expected '1'.");

      ValidateNamespace(
          configuration.defaults.@namespace,
          "defaults.namespace",
          errors);
      ValidateProjection(
          configuration.defaults.reference_return,
          "defaults.reference_return",
          errors);
      ValidateProjection(
          configuration.defaults.reference_out,
          "defaults.reference_out",
          errors);
      ValidatePlacement(
          configuration.defaults.static_class_placement,
          "defaults.static_class_placement",
          errors);

      var namespaceRules = new HashSet<string>(StringComparer.Ordinal);
      foreach (var rule in configuration.namespaces)
      {
        if (rule == null)
        {
          errors.Add("A namespace rule is null.");
          continue;
        }
        if (string.IsNullOrWhiteSpace(rule.clr_namespace))
          errors.Add("A namespace rule has an empty clr_namespace.");
        else if (!namespaceRules.Add(rule.clr_namespace))
          errors.Add($"Conflicting namespace rules target '{rule.clr_namespace}'.");
        if (rule.NamespaceSpecified && rule.@namespace != null)
        {
          ValidateNamespace(
              rule.@namespace,
              $"namespace rule '{rule.clr_namespace}'",
              errors);
        }
      }

      var typeRules = new HashSet<string>(StringComparer.Ordinal);
      foreach (var rule in configuration.types)
      {
        if (rule == null)
        {
          errors.Add("A type rule is null.");
          continue;
        }
        if (string.IsNullOrWhiteSpace(rule.type))
          errors.Add("A type rule has an empty type.");
        else if (!typeRules.Add(rule.type))
          errors.Add($"Conflicting type rules target '{rule.type}'.");
        if (!string.IsNullOrWhiteSpace(rule.@namespace))
          ValidateNamespace(rule.@namespace, $"type rule '{rule.type}'", errors);
        if (!string.IsNullOrWhiteSpace(rule.placement))
          ValidatePlacement(rule.placement, $"type rule '{rule.type}'", errors);
        if (!string.IsNullOrWhiteSpace(rule.name) &&
            !IsModuleIdentifier(rule.name))
        {
          errors.Add(
              $"Type rule '{rule.type}' has invalid Sobakasu type name '{rule.name}'.");
        }
      }

      var memberRules = new HashSet<string>(StringComparer.Ordinal);
      foreach (var rule in configuration.members)
      {
        if (rule == null)
        {
          errors.Add("A member rule is null.");
          continue;
        }
        if (string.IsNullOrWhiteSpace(rule.declaring_type))
          errors.Add("A member rule has an empty declaring_type.");
        if (!MemberKinds.Contains(rule.member_kind ?? string.Empty))
          errors.Add($"Member rule has invalid member_kind '{rule.member_kind}'.");
        if (string.IsNullOrWhiteSpace(rule.member))
          errors.Add("A member rule has an empty member name.");
        foreach (var parameterType in rule.parameter_types)
        {
          if (string.IsNullOrWhiteSpace(parameterType))
            errors.Add($"Member rule '{GetRuleIdentity(rule)}' has an empty parameter type.");
        }
        if (!string.IsNullOrWhiteSpace(rule.@return))
          ValidateProjection(rule.@return, $"member rule '{GetRuleIdentity(rule)}'", errors);
        if (!string.IsNullOrWhiteSpace(rule.name) &&
            !IsCallableIdentifier(rule.name))
        {
          errors.Add(
              $"Member rule '{GetRuleIdentity(rule)}' has invalid Sobakasu name '{rule.name}'.");
        }

        var outParameters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var outRule in rule.@out)
        {
          if (outRule == null)
          {
            errors.Add($"Member rule '{GetRuleIdentity(rule)}' has a null out rule.");
            continue;
          }
          if (string.IsNullOrWhiteSpace(outRule.parameter))
            errors.Add($"Member rule '{GetRuleIdentity(rule)}' has an empty out parameter.");
          else if (!outParameters.Add(outRule.parameter))
            errors.Add(
                $"Member rule '{GetRuleIdentity(rule)}' configures out parameter " +
                $"'{outRule.parameter}' more than once.");
          ValidateProjection(
              outRule.projection,
              $"out parameter '{outRule.parameter}' in '{GetRuleIdentity(rule)}'",
              errors);
        }

        var identity = GetRuleIdentity(rule);
        if (!memberRules.Add(identity))
          errors.Add($"Conflicting member rules target '{identity}'.");
      }

      return errors;
    }

    private static UdonApiStubTypeRule MatchTypeRule(
        UdonApiStubGenerationConfig configuration,
        UdonApiTypeModel type)
    {
      foreach (var rule in configuration.types)
      {
        if (!string.Equals(rule.type, type.QualifiedName, StringComparison.Ordinal))
          continue;
        rule.MatchCount++;
        return rule;
      }
      return null;
    }

    private static UdonApiStubNamespaceRule MatchNamespaceRule(
        UdonApiStubGenerationConfig configuration,
        UdonApiTypeModel type)
    {
      var clrNamespace = type.ClrType.Namespace ?? string.Empty;
      UdonApiStubNamespaceRule best = null;
      foreach (var rule in configuration.namespaces)
      {
        if (!IsNamespacePrefix(rule.clr_namespace, clrNamespace))
          continue;
        rule.MatchCount++;
        if (best == null || rule.clr_namespace.Length > best.clr_namespace.Length)
          best = rule;
      }
      return best;
    }

    private static List<UdonApiStubMemberRule> MatchMemberRules(
        UdonApiStubGenerationConfig configuration,
        UdonApiTypeModel type,
        UdonApiMemberModel member)
    {
      var matches = new List<UdonApiStubMemberRule>();
      var parameterTypes = GetClrParameterTypes(member);
      var kind = GetMemberKind(member.Kind);
      foreach (var rule in configuration.members)
      {
        if (!string.Equals(
                rule.declaring_type,
                type.QualifiedName,
                StringComparison.Ordinal) ||
            !string.Equals(rule.member_kind, kind, StringComparison.Ordinal) ||
            !string.Equals(rule.member, member.MemberName, StringComparison.Ordinal) ||
            !SequenceEqual(rule.parameter_types, parameterTypes))
        {
          continue;
        }
        rule.MatchCount++;
        matches.Add(rule);
      }
      return matches;
    }

    private static string ResolveNamespace(
        UdonApiStubGenerationConfig configuration,
        UdonApiTypeModel type,
        UdonApiStubTypeRule typeRule,
        UdonApiStubNamespaceRule namespaceRule)
    {
      if (!string.IsNullOrWhiteSpace(typeRule?.@namespace))
        return typeRule.@namespace;
      if (namespaceRule == null)
        return configuration.defaults.@namespace;
      var rootNamespace = namespaceRule.NamespaceSpecified
          ? namespaceRule.@namespace
          : configuration.defaults.@namespace;
      if (!namespaceRule.preserve_subnamespaces)
        return rootNamespace ?? string.Empty;

      var clrNamespace = type.ClrType.Namespace ?? string.Empty;
      if (clrNamespace.Length == namespaceRule.clr_namespace.Length)
        return rootNamespace ?? string.Empty;
      var suffix = clrNamespace.Substring(namespaceRule.clr_namespace.Length + 1);
      var segments = suffix.Split('.');
      for (var index = 0; index < segments.Length; index++)
      {
        segments[index] = SobakasuNameUtility.ToIdentifier(
            segments[index],
            $"namespace_{index}");
      }
      var relativeNamespace = string.Join(".", segments);
      return string.IsNullOrEmpty(rootNamespace)
          ? relativeNamespace
          : $"{rootNamespace}.{relativeNamespace}";
    }

    private static UdonApiGeneratedPlacement ResolvePlacement(
        UdonApiStubGenerationConfig configuration,
        UdonApiTypeModel type,
        UdonApiStubTypeRule typeRule)
    {
      if (!string.IsNullOrWhiteSpace(typeRule?.placement))
        return ParsePlacement(typeRule.placement);
      return IsStaticClass(type.ClrType)
          ? ParsePlacement(configuration.defaults.static_class_placement)
          : UdonApiGeneratedPlacement.Impl;
    }

    private static string ResolveFunctionName(
        UdonApiStubGenerationConfig configuration,
        UdonApiMemberModel member,
        UdonApiStubMemberRule rule)
    {
      if (!string.IsNullOrWhiteSpace(rule?.name))
        return rule.name;
      switch (member.Kind)
      {
        case UdonApiMemberKind.Constructor:
          return "new";
        case UdonApiMemberKind.PropertySetter:
        case UdonApiMemberKind.FieldSetter:
          var valueType = member.Member is PropertyInfo property
              ? property.PropertyType
              : ((FieldInfo)member.Member).FieldType;
          if (configuration.defaults.predicate_naming &&
              valueType == typeof(bool) &&
              TryGetPredicateStem(member.MemberName, out var setterStem))
          {
            return SobakasuNameUtility.ToIdentifier(
                $"set_{setterStem}",
                "set_value");
          }
          return SobakasuNameUtility.ToIdentifier(
              $"set_{member.MemberName}",
              "set_value");
      }

      var returnType = GetNormalReturnType(member);
      if (configuration.defaults.predicate_naming &&
          returnType == typeof(bool) &&
          !HasParameterOutputs(member.Callable?.GetParameters() ??
              Array.Empty<ParameterInfo>()) &&
          TryGetPredicateStem(member.MemberName, out var stem))
      {
        return SobakasuNameUtility.ToIdentifier(stem, "predicate") + "?";
      }

      return SobakasuNameUtility.ToIdentifier(member.MemberName, "member");
    }

    private static bool TryGetPredicateStem(string name, out string stem)
    {
      stem = null;
      if (string.IsNullOrEmpty(name) || name.Length <= 2 ||
          !(name.StartsWith("Is", StringComparison.Ordinal) ||
            name.StartsWith("is", StringComparison.Ordinal)) ||
          !char.IsUpper(name[2]))
      {
        return false;
      }
      stem = name.Substring(2);
      return true;
    }

    private static void AddStaleRuleErrors(
        UdonApiStubGenerationConfig configuration,
        ICollection<string> errors)
    {
      foreach (var rule in configuration.namespaces)
      {
        if (rule.MatchCount == 0)
          errors.Add($"Namespace rule '{rule.clr_namespace}' did not match any discovered type.");
      }
      foreach (var rule in configuration.types)
      {
        if (rule.MatchCount == 0)
          errors.Add($"Type rule '{rule.type}' did not match any discovered type.");
      }
      foreach (var rule in configuration.members)
      {
        if (rule.MatchCount == 0)
          errors.Add($"Member rule '{GetRuleIdentity(rule)}' did not match any discovered member.");
      }
    }

    private static void ResetMatchCounts(UdonApiStubGenerationConfig configuration)
    {
      foreach (var rule in configuration.namespaces)
      {
        if (rule != null)
          rule.MatchCount = 0;
      }
      foreach (var rule in configuration.types)
      {
        if (rule != null)
          rule.MatchCount = 0;
      }
      foreach (var rule in configuration.members)
      {
        if (rule != null)
          rule.MatchCount = 0;
      }
    }

    private static void ValidateProjection(
        string value,
        string location,
        ICollection<string> errors)
    {
      if (!string.Equals(value, "raw", StringComparison.Ordinal) &&
          !string.Equals(value, "maybe", StringComparison.Ordinal))
      {
        errors.Add($"{location} has invalid projection '{value}'. Expected raw or maybe.");
      }
    }

    private static void ValidatePlacement(
        string value,
        string location,
        ICollection<string> errors)
    {
      if (!string.Equals(value, "impl", StringComparison.Ordinal) &&
          !string.Equals(value, "top_level", StringComparison.Ordinal))
      {
        errors.Add($"{location} has invalid placement '{value}'. Expected impl or top_level.");
      }
    }

    private static void ValidateNamespace(
        string value,
        string location,
        ICollection<string> errors)
    {
      if (string.IsNullOrWhiteSpace(value))
      {
        errors.Add($"{location} has an empty Sobakasu namespace.");
        return;
      }
      var segments = value.Split('.');
      foreach (var segment in segments)
      {
        if (!IsModuleIdentifier(segment))
        {
          errors.Add(
              $"{location} has invalid Sobakasu namespace '{value}'. " +
              $"Segment '{segment}' is not a module identifier.");
          return;
        }
      }
    }

    private static bool IsModuleIdentifier(string value)
    {
      if (string.IsNullOrEmpty(value) ||
          !(value[0] == '_' || char.IsLetter(value[0])) ||
          !SobakasuNameUtility.IsIdentifier(value))
      {
        return false;
      }
      for (var index = 1; index < value.Length; index++)
      {
        if (value[index] != '_' && !char.IsLetterOrDigit(value[index]))
          return false;
      }
      return true;
    }

    private static bool IsCallableIdentifier(string value)
    {
      if (string.IsNullOrEmpty(value))
        return false;
      return value.EndsWith("?", StringComparison.Ordinal)
          ? value.Length > 1 && SobakasuNameUtility.IsIdentifier(
              value.Substring(0, value.Length - 1))
          : SobakasuNameUtility.IsIdentifier(value);
    }

    private static UdonApiGeneratedProjection ParseProjection(string value)
    {
      return string.Equals(value, "maybe", StringComparison.Ordinal)
          ? UdonApiGeneratedProjection.Maybe
          : UdonApiGeneratedProjection.Raw;
    }

    private static UdonApiGeneratedPlacement ParsePlacement(string value)
    {
      return string.Equals(value, "top_level", StringComparison.Ordinal)
          ? UdonApiGeneratedPlacement.TopLevel
          : UdonApiGeneratedPlacement.Impl;
    }

    private static bool IsStaticClass(Type type)
    {
      return type.IsAbstract && type.IsSealed;
    }

    private static bool IsStaticMember(UdonApiMemberModel member)
    {
      if (member.Callable is MethodInfo method)
        return method.IsStatic;
      return member.Member is FieldInfo field && field.IsStatic;
    }

    private static bool HasParameterOutputs(IReadOnlyList<ParameterInfo> parameters)
    {
      foreach (var parameter in parameters)
      {
        if (parameter.ParameterType.IsByRef &&
            (parameter.IsOut || !parameter.IsIn))
        {
          return true;
        }
      }
      return false;
    }

    private static int FindParameter(
        IReadOnlyList<ParameterInfo> parameters,
        string name)
    {
      for (var index = 0; index < parameters.Count; index++)
      {
        if (string.Equals(parameters[index].Name, name, StringComparison.Ordinal))
          return index;
      }
      return -1;
    }

    private static bool IsNamespacePrefix(string prefix, string value)
    {
      return string.Equals(prefix, value, StringComparison.Ordinal) ||
          (value.StartsWith(prefix, StringComparison.Ordinal) &&
           value.Length > prefix.Length &&
           value[prefix.Length] == '.');
    }

    private static bool SequenceEqual(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
      if (left.Count != right.Count)
        return false;
      for (var index = 0; index < left.Count; index++)
      {
        if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
          return false;
      }
      return true;
    }

    private static string GetRuleIdentity(UdonApiStubMemberRule rule)
    {
      if (rule == null)
        return "<default policy>";
      return
          $"{rule.declaring_type}|{rule.member_kind}|{rule.member}(" +
          $"{string.Join(",", rule.parameter_types ?? Array.Empty<string>())})";
    }

    private static string GetClrTypeName(Type type)
    {
      if (type == null)
        return "<unknown>";
      return (type.FullName ?? type.Name).Replace('+', '.');
    }

    private static void ThrowIfErrors(IReadOnlyCollection<string> errors)
    {
      if (errors.Count == 0)
        return;
      throw new UdonApiStubConfigurationException(
          "Udon API stub generation policy validation failed:\n- " +
          string.Join("\n- ", errors));
    }
  }
}
