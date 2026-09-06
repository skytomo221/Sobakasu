using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal enum StateSynchronizationMode
    {
        None,
        Linear,
        Smooth
    }

    internal static class StateSynchronizationCompatibility
    {
        private static readonly HashSet<string> NoneNamedTypes = new()
    {
      "UnityEngine.Color",
      "UnityEngine.Color32",
      "UnityEngine.Vector2",
      "UnityEngine.Vector3",
      "UnityEngine.Vector4",
      "UnityEngine.Quaternion",
      "VRC.SDKBase.VRCUrl"
    };

        private static readonly HashSet<string> LinearNamedTypes = new()
    {
      "UnityEngine.Color",
      "UnityEngine.Color32",
      "UnityEngine.Vector2",
      "UnityEngine.Vector3",
      "UnityEngine.Quaternion"
    };

        private static readonly HashSet<string> SmoothNamedTypes = new()
    {
      "UnityEngine.Vector2",
      "UnityEngine.Vector3",
      "UnityEngine.Quaternion"
    };

        public static bool IsSupported(
            TypeSymbol type,
            StateSynchronizationMode mode)
        {
            if (type == null || type == TypeSymbol.Error)
                return false;

            return mode switch
            {
                StateSynchronizationMode.None => IsNoneSupported(type),
                StateSynchronizationMode.Linear => IsInterpolatedNumeric(type) ||
                    IsNamed(type, LinearNamedTypes),
                StateSynchronizationMode.Smooth => IsInterpolatedNumeric(type) ||
                    IsNamed(type, SmoothNamedTypes),
                _ => false
            };
        }

        public static string GetSourceName(StateSynchronizationMode mode)
        {
            return mode switch
            {
                StateSynchronizationMode.None => "none",
                StateSynchronizationMode.Linear => "linear",
                StateSynchronizationMode.Smooth => "smooth",
                _ => "unknown"
            };
        }

        private static bool IsNoneSupported(TypeSymbol type)
        {
            if (IsPrimitiveSyncType(type) || IsNamed(type, NoneNamedTypes))
                return true;

            return type.TypeKind == TypeKind.Array &&
                (IsPrimitiveSyncType(type.ElementType) || IsNamed(type.ElementType, NoneNamedTypes));
        }

        private static bool IsPrimitiveSyncType(TypeSymbol type)
        {
            return type.TypeKind is TypeKind.Bool or
                TypeKind.Char or
                TypeKind.I8 or
                TypeKind.U8 or
                TypeKind.I16 or
                TypeKind.U16 or
                TypeKind.I32 or
                TypeKind.U32 or
                TypeKind.I64 or
                TypeKind.U64 or
                TypeKind.F32 or
                TypeKind.F64 or
                TypeKind.String;
        }

        private static bool IsInterpolatedNumeric(TypeSymbol type)
        {
            return type.TypeKind is TypeKind.I8 or
                TypeKind.U8 or
                TypeKind.I16 or
                TypeKind.U16 or
                TypeKind.I32 or
                TypeKind.U32 or
                TypeKind.I64 or
                TypeKind.U64 or
                TypeKind.F32 or
                TypeKind.F64;
        }

        private static bool IsNamed(TypeSymbol type, ISet<string> supportedTypes)
        {
            return type.TypeKind == TypeKind.Named &&
                (supportedTypes.Contains(type.QualifiedName) ||
                 supportedTypes.Contains(type.RuntimeQualifiedName));
        }
    }

    internal sealed class StateVariableSymbol : VariableSymbol
    {
        public override SymbolKind Kind => SymbolKind.State;
        public bool IsPublic { get; }
        public StateSynchronizationMode? SynchronizationMode { get; }
        public bool IsSynchronized => SynchronizationMode.HasValue;
        public object InitialValue { get; }
        public TextSpan InitializerSpan { get; }
        public int Ordinal { get; }

        public StateVariableSymbol(
            string name,
            TypeSymbol type,
            bool isPublic,
            StateSynchronizationMode? synchronizationMode,
            object initialValue,
            TextSpan declarationSpan,
            TextSpan initializerSpan,
            int ordinal)
            : base(name, type, true, declarationSpan)
        {
            IsPublic = isPublic;
            SynchronizationMode = synchronizationMode;
            InitialValue = initialValue;
            InitializerSpan = initializerSpan;
            Ordinal = ordinal;
        }
    }
}
