using System;
using System.Reflection;

namespace Skytomo221.Sobakasu.Tools.UdonApi
{
    internal readonly struct UdonApiBuiltInTypeInfo
    {
        public string Name { get; }
        public bool IsCanonicalExternPrimitive { get; }

        public UdonApiBuiltInTypeInfo(string name, bool isCanonicalExternPrimitive)
        {
            Name = name;
            IsCanonicalExternPrimitive = isCanonicalExternPrimitive;
        }
    }

    internal static class UdonApiReflectionUtilities
    {
        public static string GetSimpleTypeName(Type clrType)
        {
            var name = clrType.Name;
            var tickIndex = name.IndexOf('`');
            return tickIndex >= 0 ? name[..tickIndex] : name;
        }

        public static bool TryGetBuiltInTypeInfo(
            Type type,
            out UdonApiBuiltInTypeInfo info)
        {
            if (type == typeof(void)) info = new UdonApiBuiltInTypeInfo("unit", false);
            else if (type == typeof(string)) info = new UdonApiBuiltInTypeInfo("string", true);
            else if (type == typeof(bool)) info = new UdonApiBuiltInTypeInfo("bool", true);
            else if (type == typeof(char)) info = new UdonApiBuiltInTypeInfo("char", true);
            else if (type == typeof(sbyte)) info = new UdonApiBuiltInTypeInfo("i8", true);
            else if (type == typeof(byte)) info = new UdonApiBuiltInTypeInfo("u8", true);
            else if (type == typeof(short)) info = new UdonApiBuiltInTypeInfo("i16", true);
            else if (type == typeof(ushort)) info = new UdonApiBuiltInTypeInfo("u16", true);
            else if (type == typeof(int)) info = new UdonApiBuiltInTypeInfo("i32", true);
            else if (type == typeof(uint)) info = new UdonApiBuiltInTypeInfo("u32", true);
            else if (type == typeof(long)) info = new UdonApiBuiltInTypeInfo("i64", true);
            else if (type == typeof(ulong)) info = new UdonApiBuiltInTypeInfo("u64", true);
            else if (type == typeof(float)) info = new UdonApiBuiltInTypeInfo("f32", true);
            else if (type == typeof(double)) info = new UdonApiBuiltInTypeInfo("f64", true);
            else if (type == typeof(object)) info = new UdonApiBuiltInTypeInfo("object", false);
            else
            {
                info = default;
                return false;
            }

            return true;
        }

        public static bool TryGetUnsupportedMethodReason(MethodInfo method, out string reason)
        {
            if (method.ContainsGenericParameters && !method.IsGenericMethodDefinition)
            {
                reason = "Open generic declaring types are not supported.";
                return true;
            }
            if (method.ReturnType.IsByRef || method.ReturnType.IsPointer)
            {
                reason = "Pointer and by-ref return types are not supported in v1.";
                return true;
            }
            foreach (var parameter in method.GetParameters())
            {
                if (parameter.ParameterType.IsPointer)
                {
                    reason = "Pointer parameters are not supported.";
                    return true;
                }
                if ((parameter.Attributes & ParameterAttributes.HasFieldMarshal) != 0)
                {
                    reason = "Marshalled parameters are not supported in v1.";
                    return true;
                }
                if (Attribute.IsDefined(parameter, typeof(ParamArrayAttribute)))
                {
                    reason = "params parameters are not supported in v1.";
                    return true;
                }
            }
            reason = null;
            return false;
        }
    }
}
