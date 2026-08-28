using System;
using System.Collections.Generic;
using System.Reflection;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
  internal enum UdonApiMemberKind
  {
    Constructor,
    StaticMethod,
    InstanceMethod,
    PropertyGetter,
    PropertySetter,
    FieldGetter,
    FieldSetter,
    Event
  }

  internal sealed class UdonApiMemberModel
  {
    public Type SurfaceType { get; }
    public Type ClrDeclaringType => Member.DeclaringType;
    public MemberInfo Member { get; }
    public MethodBase Callable { get; }
    public UdonApiMemberKind Kind { get; }
    public string ExternSignature { get; }
    public string DisplaySignature { get; }
    public bool IsUdonExposed { get; }
    public string SkipReason { get; set; }

    public string SurfaceTypeName => GetTypeName(SurfaceType);
    public string ClrDeclaringTypeName => GetTypeName(ClrDeclaringType);
    public string DeclaringTypeName => ClrDeclaringTypeName;
    public string MemberName => Member.Name;
    public string SurfaceFullName => $"{SurfaceTypeName}.{MemberName}";
    public string PhysicalFullName => $"{ClrDeclaringTypeName}.{MemberName}";
    public string FullName => PhysicalFullName;
    public bool IsGenerated => string.IsNullOrEmpty(SkipReason);

    public UdonApiMemberModel(
        Type surfaceType,
        MemberInfo member,
        MethodBase callable,
        UdonApiMemberKind kind,
        string externSignature,
        string displaySignature,
        bool isUdonExposed)
    {
      SurfaceType = surfaceType ?? throw new ArgumentNullException(nameof(surfaceType));
      Member = member ?? throw new ArgumentNullException(nameof(member));
      Callable = callable;
      Kind = kind;
      ExternSignature = externSignature ?? string.Empty;
      DisplaySignature = displaySignature ?? string.Empty;
      IsUdonExposed = isUdonExposed;
    }

    public string GetSortKey()
    {
      return $"{(int)Kind:D2}|{MemberName}|{DisplaySignature}|{ExternSignature}";
    }

    private static string GetTypeName(Type type)
    {
      return (type?.FullName ?? type?.Name ?? string.Empty).Replace('+', '.');
    }
  }

  internal static class ClrMemberId
  {
    public static string Format(UdonApiMemberModel member)
    {
      if (member == null)
        throw new ArgumentNullException(nameof(member));

      switch (member.Kind)
      {
        case UdonApiMemberKind.PropertyGetter:
        case UdonApiMemberKind.PropertySetter:
        case UdonApiMemberKind.FieldGetter:
        case UdonApiMemberKind.FieldSetter:
        case UdonApiMemberKind.Event:
          return $"{GetClrTypeName(member.Member.DeclaringType)}.{member.Member.Name}";
        default:
          return Format(member.Callable);
      }
    }

    public static string Format(MethodBase callable)
    {
      if (callable == null)
        throw new ArgumentNullException(nameof(callable));

      var declaringType = GetClrTypeName(callable.DeclaringType);
      var memberName = callable.IsConstructor
          ? declaringType
          : $"{declaringType}.{callable.Name}";
      var parameters = callable.GetParameters();
      var parameterTypes = new string[parameters.Length];
      for (var index = 0; index < parameters.Length; index++)
        parameterTypes[index] = GetClrTypeName(parameters[index].ParameterType);
      return $"{memberName}({string.Join(",", parameterTypes)})";
    }

    public static string Format(MemberInfo member)
    {
      if (member == null)
        throw new ArgumentNullException(nameof(member));
      if (member is MethodBase callable)
        return Format(callable);
      if (member is PropertyInfo || member is FieldInfo || member is EventInfo)
        return $"{GetClrTypeName(member.DeclaringType)}.{member.Name}";
      throw new ArgumentException(
          $"Member kind '{member.MemberType}' has no canonical CLR member ID.",
          nameof(member));
    }

    public static string GetClrTypeName(Type type)
    {
      if (type == null)
        return "<unknown>";
      return (type.FullName ?? type.Name).Replace('+', '.');
    }
  }

  internal sealed class UdonApiTypeModel
  {
    private readonly List<UdonApiMemberModel> _members = new();

    public Type ClrType { get; }
    public string QualifiedName { get; }
    public string WrapperName { get; }
    public IReadOnlyList<UdonApiMemberModel> Members => _members;
    public string SkipReason { get; set; }
    public string RelativePath { get; set; }
    public bool IsGenerated => string.IsNullOrEmpty(SkipReason);

    public UdonApiTypeModel(Type clrType, string wrapperName)
    {
      ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
      QualifiedName = (clrType.FullName ?? clrType.Name).Replace('+', '.');
      WrapperName = wrapperName ?? throw new ArgumentNullException(nameof(wrapperName));
    }

    public void AddMember(UdonApiMemberModel member)
    {
      _members.Add(member ?? throw new ArgumentNullException(nameof(member)));
    }

    public void SortMembers()
    {
      _members.Sort((left, right) => string.CompareOrdinal(
          left.GetSortKey(),
          right.GetSortKey()));
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

  internal sealed class UdonApiModel
  {
    public IReadOnlyList<UdonApiTypeModel> Types { get; }
    public IReadOnlyCollection<string> UdonExposedSignatures { get; }

    public UdonApiModel(
        IReadOnlyList<UdonApiTypeModel> types,
        IReadOnlyCollection<string> udonExposedSignatures)
    {
      Types = types ?? throw new ArgumentNullException(nameof(types));
      UdonExposedSignatures = udonExposedSignatures ??
          throw new ArgumentNullException(nameof(udonExposedSignatures));
    }
  }

  [Serializable]
  internal sealed class UdonApiSkipRecord
  {
    public string full_name;
    public string declaring_type;
    public string surface_type;
    public string clr_declaring_type;
    public string member_kind;
    public string signature;
    public string extern_signature;
    public string reason;
    public bool is_udon_exposed;
    public List<string> surface_types = new();
    public List<string> generated_surface_types = new();
    public List<string> reasons = new();
    public List<UdonApiSurfaceFailureRecord> surface_failures = new();
  }

  [Serializable]
  internal sealed class UdonApiSurfaceFailureRecord
  {
    public string surface_type;
    public string reason;
  }

  [Serializable]
  internal sealed class UdonApiPhysicalRecord
  {
    public string extern_signature;
    public string physical_full_name;
    public string clr_declaring_type;
    public string member_kind;
    public string signature;
    public List<string> surface_types = new();
    public List<string> generated_surface_types = new();
    public bool is_udon_exposed;
    public bool is_covered;
    public List<string> reasons = new();
    public List<UdonApiSurfaceFailureRecord> surface_failures = new();
  }

  [Serializable]
  internal sealed class UdonApiSkipReasonCount
  {
    public string reason;
    public int count;
  }

  [Serializable]
  internal sealed class UdonApiGeneratedTypeRecord
  {
    public string clr_declaring_type;
    public string sobakasu_namespace;
    public string placement;
    public string generated_file;
  }

  [Serializable]
  internal sealed class UdonApiGenerationReport
  {
    public string configuration_path;
    public string configuration_version;
    public int types_discovered;
    public int types_generated;
    public int types_skipped;
    public int members_discovered;
    public int members_generated;
    public int members_skipped;
    // members_* are retained for compatibility and count Sobakasu API surfaces.
    public int member_surfaces_discovered;
    public int member_surfaces_generated;
    public int member_surfaces_skipped;
    public int udon_signatures_discovered;
    public int udon_signatures_exposed;
    public int udon_signatures_covered;
    public int udon_signatures_unsupported;
    public double udon_api_coverage_percent;
    // Unmatched installed nodes are reported but excluded from the denominator because
    // the selected reflection scope cannot identify a CLR member for them.
    public int udon_exposed_unmatched_signatures_count;
    public List<string> udon_exposed_unmatched_signatures = new();
    public List<UdonApiPhysicalRecord> udon_api = new();
    public List<UdonApiSkipRecord> skipped_types = new();
    public List<UdonApiSkipRecord> skipped_members = new();
    public List<UdonApiSkipReasonCount> skip_reasons = new();
    public List<UdonApiSkipReasonCount> type_skip_reasons = new();
    public List<UdonApiSkipReasonCount> surface_skip_reasons = new();
    public List<UdonApiSkipReasonCount> udon_unsupported_reasons = new();
    public int rules_configured;
    public int rules_matched;
    public List<string> unmatched_rules = new();
    public int explicit_exclusions;
    public int declaration_collisions;
    public int raw_return_count;
    public int maybe_return_count;
    public int raw_out_count;
    public int maybe_out_count;
    public int impl_type_count;
    public int top_level_static_type_count;
    public int namespaces_generated;
    public int namespace_rules_configured;
    public int namespace_rules_matched;
    public List<string> unmatched_namespace_rules = new();
    public List<UdonApiGeneratedTypeRecord> generated_types = new();
  }
}
