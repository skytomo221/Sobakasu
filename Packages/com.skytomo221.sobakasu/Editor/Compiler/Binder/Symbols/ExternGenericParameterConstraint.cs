using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ExternGenericParameterConstraint
    {
        public TypeSymbol Parameter { get; }
        public bool RequiresReferenceType { get; }
        public bool RequiresNonNullableValueType { get; }
        public bool RequiresDefaultConstructor { get; }
        public IReadOnlyList<TypeSymbol> ConstraintTypes { get; }

        public ExternGenericParameterConstraint(
            TypeSymbol parameter,
            bool requiresReferenceType,
            bool requiresNonNullableValueType,
            bool requiresDefaultConstructor,
            IReadOnlyList<TypeSymbol> constraintTypes)
        {
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            RequiresReferenceType = requiresReferenceType;
            RequiresNonNullableValueType = requiresNonNullableValueType;
            RequiresDefaultConstructor = requiresDefaultConstructor;
            ConstraintTypes = constraintTypes ?? Array.Empty<TypeSymbol>();
        }

        public ExternGenericParameterConstraint(
            TypeSymbol parameter,
            object ignoredDiscoveryAttributes,
            IReadOnlyList<TypeSymbol> constraintTypes)
            : this(parameter, false, false, false, constraintTypes)
        {
        }
    }
}
