using System;
using System.Collections.Generic;
using System.Reflection;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Tools.UdonApi
{
    // Physical Udon ABI rules shared by API discovery clients.  This deliberately
    // does not use ExternCatalog, which is a compiler-side projection.
    internal static class UdonApiSignatureUtilities
    {
        public static string BuildFieldExternSignature(FieldInfo field, bool isSetter)
        {
            var declaringType = UdonExternSignatureFormatter.GetUdonTypeName(field.DeclaringType);
            var fieldType = UdonExternSignatureFormatter.GetUdonTypeName(field.FieldType);
            return isSetter
                ? $"{declaringType}.__set_{field.Name}__{fieldType}"
                : $"{declaringType}.__get_{field.Name}__{fieldType}";
        }

        public static string BuildOperatorExternSignature(
            Type declaringType,
            string operatorName,
            IReadOnlyList<Type> parameterTypes,
            Type returnType)
        {
            var suffix = "__";
            for (var index = 0; index < parameterTypes.Count; index++)
            {
                if (index > 0)
                    suffix += "_";
                suffix += UdonExternSignatureFormatter.GetUdonTypeName(parameterTypes[index]);
            }
            suffix += $"__{UdonExternSignatureFormatter.GetUdonTypeName(returnType)}";
            return $"{UdonExternSignatureFormatter.GetUdonTypeName(declaringType)}.__{operatorName}{suffix}";
        }

        public static IReadOnlyList<string> GetOperatorNameVariants(string operatorName)
        {
            var names = new List<string> { operatorName };
            var alternate = operatorName switch
            {
                "op_Multiply" => "op_Multiplication",
                "op_Modulus" => "op_Remainder",
                "op_BitwiseAnd" => "op_LogicalAnd",
                "op_BitwiseOr" => "op_LogicalOr",
                "op_ExclusiveOr" => "op_LogicalXor",
                "op_UnaryPlus" => "op_UnaryAddition",
                "op_UnaryNegation" => "op_UnaryMinus",
                "op_OnesComplement" => "op_BitwiseNot",
                _ => null
            };
            if (!string.IsNullOrEmpty(alternate))
                names.Add(alternate);
            return names;
        }

        public static bool TryResolveOperatorExternSignature(
            MethodInfo method,
            Func<string, bool> isExposed,
            out string externSignature)
        {
            externSignature = null;
            if (method == null || isExposed == null ||
                !method.Name.StartsWith("op_", StringComparison.Ordinal))
                return false;

            var parameters = method.GetParameters();
            var parameterTypes = new Type[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
                parameterTypes[index] = parameters[index].ParameterType;
            foreach (var name in GetOperatorNameVariants(method.Name))
            {
                var candidate = BuildOperatorExternSignature(
                    method.DeclaringType,
                    name,
                    parameterTypes,
                    method.ReturnType);
                if (!isExposed(candidate))
                    continue;
                externSignature = candidate;
                return true;
            }
            return false;
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
