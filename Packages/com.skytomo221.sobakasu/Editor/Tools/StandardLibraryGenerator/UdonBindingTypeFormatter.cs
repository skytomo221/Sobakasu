using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.UdonApi;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    /// <summary>
    /// Formats both Catalog-projected TypeRefs used by the production generator and
    /// System.Type values used by the retained reflection test/Catalog-generation adapter.
    /// </summary>
    internal sealed class UdonBindingTypeFormatter
    {
        private static readonly IReadOnlyDictionary<string, string> Builtins =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["System.String"] = "string",
                ["System.Boolean"] = "bool",
                ["System.Char"] = "char",
                ["System.SByte"] = "i8",
                ["System.Byte"] = "u8",
                ["System.Int16"] = "i16",
                ["System.UInt16"] = "u16",
                ["System.Int32"] = "i32",
                ["System.UInt32"] = "u32",
                ["System.Int64"] = "i64",
                ["System.UInt64"] = "u64",
                ["System.Single"] = "f32",
                ["System.Double"] = "f64",
                ["System.Object"] = "object",
                ["System.Void"] = "unit"
            };

        private readonly Func<string, bool> _isExternTypeAvailable;
        private readonly IReadOnlyDictionary<string, UdonBindingSourceTypeRecord> _catalogTypes;
        private readonly ISet<string> _unexposedTypeNames;

        public UdonBindingTypeFormatter(Func<string, bool> isExternTypeAvailable = null)
        {
            _isExternTypeAvailable = isExternTypeAvailable;
        }

        public UdonBindingTypeFormatter(UdonBindingSourceModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            _catalogTypes = model.TypesByRuntimeName;
            _unexposedTypeNames = model.UnexposedTypeNames;
        }

        public static bool IsCanonicalPrimitive(string runtimeName, out string name) =>
            Builtins.TryGetValue(runtimeName ?? string.Empty, out name);

        public bool CanDeclareType(UdonBindingSourceTypeRecord type, out string reason)
        {
            if (type == null)
            {
                reason = "The catalog type is missing.";
                return false;
            }
            if (Builtins.ContainsKey(type.runtimeName))
            {
                reason = null;
                return true;
            }
            if (type.genericArity > 0)
            {
                reason = "Generic CLR types are not supported by external type bindings.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(type.clrNamespace))
            {
                reason = "Types without a CLR namespace are not supported by the current extern catalog.";
                return false;
            }
            if (!TryGetSourcePath(type.runtimeName, type.clrNamespace, out var path))
            {
                reason = "The catalog type has an invalid CLR source path.";
                return false;
            }
            foreach (var segment in path.Split('.'))
            {
                var identifier = RemoveMetadataArity(segment);
                if (!SobakasuIdentifierFacts.IsNormalIdentifier(identifier))
                {
                    reason = $"'{identifier}' is not a valid Sobakasu type identifier.";
                    return false;
                }
            }
            if (_catalogTypes != null &&
                (!_catalogTypes.ContainsKey(type.runtimeName) ||
                 _unexposedTypeNames.Contains(type.runtimeName)))
            {
                reason = "The type is not available in the current Sobakasu extern catalog.";
                return false;
            }
            reason = null;
            return true;
        }

        public bool TryFormat(
            ExternTypeRef type,
            string hostRuntimeName,
            IReadOnlyList<UdonApiGenericParameterRecord> genericParameters,
            out string formatted,
            out string reason)
        {
            formatted = null;
            if (type == null)
            {
                reason = "The ABI type is missing.";
                return false;
            }

            switch (type.kind)
            {
                case "Named":
                    if (type.runtimeName == hostRuntimeName)
                    {
                        formatted = "Self";
                        reason = null;
                        return true;
                    }
                    if (Builtins.TryGetValue(type.runtimeName ?? string.Empty, out formatted))
                    {
                        reason = null;
                        return true;
                    }
                    if (_catalogTypes != null &&
                        (!_catalogTypes.TryGetValue(type.runtimeName ?? string.Empty, out var named) ||
                         _unexposedTypeNames.Contains(type.runtimeName)))
                    {
                        reason = $"Type '{type.runtimeName}' is not ABI-available in the catalog.";
                        return false;
                    }
                    if (_catalogTypes != null)
                        return FormatNamed(named.runtimeName, named.clrNamespace, out formatted, out reason);
                    return FormatNamed(type.runtimeName, GetNamespace(type.runtimeName), out formatted, out reason);

                case "Array":
                    if (type.element == null)
                    {
                        reason = "Array element metadata is missing.";
                        return false;
                    }
                    if (!TryFormat(type.element, hostRuntimeName, genericParameters,
                            out var element, out reason))
                        return false;
                    formatted = $"[{element}]";
                    reason = null;
                    return true;

                case "GenericParameter":
                    if (genericParameters != null &&
                        type.ordinal >= 0 && type.ordinal < genericParameters.Count)
                    {
                        formatted = genericParameters[type.ordinal].name;
                        reason = null;
                        return true;
                    }
                    formatted = $"T{type.ordinal}";
                    reason = null;
                    return true;

                case "ConstructedGeneric":
                    if (type.definition?.runtimeName == null)
                    {
                        reason = "Constructed generic definition metadata is missing.";
                        return false;
                    }
                    if (!TryFormat(type.definition, hostRuntimeName, genericParameters,
                            out var definition, out reason))
                        return false;
                    var arguments = type.arguments ?? new List<ExternTypeRef>();
                    var formattedArguments = new string[arguments.Count];
                    for (var index = 0; index < arguments.Count; index++)
                    {
                        if (!TryFormat(arguments[index], hostRuntimeName, genericParameters,
                                out formattedArguments[index], out reason))
                            return false;
                    }
                    formatted = $"{definition}<{string.Join(", ", formattedArguments)}>";
                    reason = null;
                    return true;

                default:
                    reason = $"Unknown catalog type reference kind '{type.kind}'.";
                    return false;
            }
        }

        public bool CanDeclareType(Type type, out string reason)
        {
            if (type == null)
            {
                reason = "The CLR type is missing.";
                return false;
            }
            if (UdonApiReflectionUtilities.TryGetBuiltInTypeInfo(type, out var builtInType))
            {
                if (builtInType.IsCanonicalExternPrimitive)
                {
                    reason = null;
                    return true;
                }
                reason = "Built-in Sobakasu types cannot declare an external type binding.";
                return false;
            }
            if (type.IsGenericType || type.IsGenericTypeDefinition || type.ContainsGenericParameters)
            {
                reason = "Generic CLR types are not supported by external type bindings.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(type.Namespace))
            {
                reason = "Types without a CLR namespace are not supported by the current extern catalog.";
                return false;
            }
            var wrapperName = UdonApiReflectionUtilities.GetSimpleTypeName(type);
            if (!SobakasuNameUtility.IsIdentifier(wrapperName))
            {
                reason = $"'{wrapperName}' is not a valid Sobakasu type identifier.";
                return false;
            }
            if (_isExternTypeAvailable != null &&
                !_isExternTypeAvailable(type.FullName ?? type.Name))
            {
                reason = "The type is not available in the current Sobakasu extern catalog.";
                return false;
            }
            reason = null;
            return true;
        }

        public bool TryFormat(Type type, Type declaringType, out string typeName, out string reason)
        {
            typeName = null;
            if (type == null)
            {
                reason = "The CLR type is missing.";
                return false;
            }
            if (type.IsPointer)
            {
                reason = $"Pointer type '{GetDisplayTypeName(type)}' is unsupported.";
                return false;
            }
            if (type.IsByRef)
                type = type.GetElementType();
            if (type.IsGenericParameter)
            {
                typeName = type.Name;
                reason = null;
                return true;
            }
            if (UdonApiReflectionUtilities.TryGetBuiltInTypeInfo(type, out var builtInType))
            {
                typeName = builtInType.Name;
                reason = null;
                return true;
            }
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1 || type != type.GetElementType().MakeArrayType())
                {
                    reason = $"Array shape '{GetDisplayTypeName(type)}' is unsupported.";
                    return false;
                }
                if (!TryFormat(type.GetElementType(), declaringType, out var element, out reason))
                    return false;
                typeName = $"[{element}]";
                reason = null;
                return true;
            }
            if (type.IsGenericType)
            {
                var definition = type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition();
                var definitionName = (definition.FullName ?? definition.Name)
                    .Replace("+", "::").Replace(".", "::");
                var tick = definitionName.IndexOf('`');
                if (tick >= 0)
                    definitionName = definitionName.Substring(0, tick);
                var arguments = type.GetGenericArguments();
                var formattedArguments = new string[arguments.Length];
                for (var index = 0; index < arguments.Length; index++)
                {
                    if (!TryFormat(arguments[index], declaringType, out formattedArguments[index], out reason))
                        return false;
                }
                typeName = $"{definitionName}<{string.Join(", ", formattedArguments)}>";
                reason = null;
                return true;
            }
            if (_isExternTypeAvailable != null &&
                !_isExternTypeAvailable(type.FullName ?? type.Name))
            {
                reason = $"Type '{GetDisplayTypeName(type)}' is not available in the current Sobakasu extern catalog.";
                return false;
            }
            if (type == declaringType)
            {
                typeName = "Self";
                reason = null;
                return true;
            }
            var runtimeName = (type.FullName ?? type.Name).Replace('+', '.');
            foreach (var segment in runtimeName.Split('.'))
            {
                if (!SobakasuNameUtility.IsIdentifier(segment))
                {
                    reason = $"Type '{runtimeName}' cannot be represented as a Sobakasu type path.";
                    return false;
                }
            }
            typeName = runtimeName.Replace(".", "::");
            reason = null;
            return true;
        }

        private static bool FormatNamed(string runtimeName, string clrNamespace, out string formatted, out string reason)
        {
            if (!TryGetSourcePath(runtimeName, clrNamespace, out var path))
            {
                formatted = null;
                reason = $"Catalog type '{runtimeName}' has an invalid namespace boundary.";
                return false;
            }
            var qualified = string.IsNullOrEmpty(clrNamespace) ? path : clrNamespace + "." + path;
            var segments = qualified.Split('.');
            for (var index = 0; index < segments.Length; index++)
            {
                segments[index] = RemoveMetadataArity(segments[index]);
                if (!SobakasuIdentifierFacts.IsNormalIdentifier(segments[index]))
                {
                    formatted = null;
                    reason = $"'{segments[index]}' is not a valid Sobakasu type identifier.";
                    return false;
                }
            }
            formatted = string.Join("::", segments);
            reason = null;
            return true;
        }

        private static bool TryGetSourcePath(string runtimeName, string clrNamespace, out string path)
        {
            var prefix = string.IsNullOrEmpty(clrNamespace) ? string.Empty : clrNamespace + ".";
            if (runtimeName != null && runtimeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                path = runtimeName.Substring(prefix.Length).Replace('+', '.');
                return true;
            }
            path = null;
            return false;
        }

        private static string GetNamespace(string runtimeName)
        {
            if (string.IsNullOrEmpty(runtimeName))
                return string.Empty;
            var root = runtimeName.Split('+')[0];
            var dot = root.LastIndexOf('.');
            return dot < 0 ? string.Empty : root.Substring(0, dot);
        }

        private static string RemoveMetadataArity(string value)
        {
            var tick = value?.IndexOf('`') ?? -1;
            return tick < 0 ? value : value.Substring(0, tick);
        }

        private static string GetDisplayTypeName(Type type) =>
            (type.FullName ?? type.Name).Replace('+', '.');
    }
}
