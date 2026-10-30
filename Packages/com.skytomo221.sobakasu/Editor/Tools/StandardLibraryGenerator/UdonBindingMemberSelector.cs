using System;
using System.Collections.Generic;
using System.Linq;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    // Configuration transport only. Policy consumes the immutable selector below.
    [Serializable]
    internal sealed class UdonBindingMemberSelectorRule
    {
        public string member;
        public string kind;
        public UdonBindingSelectorParameterRule[] parameters;
        public int genericArity;

        public UdonBindingMemberSelector ToSelector()
        {
            if (string.IsNullOrWhiteSpace(member) || member.Any(char.IsWhiteSpace) ||
                member.IndexOfAny(new[] { '(', ')', '@', '#' }) >= 0 || member.LastIndexOf('.') <= 0 || member.EndsWith(".", StringComparison.Ordinal))
                throw new UdonBindingConfigurationException("Selector member must be a fully-qualified logical member name without a parameter list.");
            var memberKind = kind switch
            {
                "instanceMethod" => UdonApiMemberKind.InstanceMethod,
                "propertyGetter" => UdonApiMemberKind.PropertyGetter,
                "propertySetter" => UdonApiMemberKind.PropertySetter,
                "fieldGetter" => UdonApiMemberKind.FieldGetter,
                "fieldSetter" => UdonApiMemberKind.FieldSetter,
                _ => throw new UdonBindingConfigurationException($"Unknown or unusable readonly receiver kind '{kind}'; an instance member is required.")
            };
            if (parameters == null && memberKind == UdonApiMemberKind.InstanceMethod)
                throw new UdonBindingConfigurationException("An instanceMethod selector requires 'parameters', including [] for no arguments.");
            if (genericArity < 0 || (genericArity != 0 && memberKind != UdonApiMemberKind.InstanceMethod))
                throw new UdonBindingConfigurationException("Invalid selector genericArity.");
            var arguments = parameters ?? Array.Empty<UdonBindingSelectorParameterRule>();
            if ((memberKind == UdonApiMemberKind.FieldGetter || memberKind == UdonApiMemberKind.FieldSetter) && arguments.Length != 0)
                throw new UdonBindingConfigurationException("A field selector cannot have parameters.");
            return new UdonBindingMemberSelector(member, memberKind, false,
                arguments.Select(argument => argument?.ToIdentity() ??
                    throw new UdonBindingConfigurationException("A selector parameter is null.")), genericArity);
        }
    }

    [Serializable]
    internal sealed class UdonBindingSelectorParameterRule
    {
        public UdonApiTypeRef type;
        public string passing;

        public UdonBindingParameterIdentity ToIdentity()
        {
            if (passing != null && passing != "ref" && passing != "out" && passing != "in")
                throw new UdonBindingConfigurationException($"Invalid selector parameter passing '{passing}'.");
            return new UdonBindingParameterIdentity(type, passing);
        }
    }

    // Owns snapshots of Catalog TypeRefs; neither parameter names nor params affect identity.
    internal sealed class UdonBindingParameterIdentity : IEquatable<UdonBindingParameterIdentity>
    {
        private readonly UdonApiTypeRef _type;
        private readonly string _passing;

        public UdonBindingParameterIdentity(UdonApiTypeRef type, string passing)
        {
            _type = Snapshot(type);
            _passing = passing;
        }

        private static UdonApiTypeRef Snapshot(UdonApiTypeRef type)
        {
            if (type == null)
                throw new UdonBindingConfigurationException("A selector parameter requires a non-null type.");
            if (type.named != null && !string.IsNullOrWhiteSpace(type.named))
                return UdonApiTypeRef.Named(type.named);
            if (type.array != null && (type.rank ?? 1) > 0)
                return new UdonApiTypeRef { array = Snapshot(type.array), rank = type.rank ?? 1 };
            if (type.typeParameter >= 0)
                return new UdonApiTypeRef { typeParameter = type.typeParameter };
            if (type.methodParameter >= 0)
                return new UdonApiTypeRef { methodParameter = type.methodParameter };
            if (!string.IsNullOrWhiteSpace(type.generic) && type.arguments?.Count > 0)
                return new UdonApiTypeRef { generic = type.generic, arguments = type.arguments.Select(Snapshot).ToList() };
            throw new UdonBindingConfigurationException("Invalid selector parameter TypeRef.");
        }

        private static bool TypeEquals(UdonApiTypeRef left, UdonApiTypeRef right)
        {
            if (left == null || right == null) return left == right;
            return left.named == right.named && left.rank == right.rank &&
                left.typeParameter == right.typeParameter && left.methodParameter == right.methodParameter &&
                left.generic == right.generic && TypeEquals(left.array, right.array) &&
                (left.arguments ?? new List<UdonApiTypeRef>()).SequenceEqual(
                    right.arguments ?? new List<UdonApiTypeRef>(), TypeComparer.Instance);
        }

        private sealed class TypeComparer : IEqualityComparer<UdonApiTypeRef>
        {
            public static readonly TypeComparer Instance = new();
            public bool Equals(UdonApiTypeRef left, UdonApiTypeRef right) => TypeEquals(left, right);
            public int GetHashCode(UdonApiTypeRef type) => TypeHash(type);
        }

        private static int TypeHash(UdonApiTypeRef type)
        {
            if (type == null) return 0;
            unchecked
            {
                var hash = type.named?.GetHashCode() ?? 0;
                hash = hash * 31 + (type.rank ?? 0);
                hash = hash * 31 + (type.typeParameter ?? -1);
                hash = hash * 31 + (type.methodParameter ?? -1);
                hash = hash * 31 + (type.generic?.GetHashCode() ?? 0);
                hash = hash * 31 + TypeHash(type.array);
                foreach (var argument in type.arguments ?? new List<UdonApiTypeRef>())
                    hash = hash * 31 + TypeHash(argument);
                return hash;
            }
        }

        public bool Equals(UdonBindingParameterIdentity other) =>
            other != null && _passing == other._passing && TypeEquals(_type, other._type);
        public override bool Equals(object obj) => Equals(obj as UdonBindingParameterIdentity);
        public override int GetHashCode() => TypeHash(_type) * 31 + (_passing?.GetHashCode() ?? 0);
        // Display only. Matching and duplicate detection never use this representation.
        public override string ToString() => Newtonsoft.Json.JsonConvert.SerializeObject(_type,
            new UdonApiTypeRefJsonConverter()) + (_passing == null ? "" : " " + _passing);
    }

    internal sealed class UdonBindingMemberSelector : IEquatable<UdonBindingMemberSelector>
    {
        public string Member { get; }
        public UdonApiMemberKind Kind { get; }
        public bool IsStatic { get; }
        public int GenericArity { get; }
        private readonly UdonBindingParameterIdentity[] _parameters;

        public UdonBindingMemberSelector(string member, UdonApiMemberKind kind, bool isStatic,
            IEnumerable<UdonBindingParameterIdentity> parameters, int genericArity)
        {
            Member = member;
            Kind = kind;
            IsStatic = isStatic;
            GenericArity = genericArity;
            _parameters = parameters.ToArray();
        }

        public static UdonBindingMemberSelector FromPhysical(UdonBindingSourceMember member)
        {
            // Accessor parameters identify index arguments, excluding the setter's implicit value.
            var count = member.Parameters.Count;
            if (member.Kind == UdonApiMemberKind.PropertySetter || member.Kind == UdonApiMemberKind.FieldSetter)
                count--;
            return new UdonBindingMemberSelector(member.HostRuntimeName + "." + member.Name,
                member.Kind, member.IsStatic, member.Parameters.Take(count).Select(parameter =>
                    new UdonBindingParameterIdentity(ToCatalogType(parameter.type), parameter.passingMode switch
                    {
                        "Ref" => "ref", "Out" => "out", "In" => "in", _ => null
                    })), member.GenericParameters.Count);
        }

        private static UdonApiTypeRef ToCatalogType(ExternTypeRef type) => type.kind switch
        {
            "Named" => UdonApiTypeRef.Named(type.runtimeName),
            "Array" or "UnsupportedArray" => new UdonApiTypeRef { array = ToCatalogType(type.element), rank = type.rank },
            "GenericParameter" => type.scope == "Method"
                ? new UdonApiTypeRef { methodParameter = type.ordinal }
                : new UdonApiTypeRef { typeParameter = type.ordinal },
            "ConstructedGeneric" => new UdonApiTypeRef { generic = type.definition.runtimeName,
                arguments = type.arguments.Select(ToCatalogType).ToList() },
            _ => throw new UdonBindingConfigurationException("Cannot identify an unsupported physical parameter type.")
        };

        public bool Equals(UdonBindingMemberSelector other) => other != null &&
            Member == other.Member && Kind == other.Kind && IsStatic == other.IsStatic &&
            GenericArity == other.GenericArity && _parameters.SequenceEqual(other._parameters);
        public override bool Equals(object obj) => Equals(obj as UdonBindingMemberSelector);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ((Member.GetHashCode() * 31 + (int)Kind) * 31 + GenericArity) * 31 + IsStatic.GetHashCode();
                foreach (var parameter in _parameters) hash = hash * 31 + parameter.GetHashCode();
                return hash;
            }
        }
        public override string ToString() => $"{Member} ({Kind}, static={IsStatic}, genericArity={GenericArity}, parameters=[{string.Join(", ", _parameters.Select(parameter => parameter.ToString()))}])";
    }
}
