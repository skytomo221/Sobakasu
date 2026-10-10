using System;
using System.Collections.Generic;
using System.Reflection;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{


    internal sealed class UdonApiMemberModel
    {
        private readonly Type _syntheticDeclaringType;

        public Type SurfaceType { get; }
        public Type ClrDeclaringType => Member?.DeclaringType ?? _syntheticDeclaringType;
        public MemberInfo Member { get; }
        public MethodBase Callable { get; }
        public string OperatorName { get; }
        public IReadOnlyList<Type> OperatorParameterTypes { get; }
        public Type OperatorReturnType { get; }
        public bool IsSyntheticOperator => Member == null && OperatorName != null;
        public bool IsOperator => OperatorName != null;
        public UdonApiMemberKind Kind { get; }
        public string ExternSignature { get; }
        public string DisplaySignature { get; }
        public bool IsUdonExposed { get; }
        public string SkipReason { get; set; }

        public string SurfaceTypeName => GetTypeName(SurfaceType);
        public string ClrDeclaringTypeName => GetTypeName(ClrDeclaringType);
        public string DeclaringTypeName => ClrDeclaringTypeName;
        public string MemberName => Member?.Name ?? OperatorName;
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
            if (callable is MethodInfo operatorMethod && operatorMethod.IsSpecialName &&
                operatorMethod.Name.StartsWith("op_", StringComparison.Ordinal) &&
                callable is MethodInfo method)
            {
                OperatorName = method.Name;
                var parameters = method.GetParameters();
                var parameterTypes = new Type[parameters.Length];
                for (var index = 0; index < parameters.Length; index++)
                    parameterTypes[index] = parameters[index].ParameterType;
                OperatorParameterTypes = parameterTypes;
                OperatorReturnType = method.ReturnType;
            }
            Kind = kind;
            ExternSignature = externSignature ?? string.Empty;
            DisplaySignature = displaySignature ?? string.Empty;
            IsUdonExposed = isUdonExposed;
        }

        public UdonApiMemberModel(
            Type surfaceType,
            Type declaringType,
            string operatorName,
            IReadOnlyList<Type> parameterTypes,
            Type returnType,
            string externSignature)
        {
            SurfaceType = surfaceType ?? throw new ArgumentNullException(nameof(surfaceType));
            _syntheticDeclaringType = declaringType ??
                throw new ArgumentNullException(nameof(declaringType));
            OperatorName = operatorName ?? throw new ArgumentNullException(nameof(operatorName));
            OperatorParameterTypes = parameterTypes ??
                throw new ArgumentNullException(nameof(parameterTypes));
            OperatorReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            Kind = UdonApiMemberKind.StaticMethod;
            ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature));
            DisplaySignature = $"{GetTypeName(returnType)} {operatorName}(" +
                string.Join(", ", GetTypeNames(parameterTypes)) + ")";
            IsUdonExposed = true;
        }

        private static IEnumerable<string> GetTypeNames(IReadOnlyList<Type> types)
        {
            for (var index = 0; index < types.Count; index++)
                yield return GetTypeName(types[index]);
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
                    if (member.IsSyntheticOperator)
                    {
                        return $"{GetClrTypeName(member.ClrDeclaringType)}.{member.OperatorName}(" +
                            string.Join(",", GetTypeNames(member.OperatorParameterTypes)) + ")";
                    }
                    return Format(member.Callable);
            }
        }

        private static IEnumerable<string> GetTypeNames(IReadOnlyList<Type> types)
        {
            for (var index = 0; index < types.Count; index++)
                yield return GetClrTypeName(types[index]);
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

}
