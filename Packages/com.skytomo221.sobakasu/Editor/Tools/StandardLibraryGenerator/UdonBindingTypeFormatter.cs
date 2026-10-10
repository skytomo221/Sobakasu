using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    internal sealed class UdonBindingTypeFormatter
    {
        private static readonly IReadOnlyDictionary<string, string> Builtins = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["System.String"] = "string", ["System.Boolean"] = "bool", ["System.Char"] = "char",
            ["System.SByte"] = "i8", ["System.Byte"] = "u8", ["System.Int16"] = "i16", ["System.UInt16"] = "u16",
            ["System.Int32"] = "i32", ["System.UInt32"] = "u32", ["System.Int64"] = "i64", ["System.UInt64"] = "u64",
            ["System.Single"] = "f32", ["System.Double"] = "f64", ["System.Object"] = "object", ["System.Void"] = "unit"
        };

        private readonly UdonBindingSourceModel _model;
        public UdonBindingTypeFormatter(UdonBindingSourceModel model) { _model = model ?? throw new ArgumentNullException(nameof(model)); }
        public static bool IsCanonicalPrimitive(string runtimeName, out string name) => Builtins.TryGetValue(runtimeName ?? string.Empty, out name);

        public bool CanDeclareType(UdonApiTypeRecord type, out string reason)
        {
            if (type == null) { reason = "The catalog type is missing."; return false; }
            if (Builtins.ContainsKey(type.runtimeName)) { reason = null; return true; }
            if (type.genericArity > 0) { reason = "Generic CLR types are not supported by external type bindings."; return false; }
            if (string.IsNullOrWhiteSpace(type.clrNamespace)) { reason = "Types without a CLR namespace are not supported by the current extern catalog."; return false; }
            if (!TryGetSourcePath(type, out var path)) { reason = "The catalog type has an invalid CLR source path."; return false; }
            foreach (var segment in path.Split('.'))
                if (!SobakasuIdentifierFacts.IsNormalIdentifier(segment)) { reason = $"'{segment}' is not a valid Sobakasu type identifier."; return false; }
            if (!_model.TypesByRuntimeName.ContainsKey(type.runtimeName) || _model.UnexposedTypeNames.Contains(type.runtimeName)) { reason = "The type is not available in the current Sobakasu extern catalog."; return false; }
            reason = null; return true;
        }

        public bool TryFormat(ExternTypeRef type, string hostRuntimeName, IReadOnlyList<UdonApiGenericParameterRecord> genericParameters, out string formatted, out string reason)
        {
            formatted = null;
            if (type == null) { reason = "The ABI type is missing."; return false; }
            switch (type.kind)
            {
                case "Named":
                    if (type.runtimeName == hostRuntimeName) { formatted = "Self"; reason = null; return true; }
                    if (Builtins.TryGetValue(type.runtimeName ?? string.Empty, out formatted)) { reason = null; return true; }
                    if (!_model.TypesByRuntimeName.TryGetValue(type.runtimeName ?? string.Empty, out var named) || _model.UnexposedTypeNames.Contains(type.runtimeName)) { reason = $"Type '{type.runtimeName}' is not ABI-available in the catalog."; return false; }
                    return FormatNamed(named, out formatted, out reason);
                case "Array":
                    if (type.element == null) { reason = "Array element metadata is missing."; return false; }
                    if (!TryFormat(type.element, hostRuntimeName, genericParameters, out var element, out reason)) return false;
                    formatted = $"[{element}]"; return true;
                case "GenericParameter":
                    var ordinal = type.ordinal;
                    if (genericParameters != null && ordinal >= 0 && ordinal < genericParameters.Count)
                    { formatted = genericParameters[ordinal].name; reason = null; return true; }
                    reason = "Generic parameter metadata is missing."; return false;
                case "ConstructedGeneric":
                    if (type.definition == null || !_model.TypesByRuntimeName.TryGetValue(type.definition.runtimeName ?? string.Empty, out var definition) || _model.UnexposedTypeNames.Contains(type.definition.runtimeName))
                    { reason = "Constructed generic definition is not ABI-available in the catalog."; return false; }
                    if (!FormatNamed(definition, out var definitionName, out reason)) return false;
                    var args = new string[type.arguments?.Count ?? 0];
                    for (var index = 0; index < args.Length; index++) if (!TryFormat(type.arguments[index], hostRuntimeName, genericParameters, out args[index], out reason)) return false;
                    formatted = $"{definitionName}<{string.Join(", ", args)}>"; return true;
                default:
                    reason = $"Unknown catalog type reference kind '{type.kind}'."; return false;
            }
        }

        private static bool FormatNamed(UdonApiTypeRecord record, out string formatted, out string reason)
        {
            if (!TryGetSourcePath(record, out var typePath)) { formatted = null; reason = $"Catalog type '{record.runtimeName}' has an invalid namespace boundary."; return false; }
            var path = string.IsNullOrEmpty(record.clrNamespace) ? typePath : record.clrNamespace + "." + typePath;
            var segments = path.Split('.');
            for (var index = 0; index < segments.Length; index++)
            {
                var tick = segments[index].IndexOf('`');
                if (tick >= 0) segments[index] = segments[index].Substring(0, tick);
                if (!SobakasuIdentifierFacts.IsNormalIdentifier(segments[index])) { formatted = null; reason = $"'{segments[index]}' is not a valid Sobakasu type identifier."; return false; }
            }
            formatted = string.Join("::", segments); reason = null; return true;
        }

        private static bool TryGetSourcePath(UdonApiTypeRecord record, out string path)
        {
            var prefix = string.IsNullOrEmpty(record.clrNamespace) ? string.Empty : record.clrNamespace + ".";
            if (record.runtimeName != null && record.runtimeName.StartsWith(prefix, StringComparison.Ordinal))
            { path = record.runtimeName.Substring(prefix.Length).Replace('+', '.'); return true; }
            path = null; return false;
        }
    }
}
