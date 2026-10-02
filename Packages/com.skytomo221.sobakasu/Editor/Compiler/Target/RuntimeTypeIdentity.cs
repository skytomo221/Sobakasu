using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Target
{
    internal enum RuntimeTypeIdentityKind
    {
        Named,
        Array,
        ConstructedGeneric
    }

    // This is deliberately a value object.  Compiler symbols use it to describe
    // target runtime types without retaining a CLR Type or requiring reflection.
    internal sealed class RuntimeTypeIdentity : IEquatable<RuntimeTypeIdentity>
    {
        public RuntimeTypeIdentityKind Kind { get; }
        public string RuntimeName { get; }
        public RuntimeTypeIdentity ElementType { get; }
        public RuntimeTypeIdentity GenericDefinition { get; }
        public IReadOnlyList<RuntimeTypeIdentity> TypeArguments { get; }

        private RuntimeTypeIdentity(
            RuntimeTypeIdentityKind kind,
            string runtimeName,
            RuntimeTypeIdentity elementType,
            RuntimeTypeIdentity genericDefinition,
            IReadOnlyList<RuntimeTypeIdentity> typeArguments)
        {
            Kind = kind;
            RuntimeName = runtimeName ?? string.Empty;
            ElementType = elementType;
            GenericDefinition = genericDefinition;
            TypeArguments = typeArguments ?? System.Array.Empty<RuntimeTypeIdentity>();
        }

        public static RuntimeTypeIdentity Named(string runtimeName)
        {
            if (string.IsNullOrWhiteSpace(runtimeName))
                throw new ArgumentException("A runtime type name is required.", nameof(runtimeName));
            return new RuntimeTypeIdentity(RuntimeTypeIdentityKind.Named, runtimeName, null, null, null);
        }

        public static RuntimeTypeIdentity Array(RuntimeTypeIdentity elementType)
        {
            if (elementType == null)
                throw new ArgumentNullException(nameof(elementType));
            return new RuntimeTypeIdentity(RuntimeTypeIdentityKind.Array, elementType.RuntimeName + "[]", elementType, null, null);
        }

        public static RuntimeTypeIdentity ConstructedGeneric(
            RuntimeTypeIdentity genericDefinition,
            IReadOnlyList<RuntimeTypeIdentity> typeArguments)
        {
            if (genericDefinition == null)
                throw new ArgumentNullException(nameof(genericDefinition));
            if (typeArguments == null)
                throw new ArgumentNullException(nameof(typeArguments));

            var copiedArguments = new RuntimeTypeIdentity[typeArguments.Count];
            for (var index = 0; index < copiedArguments.Length; index++)
            {
                copiedArguments[index] = typeArguments[index] ??
                    throw new ArgumentException("Type arguments cannot be null.", nameof(typeArguments));
            }
            return new RuntimeTypeIdentity(
                RuntimeTypeIdentityKind.ConstructedGeneric,
                genericDefinition.RuntimeName,
                null,
                genericDefinition,
                copiedArguments);
        }

        public bool Equals(RuntimeTypeIdentity other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other is null || Kind != other.Kind ||
                !string.Equals(RuntimeName, other.RuntimeName, StringComparison.Ordinal))
                return false;
            if (!Equals(ElementType, other.ElementType) ||
                !Equals(GenericDefinition, other.GenericDefinition) ||
                TypeArguments.Count != other.TypeArguments.Count)
                return false;
            for (var index = 0; index < TypeArguments.Count; index++)
            {
                if (!Equals(TypeArguments[index], other.TypeArguments[index]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is RuntimeTypeIdentity other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(RuntimeName);
                hash = (hash * 397) ^ (ElementType?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (GenericDefinition?.GetHashCode() ?? 0);
                foreach (var argument in TypeArguments)
                    hash = (hash * 397) ^ argument.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => RuntimeName;
    }
}
