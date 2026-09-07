using System;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler
{
    public static class SobakasuTypeMapper
    {
        public static Type ToSystemType(TypeKind type) => ToSystemType(type, null);

        public static Type ToSystemType(TypeKind type, string runtimeTypeName)
        {
            if (type == TypeKind.Array)
            {
                if (string.IsNullOrEmpty(runtimeTypeName) || !runtimeTypeName.EndsWith("[]", StringComparison.Ordinal))
                    throw new NotSupportedException("Sobakasu array heap patches require their runtime array type name.");

                return ResolveRuntimeType(runtimeTypeName[..^2]).MakeArrayType();
            }

            return type switch
            {
                TypeKind.Bool => typeof(bool),
                TypeKind.Char => typeof(char),
                TypeKind.I8 => typeof(sbyte),
                TypeKind.U8 => typeof(byte),
                TypeKind.I16 => typeof(short),
                TypeKind.U16 => typeof(ushort),
                TypeKind.I32 => typeof(int),
                TypeKind.U32 => typeof(uint),
                TypeKind.I64 => typeof(long),
                TypeKind.U64 => typeof(ulong),
                TypeKind.F32 => typeof(float),
                TypeKind.F64 => typeof(double),
                TypeKind.String => typeof(string),
                TypeKind.Named when !string.IsNullOrEmpty(runtimeTypeName) => ResolveRuntimeType(runtimeTypeName),
                _ => throw new NotSupportedException($"Sobakasu heap patch type '{type}' is not supported.")
            };
        }

        internal static Type ResolveRuntimeType(string runtimeTypeName)
        {
            if (string.IsNullOrEmpty(runtimeTypeName))
                throw new ArgumentException("Runtime type name must not be empty.", nameof(runtimeTypeName));
            if (runtimeTypeName.EndsWith("[]", StringComparison.Ordinal))
                return ResolveRuntimeType(runtimeTypeName[..^2]).MakeArrayType();

            var resolved = Type.GetType(runtimeTypeName, throwOnError: false);
            if (resolved != null) return resolved;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolved = assembly.GetType(runtimeTypeName, throwOnError: false);
                if (resolved != null) return resolved;
            }
            throw new TypeLoadException($"Runtime type '{runtimeTypeName}' could not be resolved.");
        }
    }
}
