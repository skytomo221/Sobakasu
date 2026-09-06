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

}
