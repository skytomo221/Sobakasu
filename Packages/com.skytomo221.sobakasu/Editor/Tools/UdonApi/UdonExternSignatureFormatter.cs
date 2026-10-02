using System;
using System.Collections.Generic;
using System.Reflection;
using VRC.Udon.Editor;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal static class UdonExternSignatureFormatter
    {
        private static readonly Dictionary<Type, string> TypeNameCache = new();
        private static readonly object TypeNameCacheGate = new();

        public static string GetUdonMethodName(MethodBase methodInfo)
        {
            if (methodInfo == null)
                throw new ArgumentNullException(nameof(methodInfo));

            var methodSourceType = methodInfo.DeclaringType;
            var sourceTypeName = methodSourceType?.FullName ??
                $"{methodSourceType?.Namespace}{methodSourceType?.Name}";
            var functionNamespace = SanitizeTypeName(sourceTypeName)
                .Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver")
                .Replace("UdonSharpUdonSharpBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

            var methodName = $"__{methodInfo.Name.Trim('_').TrimStart('.')}";
            var parameters = methodInfo.GetParameters();
            var parameterString = string.Empty;

            if (parameters.Length > 0)
            {
                parameterString = "_";
                foreach (var parameter in parameters)
                    parameterString += $"_{GetUdonTypeName(parameter.ParameterType)}";
            }
            else if (methodInfo.IsConstructor)
            {
                parameterString = "__";
            }

            var returnString = methodInfo.IsConstructor
                ? $"__{GetUdonTypeName(methodSourceType)}"
                : $"__{GetUdonTypeName(((MethodInfo)methodInfo).ReturnType)}";

            return $"{functionNamespace}.{methodName}{parameterString}{returnString}";
        }

        public static string GetUdonTypeName(Type externType)
        {
            if (externType == null)
                throw new ArgumentNullException(nameof(externType));

            lock (TypeNameCacheGate)
            {
                if (TypeNameCache.TryGetValue(externType, out var foundTypeName))
                    return foundTypeName;
            }

            var originalType = externType;
            var externTypeName = GetNameWithoutGenericArity(originalType);
            while (externType.IsArray || externType.IsByRef)
                externType = externType.GetElementType();

            var typeNamespace = externType.Namespace ?? string.Empty;
            if (externType.DeclaringType != null)
            {
                var declaringTypeNamespace = string.Empty;
                var declaringType = externType.DeclaringType;
                while (declaringType != null)
                {
                    declaringTypeNamespace = $"{declaringType.Name}.{declaringTypeNamespace}";
                    declaringType = declaringType.DeclaringType;
                }

                typeNamespace += $".{declaringTypeNamespace}";
            }

            if (externTypeName == "T" || externTypeName == "T[]")
                typeNamespace = string.Empty;

            var fullTypeName = SanitizeTypeName($"{typeNamespace}.{externTypeName}");
            foreach (var genericType in externType.GetGenericArguments())
                fullTypeName += GetUdonTypeName(genericType);

            if (fullTypeName == "SystemCollectionsGenericListT")
            {
                fullTypeName = "ListT";
            }
            else if (fullTypeName == "SystemCollectionsGenericIEnumerableT")
            {
                fullTypeName = "IEnumerableT";
            }

            lock (TypeNameCacheGate)
                TypeNameCache[originalType] = fullTypeName;

            return fullTypeName;
        }

        public static string SanitizeTypeName(string typeName)
        {
            return (typeName ?? string.Empty).Replace(",", "")
                .Replace(".", "")
                .Replace("[]", "Array")
                .Replace("&", "Ref")
                .Replace("+", "");
        }

        private static string GetNameWithoutGenericArity(Type type)
        {
            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            return tickIndex >= 0
                ? name[..tickIndex]
                : name;
        }
    }

}
