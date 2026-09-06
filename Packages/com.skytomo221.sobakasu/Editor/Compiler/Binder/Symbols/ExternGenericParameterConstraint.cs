using System;
using System.Collections.Generic;
using System.Reflection;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ExternGenericParameterConstraint
    {
        public TypeSymbol Parameter { get; }
        public GenericParameterAttributes Attributes { get; }
        public IReadOnlyList<TypeSymbol> ConstraintTypes { get; }

        public ExternGenericParameterConstraint(
            TypeSymbol parameter,
            GenericParameterAttributes attributes,
            IReadOnlyList<TypeSymbol> constraintTypes)
        {
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            Attributes = attributes;
            ConstraintTypes = constraintTypes ?? Array.Empty<TypeSymbol>();
        }
    }
}
