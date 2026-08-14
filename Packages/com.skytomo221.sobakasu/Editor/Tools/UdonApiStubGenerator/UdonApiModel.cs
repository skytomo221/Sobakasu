using System;
using System.Collections.Generic;
using System.Reflection;

namespace Skytomo221.Sobakasu.Tools.UdonApiStubGenerator
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
    public MemberInfo Member { get; }
    public MethodBase Callable { get; }
    public UdonApiMemberKind Kind { get; }
    public string ExternSignature { get; }
    public string DisplaySignature { get; }
    public string SkipReason { get; set; }

    public string DeclaringTypeName =>
        (Member.DeclaringType?.FullName ?? string.Empty).Replace('+', '.');
    public string MemberName => Member.Name;
    public string FullName => $"{DeclaringTypeName}.{MemberName}";
    public bool IsGenerated => string.IsNullOrEmpty(SkipReason);

    public UdonApiMemberModel(
        MemberInfo member,
        MethodBase callable,
        UdonApiMemberKind kind,
        string externSignature,
        string displaySignature)
    {
      Member = member ?? throw new ArgumentNullException(nameof(member));
      Callable = callable;
      Kind = kind;
      ExternSignature = externSignature ?? string.Empty;
      DisplaySignature = displaySignature ?? string.Empty;
    }

    public string GetSortKey()
    {
      return $"{(int)Kind:D2}|{MemberName}|{DisplaySignature}|{ExternSignature}";
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

    public UdonApiModel(IReadOnlyList<UdonApiTypeModel> types)
    {
      Types = types ?? throw new ArgumentNullException(nameof(types));
    }
  }

  [Serializable]
  internal sealed class UdonApiSkipRecord
  {
    public string full_name;
    public string declaring_type;
    public string member_kind;
    public string signature;
    public string extern_signature;
    public string reason;
  }

  [Serializable]
  internal sealed class UdonApiSkipReasonCount
  {
    public string reason;
    public int count;
  }

  [Serializable]
  internal sealed class UdonApiGenerationReport
  {
    public int types_discovered;
    public int types_generated;
    public int types_skipped;
    public int members_discovered;
    public int members_generated;
    public int members_skipped;
    public List<UdonApiSkipRecord> skipped_types = new();
    public List<UdonApiSkipRecord> skipped_members = new();
    public List<UdonApiSkipReasonCount> skip_reasons = new();
  }
}
