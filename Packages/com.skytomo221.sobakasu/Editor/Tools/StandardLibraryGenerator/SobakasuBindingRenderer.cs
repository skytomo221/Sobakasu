using System;
using System.Collections.Generic;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    internal static class SobakasuNameUtility
    {
        public static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var result = new StringBuilder(value.Length + 8);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (!char.IsLetterOrDigit(current))
                {
                    AppendUnderscore(result);
                    continue;
                }

                if (char.IsUpper(current))
                {
                    var hasPrevious = index > 0;
                    var hasNext = index + 1 < value.Length;
                    var previous = hasPrevious ? value[index - 1] : '\0';
                    var next = hasNext ? value[index + 1] : '\0';
                    if (hasPrevious &&
                        (char.IsLower(previous) ||
                         char.IsDigit(previous) ||
                         (char.IsUpper(previous) && char.IsLower(next))))
                    {
                        AppendUnderscore(result);
                    }

                    result.Append(char.ToLowerInvariant(current));
                }
                else
                {
                    result.Append(char.ToLowerInvariant(current));
                }
            }

            return result.ToString().Trim('_');
        }

        public static string ToIdentifier(string value, string fallback)
        {
            var identifier = ToNormalIdentifier(value, fallback);
            while (!IsIdentifier(identifier))
                identifier += "_";

            return identifier;
        }

        public static string ToNormalIdentifier(string value, string fallback)
        {
            var identifier = ToSnakeCase(value);
            if (string.IsNullOrEmpty(identifier))
                identifier = fallback;
            if (char.IsDigit(identifier[0]))
                identifier = $"_{identifier}";

            while (!SobakasuIdentifierFacts.IsNormalIdentifier(identifier))
                identifier += "_";

            return identifier;
        }

        public static bool IsIdentifier(string value)
        {
            return SobakasuIdentifierFacts.IsBareIdentifier(value);
        }

        private static void AppendUnderscore(StringBuilder builder)
        {
            if (builder.Length > 0 && builder[^1] != '_')
                builder.Append('_');
        }
    }

    internal sealed class SobakasuBindingRenderer
    {
        private readonly UdonBindingTypeFormatter _typeFormatter;

        internal UdonBindingTypeFormatter TypeFormatter => _typeFormatter;

        public SobakasuBindingRenderer(UdonBindingTypeFormatter typeFormatter)
        {
            _typeFormatter = typeFormatter ??
                throw new ArgumentNullException(nameof(typeFormatter));
        }

        public string RenderType(UdonApiGeneratedTypeModel type)
        {
            return RenderType(type, includeMaybeImport: true);
        }

        internal string RenderType(
            UdonApiGeneratedTypeModel type,
            bool includeMaybeImport)
        {
            return RenderType(
                type,
                includeMaybeImport,
                includeLanguageItem: true,
                includeOperators: true);
        }

        internal string RenderType(
            UdonApiGeneratedTypeModel type,
            bool includeMaybeImport,
            bool includeLanguageItem,
            bool includeOperators)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var source = new StringBuilder();
            if (includeMaybeImport && RequiresMaybeImport(type))
                source.AppendLine("use maybe::Maybe;\n");
            var wroteDeclaration = false;
            if (type.Placement == UdonApiGeneratedPlacement.Impl)
            {
                RenderImpl(source, type, includeLanguageItem, includeOperators);
            }
            else if (type.Placement == UdonApiGeneratedPlacement.Type)
            {
                RenderExternalType(source, type);
            }
            else if (type.Placement == UdonApiGeneratedPlacement.Struct)
            {
                RenderExternStruct(source, type);
            }
            else if (type.Placement == UdonApiGeneratedPlacement.Enum)
            {
                RenderExternEnum(source, type);
            }
            else
            {
                foreach (var member in type.Members)
                {
                    if (!member.IsGenerated)
                        continue;
                    if (wroteDeclaration)
                        source.AppendLine();
                    RenderMember(source, type, member, string.Empty);
                    wroteDeclaration = true;
                }
            }

            return source.ToString().Replace("\r\n", "\n");
        }

        private static bool RequiresMaybeImport(UdonApiGeneratedTypeModel type)
        {
            foreach (var member in type.Members)
            {
                if (!member.IsGenerated)
                    continue;
                if (member.ReturnProjection == UdonApiGeneratedProjection.Maybe)
                    return true;

                var parameters = member.Physical.Parameters;
                for (var index = 0; index < parameters.Count; index++)
                {
                    if (parameters[index].passingMode == "Out" &&
                        member.GetOutProjection(index) == UdonApiGeneratedProjection.Maybe)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public string RenderNamespaceModule(
            IReadOnlyList<string> childModules,
            IReadOnlyList<UdonApiGeneratedTypeModel> typeModules,
            ISet<string> rootModuleNames)
        {
            if (childModules == null)
                throw new ArgumentNullException(nameof(childModules));
            if (typeModules == null)
                throw new ArgumentNullException(nameof(typeModules));
            if (rootModuleNames == null)
                throw new ArgumentNullException(nameof(rootModuleNames));

            var sortedChildren = new List<string>(childModules);
            sortedChildren.Sort(StringComparer.Ordinal);
            var sortedTypes = new List<UdonApiGeneratedTypeModel>(typeModules);
            sortedTypes.Sort((left, right) =>
            {
                var moduleComparison = string.CompareOrdinal(
              left.ModuleName,
              right.ModuleName);
                return moduleComparison != 0
              ? moduleComparison
              : string.CompareOrdinal(
                  left.Physical.QualifiedName,
                  right.Physical.QualifiedName);
            });

            var source = new StringBuilder();
            foreach (var childModule in sortedChildren)
            {
                source.Append("public module ");
                source.Append(childModule);
                source.AppendLine(";");
            }
            foreach (var type in sortedTypes)
            {
                source.Append("module ");
                source.Append(type.ModuleName);
                source.AppendLine(";");
            }

            if (sortedTypes.Count > 0)
                source.AppendLine();
            foreach (var type in sortedTypes)
            {
                if (!type.ShouldReExport)
                    continue;
                source.Append("public use ");
                if (type.Placement == UdonApiGeneratedPlacement.TopLevel)
                {
                    source.Append(type.ModuleName);
                }
                else
                {
                    var path = rootModuleNames.Contains(type.ModuleName)
                        ? $"{type.GeneratedNamespace}.{type.ModuleName}"
                        : type.ModuleName;
                    source.Append(FormatSobakasuPath(path));
                    source.Append("::");
                    source.Append(type.WrapperName);
                }
                source.AppendLine(";");
            }

            return source.ToString().Replace("\r\n", "\n");
        }

        public string RenderPrelude(
            IReadOnlyList<string> reExports,
            IReadOnlyList<string> operatorModules)
        {
            if (reExports == null)
                throw new ArgumentNullException(nameof(reExports));
            if (operatorModules == null)
                throw new ArgumentNullException(nameof(operatorModules));
            var source = new StringBuilder();
            for (var index = 0; index < operatorModules.Count; index++)
            {
                source.Append("use ");
                source.Append(operatorModules[index]);
                source.Append(" as __operator_module_");
                source.Append(index);
                source.AppendLine(";");
            }
            if (operatorModules.Count > 0 && reExports.Count > 0)
                source.AppendLine();
            foreach (var reExport in reExports)
            {
                source.Append("public use ");
                source.Append(FormatSobakasuPath(reExport));
                source.AppendLine(";");
            }
            return source.ToString().Replace("\r\n", "\n");
        }

        public string RenderOperatorBindings(
            IReadOnlyList<UdonApiGeneratedTypeModel> types)
        {
            if (types == null)
                throw new ArgumentNullException(nameof(types));

            var source = new StringBuilder();
            var wroteType = false;
            foreach (var type in types)
            {
                if (wroteType)
                    source.AppendLine();
                RenderImpl(
                    source,
                    type,
                    includeLanguageItem: true,
                    includeOperators: true,
                    operatorsOnly: true);
                wroteType = true;
            }
            return source.ToString().Replace("\r\n", "\n");
        }

        private void RenderImpl(
            StringBuilder source,
            UdonApiGeneratedTypeModel type,
            bool includeLanguageItem = true,
            bool includeOperators = true,
            bool operatorsOnly = false)
        {
            if (includeLanguageItem && !string.IsNullOrEmpty(type.LanguageItem))
            {
                source.Append("language item \"");
                source.Append(type.LanguageItem
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\""));
                source.AppendLine("\"");
            }
            source.Append("public implementation ");
            source.Append(type.WrapperName);
            source.Append(" = extern ");
        source.Append(GetExternalTypeName(type.Physical.RuntimeName));
            source.AppendLine(" {");

            var wroteMember = false;
            foreach (var member in type.Members)
            {
                if (!member.IsGenerated)
                    continue;
                var isOperator = SobakasuOperatorMapping.IsOperator(member.Physical);
                if (!includeOperators && isOperator || operatorsOnly && !isOperator)
                    continue;
                if (wroteMember)
                    source.AppendLine();
                RenderMember(source, type, member, "  ");
                wroteMember = true;
            }

            source.AppendLine("}");
        }

        private void RenderExternalType(
            StringBuilder source,
            UdonApiGeneratedTypeModel type)
        {
            RenderLanguageItem(source, type);
            source.Append("public type ");
            source.Append(type.WrapperName);
            source.Append(" = extern ");
        source.Append(GetExternalTypeName(type.Physical.RuntimeName));
            source.AppendLine(";");

            var wroteMember = false;
            foreach (var member in type.Members)
            {
                if (!member.IsGenerated)
                    continue;
                if (!wroteMember)
                {
                    source.AppendLine();
                    source.Append("implementation ");
                    source.Append(type.WrapperName);
                    source.AppendLine(" {");
                }
                else
                {
                    source.AppendLine();
                }
                RenderMember(source, type, member, "  ");
                wroteMember = true;
            }

            if (wroteMember)
                source.AppendLine("}");
        }

        private void RenderExternStruct(StringBuilder source, UdonApiGeneratedTypeModel type)
        {
            RenderLanguageItem(source, type);
            source.Append("public struct ");
            source.Append(type.WrapperName);
            source.Append(" = extern ");
        source.Append(GetExternalTypeName(type.Physical.RuntimeName));
            source.AppendLine(" {");
            foreach (var member in type.Members)
            {
                if (!member.IsGenerated || member.Physical.SourceKind != "FieldGetter" || member.Physical.IsStatic)
                    continue;
                source.Append("  ");
                AppendIdentifier(source, member.FunctionName);
                source.Append(": ");
                source.Append(FormatType(member.Physical.ReturnType, type.Physical.RuntimeName, member.Physical.GenericParameters));
                source.Append(" = extern ");
                AppendIdentifier(source, member.Physical.Name);
                source.AppendLine(",");
            }
            source.AppendLine("}");

            var hasMethods = false;
            foreach (var member in type.Members)
            {
                if (member.IsGenerated && !IsInstanceFieldMember(member))
                {
                    hasMethods = true;
                    break;
                }
            }
            if (!hasMethods)
                return;
            source.AppendLine();
            source.Append("implementation ");
            source.Append(type.WrapperName);
            source.AppendLine(" {");
            var wroteMember = false;
            foreach (var member in type.Members)
            {
                if (!member.IsGenerated || IsInstanceFieldMember(member))
                    continue;
                if (wroteMember)
                    source.AppendLine();
                RenderMember(source, type, member, "  ");
                wroteMember = true;
            }
            source.AppendLine("}");
        }

        private static bool IsInstanceFieldMember(UdonApiGeneratedMemberModel member)
        {
            return (member.Physical.SourceKind == "FieldGetter" || member.Physical.SourceKind == "FieldSetter") && !member.Physical.IsStatic;
        }

        private void RenderExternEnum(StringBuilder source, UdonApiGeneratedTypeModel type)
        {
            RenderLanguageItem(source, type);
            source.Append("public enum ");
            source.Append(type.WrapperName);
            source.Append(" = extern ");
        source.Append(GetExternalTypeName(type.Physical.RuntimeName));
            source.AppendLine(" {");
        foreach (var constant in type.Physical.Record.@enum?.constants ?? new List<UdonApiEnumConstantRecord>())
            {
                source.Append("  ");
            source.Append(constant.name);
                source.Append(" = extern ");
            source.Append(constant.name);
                source.AppendLine(",");
            }
            source.AppendLine("}");
        }

        private static void RenderLanguageItem(StringBuilder source, UdonApiGeneratedTypeModel type)
        {
            if (string.IsNullOrEmpty(type.LanguageItem))
                return;
            source.Append("language item \"");
            source.Append(type.LanguageItem.Replace("\\", "\\\\").Replace("\"", "\\\""));
            source.AppendLine("\"");
        }

        public string GetDeclarationKey(
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member)
        {
            var parameterTypes = new List<string>();
            if (SobakasuOperatorMapping.TryGet(member.Physical, out _, out _))
            {
                var operatorParameters = member.Physical.Parameters;
                for (var index = 1; index < operatorParameters.Count; index++)
                {
                    parameterTypes.Add(FormatOperatorType(
                        operatorParameters[index].type,
                        type.Physical.RuntimeName,
                        member.Physical.GenericParameters));
                }
                return $"{member.FunctionName}|{string.Join(",", parameterTypes)}";
            }

            switch (member.Physical.Kind)
            {
                case UdonApiMemberKind.Constructor:
                case UdonApiMemberKind.StaticMethod:
                case UdonApiMemberKind.InstanceMethod:
                    foreach (var parameter in member.Physical.Parameters)
                    {
                        if (parameter.passingMode != "Out" && parameter.passingMode != "GenericTypeArgument")
                            parameterTypes.Add(FormatType(
                                parameter.type,
                                type.Physical.RuntimeName,
                                member.Physical.GenericParameters));
                    }
                    break;

                case UdonApiMemberKind.FieldSetter:
                case UdonApiMemberKind.PropertySetter:
                    parameterTypes.Add(FormatType(
                        member.Physical.Parameters[member.Physical.Parameters.Count - 1].type,
                        type.Physical.RuntimeName,
                        member.Physical.GenericParameters));
                    break;
            }

            return $"{member.FunctionName}|{string.Join(",", parameterTypes)}";
        }

        private void RenderMember(
            StringBuilder source,
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member,
            string indent)
        {
            if (SobakasuOperatorMapping.TryGet(
                    member.Physical,
                    out var operatorToken,
                    out var isUnary))
            {
                RenderOperator(source, type, member, operatorToken, isUnary, indent);
                return;
            }

            switch (member.Physical.Kind)
            {
                case UdonApiMemberKind.Constructor:
                    if (type.Placement == UdonApiGeneratedPlacement.TopLevel)
                    {
                        throw new InvalidOperationException(
                            "Constructors cannot be rendered as top-level declarations.");
                    }
                    RenderConstructor(source, type, member, indent);
                    break;
                case UdonApiMemberKind.StaticMethod:
                case UdonApiMemberKind.InstanceMethod:
                    RenderMethod(source, type, member, indent);
                    break;
                case UdonApiMemberKind.PropertyGetter:
                case UdonApiMemberKind.PropertySetter:
                    RenderProperty(source, type, member, indent);
                    break;
                case UdonApiMemberKind.FieldGetter:
                case UdonApiMemberKind.FieldSetter:
                    RenderField(source, type, member, indent);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported generated member kind '{member.Physical.Kind}'.");
            }
        }

        private void RenderOperator(
            StringBuilder source,
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member,
            string operatorToken,
            bool isUnary,
            string indent)
        {
            var parameters = member.Physical.Parameters;
            var expectedArity = isUnary ? 1 : 2;
            if (parameters.Count != expectedArity)
            {
                throw new InvalidOperationException(
                    $"CLR operator '{member.Physical.OperatorName}' has invalid arity {parameters.Count}.");
            }

            source.Append(indent);
            source.Append("public function ");
            if (isUnary)
            {
                source.Append('@');
                source.Append(operatorToken);
                source.Append("(self)");
            }
            else
            {
                source.Append(operatorToken);
                source.Append("(self, rhs: ");
                source.Append(FormatOperatorType(parameters[1].type, type.Physical.RuntimeName, member.Physical.GenericParameters));
                source.Append(')');
            }
            if (member.Physical.ReturnType?.kind != "Named" || member.Physical.ReturnType.runtimeName != "System.Void")
            {
                source.Append(" -> ");
                source.Append(FormatOperatorType(
                    member.Physical.ReturnType,
                    type.Physical.RuntimeName,
                    member.Physical.GenericParameters));
            }
            source.AppendLine();
            source.Append(indent);
            source.Append("  = extern ");
            if (isUnary)
            {
                source.Append(operatorToken);
                source.Append("self");
            }
            else
            {
                source.Append("self ");
                source.Append(operatorToken);
                source.Append(" rhs");
            }
            source.AppendLine();
        }

        private void RenderConstructor(
            StringBuilder source,
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member,
            string indent)
        {
            var parameters = FormatParameters(
                member.Physical.Parameters,
                type.Physical.RuntimeName,
                member.Physical.GenericParameters);
            source.Append(indent);
            source.Append("public function ");
            source.Append(member.FunctionName);
            source.Append('(');
            source.Append(parameters.Declarations);
            source.Append(") -> ");
            source.AppendLine(FormatAdapterReturnType(
                new ExternTypeRef { kind = "Named", runtimeName = type.Physical.RuntimeName },
                member.Physical.Parameters,
                type,
                member));
            source.Append(indent);
            source.Append("  = extern new Self(");
            source.Append(HasByRefParameters(member.Physical.Parameters) ||
                member.RequiresExplicitAbiSignature
                ? FormatAbiParameters(
                    member.Physical.Parameters,
                    type.Physical.RuntimeName,
                    member.Physical.GenericParameters,
                    member)
                : parameters.Arguments);
            source.AppendLine(")");
        }

        private void RenderMethod(
            StringBuilder source,
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member,
            string indent)
        {
            var isStatic = member.Physical.IsStatic;
            if (type.Placement == UdonApiGeneratedPlacement.TopLevel && !isStatic)
            {
                throw new InvalidOperationException(
                    "Instance methods cannot be rendered as top-level declarations.");
            }
            var parameters = FormatParameters(
                member.Physical.Parameters,
                type.Physical.RuntimeName,
                member.Physical.GenericParameters);
            source.Append(indent);
            source.Append("public ");
            source.Append("function ");
            AppendCallableName(source, member.FunctionName);
            AppendGenericParameterList(source, member.Physical.GenericParameters);
            source.Append('(');
            if (!isStatic)
            {
                source.Append("self");
                if (parameters.Declarations.Length > 0)
                    source.Append(", ");
            }
            source.Append(parameters.Declarations);
            source.Append(')');
            var adapterReturnType = FormatAdapterReturnType(
                member.Physical.ReturnType,
                member.Physical.Parameters,
                type,
                member);
            if (adapterReturnType != null)
            {
                source.Append(" -> ");
                source.Append(adapterReturnType);
            }
            source.AppendLine();
            source.Append(indent);
            source.Append("  = ");
            if (member.ReturnProjection == UdonApiGeneratedProjection.Maybe)
                source.Append("maybe ");
            source.Append("extern ");
            if (isStatic)
            {
                source.Append(GetExternalTypeName(member.Physical.DeclaringRuntimeName));
                source.Append('.');
            }
            else
            {
                source.Append("self.");
            }
            AppendIdentifier(source, member.Physical.Name);
            AppendGenericParameterList(source, member.Physical.GenericParameters);
            source.Append('(');
            source.Append(HasByRefParameters(member.Physical.Parameters) ||
                member.RequiresExplicitAbiSignature
                ? FormatAbiParameters(
                    member.Physical.Parameters,
                    type.Physical.RuntimeName,
                    member.Physical.GenericParameters,
                    member)
                : parameters.Arguments);
            source.Append(')');
            source.AppendLine();
        }

        private static void AppendGenericParameterList(
            StringBuilder source,
            IReadOnlyList<UdonApiGenericParameterRecord> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return;
            source.Append('<');
            for (var index = 0; index < parameters.Count; index++)
            {
                if (index > 0)
                    source.Append(", ");
                source.Append(parameters[index].name);
            }
            source.Append('>');
        }

        private void RenderProperty(
            StringBuilder source,
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member,
            string indent)
        {
            var isSetter = member.Physical.Kind == UdonApiMemberKind.PropertySetter;
            var isStatic = member.Physical.IsStatic;
            var propertyType = isSetter
                ? member.Physical.Parameters[member.Physical.Parameters.Count - 1].type
                : member.Physical.ReturnType;
            if (type.Placement == UdonApiGeneratedPlacement.TopLevel && !isStatic)
            {
                throw new InvalidOperationException(
                    "Instance properties cannot be rendered as top-level declarations.");
            }
            source.Append(indent);
            source.Append("public ");
            source.Append("function ");
            AppendCallableName(source, member.FunctionName);
            if (isSetter)
            {
                source.Append(isStatic ? "(value: " : "(self, value: ");
                source.Append(FormatType(propertyType, type.Physical.RuntimeName, member.Physical.GenericParameters));
                source.AppendLine(")");
            }
            else
            {
                if (!isStatic)
                    source.Append("(self)");
                source.Append(" -> ");
                source.Append(FormatProjectedType(
                    propertyType,
                    type.Physical.RuntimeName,
                    member.Physical.GenericParameters,
                    member.ReturnProjection));
                source.AppendLine();
            }

            source.Append(indent);
            source.Append("  = ");
            if (member.ReturnProjection == UdonApiGeneratedProjection.Maybe)
                source.Append("maybe ");
            source.Append("extern ");
            AppendMemberReceiver(source, isStatic, member.Physical.DeclaringRuntimeName);
            AppendIdentifier(source, member.Physical.Name);
            if (isSetter)
                source.Append(" = value");
            source.AppendLine();
        }

        private void RenderField(
            StringBuilder source,
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member,
            string indent)
        {
            var isSetter = member.Physical.Kind == UdonApiMemberKind.FieldSetter;
            var isStatic = member.Physical.IsStatic;
            var fieldType = isSetter
                ? member.Physical.Parameters[member.Physical.Parameters.Count - 1].type
                : member.Physical.ReturnType;
            if (type.Placement == UdonApiGeneratedPlacement.TopLevel && !isStatic)
            {
                throw new InvalidOperationException(
                    "Instance fields cannot be rendered as top-level declarations.");
            }
            source.Append(indent);
            source.Append("public ");
            source.Append("function ");
            AppendCallableName(source, member.FunctionName);
            if (isSetter)
            {
                source.Append(isStatic ? "(value: " : "(self, value: ");
                source.Append(FormatType(fieldType, type.Physical.RuntimeName, member.Physical.GenericParameters));
                source.AppendLine(")");
            }
            else
            {
                if (!isStatic)
                    source.Append("(self)");
                source.Append(" -> ");
                source.Append(FormatProjectedType(
                    fieldType,
                    type.Physical.RuntimeName,
                    member.Physical.GenericParameters,
                    member.ReturnProjection));
                source.AppendLine();
            }

            source.Append(indent);
            source.Append("  = ");
            if (member.ReturnProjection == UdonApiGeneratedProjection.Maybe)
                source.Append("maybe ");
            source.Append("extern ");
            AppendMemberReceiver(source, isStatic, member.Physical.DeclaringRuntimeName);
            AppendIdentifier(source, member.Physical.Name);
            if (isSetter)
                source.Append(" = value");
            source.AppendLine();
        }

        private static void AppendMemberReceiver(
            StringBuilder source,
            bool isStatic,
            string declaringRuntimeName)
        {
            if (!isStatic)
            {
                source.Append("self.");
            }
            else
            {
                source.Append(GetExternalTypeName(declaringRuntimeName));
                source.Append('.');
            }
        }

        private string FormatProjectedType(
            ExternTypeRef type,
            string declaringRuntimeName,
            IReadOnlyList<UdonApiGenericParameterRecord> genericParameters,
            UdonApiGeneratedProjection projection)
        {
            var formatted = FormatType(type, declaringRuntimeName, genericParameters);
            return projection == UdonApiGeneratedProjection.Maybe
                ? $"Maybe<{formatted}>"
                : formatted;
        }

        private string FormatType(ExternTypeRef type, string declaringRuntimeName, IReadOnlyList<UdonApiGenericParameterRecord> genericParameters)
        {
            if (_typeFormatter.TryFormat(
                    type,
                    declaringRuntimeName,
                    genericParameters,
                    out var typeName,
                    out var reason))
            {
                return typeName;
            }

            throw new InvalidOperationException(reason);
        }

        private string FormatOperatorType(ExternTypeRef type, string hostRuntimeName, IReadOnlyList<UdonApiGenericParameterRecord> genericParameters)
        {
            return type?.kind == "Named" && type.runtimeName == hostRuntimeName
                ? "Self"
                : FormatType(type, hostRuntimeName, genericParameters);
        }

        private static string GetExternalTypeName(string runtimeName)
        {
            // ExternCatalog canonicalizes nested CLR types to dotted runtime
            // identities, so preserve that catalog spelling for extern lookup.
            return (runtimeName ?? string.Empty).Replace('+', '.');
        }

        private static string FormatSobakasuPath(string path)
        {
            return path.Replace(".", "::");
        }

        private static void AppendIdentifier(StringBuilder source, string name)
        {
            if (!SobakasuIdentifierFacts.TryRenderIdentifier(name, out var rendering))
            {
                throw new InvalidOperationException(
                    $"Identifier '{name}' cannot be represented in Sobakasu source.");
            }

            source.Append(rendering);
        }

        private static void AppendCallableName(StringBuilder source, string name)
        {
            var hasPredicateSuffix = name.EndsWith("?", StringComparison.Ordinal);
            var identifier = hasPredicateSuffix ? name[..^1] : name;
            AppendIdentifier(source, identifier);
            if (hasPredicateSuffix)
                source.Append('?');
        }

        private ParameterList FormatParameters(
            IReadOnlyList<ExternParameterRecord> parameters,
            string declaringRuntimeName,
            IReadOnlyList<UdonApiGenericParameterRecord> genericParameters)
        {
            var declarations = new StringBuilder();
            var arguments = new StringBuilder();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            var wroteInput = false;
            for (var index = 0; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                if (parameter.passingMode == "GenericTypeArgument")
                    continue;
                var baseName = SobakasuNameUtility.ToIdentifier(
                    parameter.name,
                    $"arg{index}");
                var parameterName = baseName;
                var suffix = 2;
                while (!usedNames.Add(parameterName))
                    parameterName = $"{baseName}_{suffix++}";

                if (parameter.passingMode == "Out")
                    continue;

                if (wroteInput)
                {
                    declarations.Append(", ");
                    arguments.Append(", ");
                }

                declarations.Append(parameterName);
                declarations.Append(": ");
                declarations.Append(FormatType(parameter.type, declaringRuntimeName, genericParameters));
                arguments.Append(parameterName);
                wroteInput = true;
            }

            return new ParameterList(declarations.ToString(), arguments.ToString());
        }

        private string FormatAbiParameters(
            IReadOnlyList<ExternParameterRecord> parameters,
            string declaringRuntimeName,
            IReadOnlyList<UdonApiGenericParameterRecord> genericParameters,
            UdonApiGeneratedMemberModel member)
        {
            var result = new StringBuilder();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                if (parameter.passingMode == "GenericTypeArgument")
                    continue;
                if (result.Length > 0)
                    result.Append(", ");
                if (parameter.passingMode == "Out")
                {
                    if (member.GetOutProjection(index) == UdonApiGeneratedProjection.Maybe)
                        result.Append("maybe ");
                    result.Append("out ");
                }
                else if (parameter.passingMode == "Ref")
                    result.Append("ref ");

                result.Append(FormatType(parameter.type, declaringRuntimeName, genericParameters));
                result.Append(' ');
                var baseName = SobakasuNameUtility.ToIdentifier(parameter.name, $"arg{index}");
                var name = baseName;
                var suffix = 2;
                while (!usedNames.Add(name))
                    name = $"{baseName}_{suffix++}";
                result.Append(name);
            }
            return result.ToString();
        }

        private string FormatAdapterReturnType(
            ExternTypeRef returnType,
            IReadOnlyList<ExternParameterRecord> parameters,
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member)
        {
            var outputs = new List<string>();
            if (returnType?.kind != "Named" || returnType.runtimeName != "System.Void")
            {
                outputs.Add(FormatProjectedType(
                    returnType,
                    type.Physical.RuntimeName,
                    member.Physical.GenericParameters,
                    member.ReturnProjection));
            }
            for (var index = 0; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                if (parameter.passingMode == "Out" || parameter.passingMode == "Ref")
                {
                    outputs.Add(FormatProjectedType(
                        parameter.type,
                        type.Physical.RuntimeName,
                        member.Physical.GenericParameters,
                        member.GetOutProjection(index)));
                }
            }

            if (outputs.Count == 0)
                return null;
            if (outputs.Count == 1)
                return outputs[0];
            return $"({string.Join(", ", outputs)})";
        }

        private static bool HasByRefParameters(IReadOnlyList<ExternParameterRecord> parameters)
        {
            foreach (var parameter in parameters)
            {
                if (parameter.passingMode == "Ref" || parameter.passingMode == "Out" || parameter.passingMode == "In")
                    return true;
            }
            return false;
        }

        private readonly struct ParameterList
        {
            public string Declarations { get; }
            public string Arguments { get; }

            public ParameterList(string declarations, string arguments)
            {
                Declarations = declarations;
                Arguments = arguments;
            }
        }
    }
}
