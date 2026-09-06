using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

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
            var identifier = ToSnakeCase(value);
            if (string.IsNullOrEmpty(identifier))
                identifier = fallback;
            if (char.IsDigit(identifier[0]))
                identifier = $"_{identifier}";

            while (!IsIdentifier(identifier))
                identifier += "_";

            return identifier;
        }

        public static bool IsIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var lexer = new SobakasuLexer(SourceText.From(value));
            var first = lexer.Lex();
            var second = lexer.Lex();
            return !lexer.Diagnostics.HasErrors &&
                first.Kind == SyntaxKind.Identifier &&
                first.Text == value &&
                second.Kind == SyntaxKind.EndOfFile;
        }

        private static void AppendUnderscore(StringBuilder builder)
        {
            if (builder.Length > 0 && builder[builder.Length - 1] != '_')
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
                source.AppendLine("use maybe.Maybe;\n");
            var wroteDeclaration = false;
            if (type.Placement == UdonApiGeneratedPlacement.Impl)
            {
                RenderImpl(source, type, includeLanguageItem, includeOperators);
                wroteDeclaration = true;
            }
            else if (type.Placement == UdonApiGeneratedPlacement.Struct)
            {
                RenderExternStruct(source, type);
                wroteDeclaration = true;
            }
            else if (type.Placement == UdonApiGeneratedPlacement.Enum)
            {
                RenderExternEnum(source, type);
                wroteDeclaration = true;
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

                var parameters = member.Physical.Callable?.GetParameters() ??
                    Array.Empty<ParameterInfo>();
                for (var index = 0; index < parameters.Length; index++)
                {
                    if (parameters[index].IsOut &&
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
                source.Append("pub mod ");
                source.Append(childModule);
                source.AppendLine(";");
            }
            foreach (var type in sortedTypes)
            {
                source.Append("mod ");
                source.Append(type.ModuleName);
                source.AppendLine(";");
            }

            if (sortedTypes.Count > 0)
                source.AppendLine();
            foreach (var type in sortedTypes)
            {
                if (!type.ShouldReExport)
                    continue;
                source.Append("pub use ");
                if (type.Placement == UdonApiGeneratedPlacement.TopLevel)
                {
                    source.Append(type.ModuleName);
                }
                else
                {
                    source.Append(rootModuleNames.Contains(type.ModuleName)
                        ? $"{type.GeneratedNamespace}.{type.ModuleName}"
                        : type.ModuleName);
                    source.Append('.');
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
                source.Append("pub use ");
                source.Append(reExport);
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
                source.Append("lang \"");
                source.Append(type.LanguageItem
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\""));
                source.AppendLine("\"");
            }
            source.Append("pub impl ");
            source.Append(type.WrapperName);
            source.Append(" = extern ");
            source.Append(type.Physical.QualifiedName);
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

        private void RenderExternStruct(StringBuilder source, UdonApiGeneratedTypeModel type)
        {
            RenderLanguageItem(source, type);
            source.Append("pub struct ");
            source.Append(type.WrapperName);
            source.Append(" = extern ");
            source.Append(type.Physical.QualifiedName);
            source.AppendLine(" {");
            foreach (var member in type.Members)
            {
                if (!member.IsGenerated || member.Physical.Kind != UdonApiMemberKind.FieldGetter || member.Physical.Member is not FieldInfo field || field.IsStatic)
                    continue;
                source.Append("  ");
                source.Append(member.FunctionName);
                source.Append(": ");
                source.Append(FormatType(field.FieldType, type.Physical.ClrType));
                source.Append(" = extern ");
                source.Append(field.Name);
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
            source.Append("impl ");
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
            return (member.Physical.Kind == UdonApiMemberKind.FieldGetter ||
                    member.Physical.Kind == UdonApiMemberKind.FieldSetter) &&
                member.Physical.Member is FieldInfo field && !field.IsStatic;
        }

        private void RenderExternEnum(StringBuilder source, UdonApiGeneratedTypeModel type)
        {
            RenderLanguageItem(source, type);
            source.Append("pub enum ");
            source.Append(type.WrapperName);
            source.Append(" = extern ");
            source.Append(type.Physical.QualifiedName);
            source.AppendLine(" {");
            foreach (var name in Enum.GetNames(type.Physical.ClrType))
            {
                source.Append("  ");
                source.Append(name);
                source.Append(" = extern ");
                source.Append(name);
                source.AppendLine(",");
            }
            source.AppendLine("}");
        }

        private static void RenderLanguageItem(StringBuilder source, UdonApiGeneratedTypeModel type)
        {
            if (string.IsNullOrEmpty(type.LanguageItem))
                return;
            source.Append("lang \"");
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
                var operatorParameters = member.Physical.OperatorParameterTypes;
                for (var index = 1; index < operatorParameters.Count; index++)
                {
                    parameterTypes.Add(FormatOperatorType(
                        operatorParameters[index],
                        type.Physical.ClrType));
                }
                return $"{member.FunctionName}|{string.Join(",", parameterTypes)}";
            }

            switch (member.Physical.Kind)
            {
                case UdonApiMemberKind.Constructor:
                case UdonApiMemberKind.StaticMethod:
                case UdonApiMemberKind.InstanceMethod:
                    foreach (var parameter in member.Physical.Callable.GetParameters())
                    {
                        if (!parameter.IsOut)
                            parameterTypes.Add(FormatType(
                                parameter.ParameterType,
                                type.Physical.ClrType));
                    }
                    break;

                case UdonApiMemberKind.PropertySetter:
                    var property = (PropertyInfo)member.Physical.Member;
                    parameterTypes.Add(FormatType(
                        property.PropertyType,
                        type.Physical.ClrType));
                    break;

                case UdonApiMemberKind.FieldSetter:
                    var field = (FieldInfo)member.Physical.Member;
                    parameterTypes.Add(FormatType(
                        field.FieldType,
                        type.Physical.ClrType));
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
            var parameters = member.Physical.OperatorParameterTypes;
            var expectedArity = isUnary ? 1 : 2;
            if (parameters.Count != expectedArity)
            {
                throw new InvalidOperationException(
                    $"CLR operator '{member.Physical.OperatorName}' has invalid arity {parameters.Count}.");
            }

            source.Append(indent);
            source.Append("pub fn ");
            if (isUnary)
            {
                source.Append('@');
                source.Append(operatorToken);
            }
            else
            {
                source.Append(operatorToken);
                source.Append("(rhs: ");
                source.Append(FormatOperatorType(parameters[1], type.Physical.ClrType));
                source.Append(')');
            }
            if (member.Physical.OperatorReturnType != typeof(void))
            {
                source.Append(" -> ");
                source.Append(FormatOperatorType(
                    member.Physical.OperatorReturnType,
                    type.Physical.ClrType));
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
            var constructor = (ConstructorInfo)member.Physical.Callable;
            var parameters = FormatParameters(
                constructor.GetParameters(),
                type.Physical.ClrType);
            source.Append(indent);
            source.Append("pub static fn ");
            source.Append(member.FunctionName);
            source.Append('(');
            source.Append(parameters.Declarations);
            source.Append(") -> ");
            source.AppendLine(FormatAdapterReturnType(
                type.Physical.ClrType,
                constructor.GetParameters(),
                type,
                member));
            source.Append(indent);
            source.Append("  = extern new Self(");
            source.Append(HasByRefParameters(constructor.GetParameters()) ||
                member.RequiresExplicitAbiSignature
                ? FormatAbiParameters(
                    constructor.GetParameters(),
                    type.Physical.ClrType,
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
            var method = (MethodInfo)member.Physical.Callable;
            if (type.Placement == UdonApiGeneratedPlacement.TopLevel && !method.IsStatic)
            {
                throw new InvalidOperationException(
                    "Instance methods cannot be rendered as top-level declarations.");
            }
            var parameters = FormatParameters(
                method.GetParameters(),
                type.Physical.ClrType);
            source.Append(indent);
            source.Append("pub ");
            if (method.IsStatic && type.Placement != UdonApiGeneratedPlacement.TopLevel)
                source.Append("static ");
            source.Append("fn ");
            source.Append(member.FunctionName);
            AppendGenericParameterList(source, method);
            source.Append('(');
            source.Append(parameters.Declarations);
            source.Append(')');
            var adapterReturnType = FormatAdapterReturnType(
                method.ReturnType,
                method.GetParameters(),
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
            if (method.IsStatic)
            {
                source.Append(GetQualifiedTypeName(method.DeclaringType));
                source.Append('.');
            }
            else
            {
                source.Append("self.");
            }
            source.Append(method.Name);
            AppendGenericParameterList(source, method);
            source.Append('(');
            source.Append(HasByRefParameters(method.GetParameters()) ||
                member.RequiresExplicitAbiSignature
                ? FormatAbiParameters(
                    method.GetParameters(),
                    type.Physical.ClrType,
                    member)
                : parameters.Arguments);
            source.Append(')');
            source.AppendLine();
        }

        private static void AppendGenericParameterList(
            StringBuilder source,
            MethodInfo method)
        {
            if (!method.IsGenericMethodDefinition)
                return;
            var parameters = method.GetGenericArguments();
            source.Append('<');
            for (var index = 0; index < parameters.Length; index++)
            {
                if (index > 0)
                    source.Append(", ");
                source.Append(parameters[index].Name);
            }
            source.Append('>');
        }

        private void RenderProperty(
            StringBuilder source,
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member,
            string indent)
        {
            var property = (PropertyInfo)member.Physical.Member;
            var accessor = (MethodInfo)member.Physical.Callable;
            var isSetter = member.Physical.Kind == UdonApiMemberKind.PropertySetter;
            if (type.Placement == UdonApiGeneratedPlacement.TopLevel && !accessor.IsStatic)
            {
                throw new InvalidOperationException(
                    "Instance properties cannot be rendered as top-level declarations.");
            }
            source.Append(indent);
            source.Append("pub ");
            if (accessor.IsStatic && type.Placement != UdonApiGeneratedPlacement.TopLevel)
                source.Append("static ");
            source.Append("fn ");
            source.Append(member.FunctionName);
            if (isSetter)
            {
                source.Append("(value: ");
                source.Append(FormatType(property.PropertyType, type.Physical.ClrType));
                source.AppendLine(")");
            }
            else
            {
                source.Append(" -> ");
                source.Append(FormatProjectedType(
                    property.PropertyType,
                    type.Physical.ClrType,
                    member.ReturnProjection));
                source.AppendLine();
            }

            source.Append(indent);
            source.Append("  = ");
            if (member.ReturnProjection == UdonApiGeneratedProjection.Maybe)
                source.Append("maybe ");
            source.Append("extern ");
            AppendMemberReceiver(source, type, accessor.IsStatic, property.DeclaringType);
            source.Append(property.Name);
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
            var field = (FieldInfo)member.Physical.Member;
            var isSetter = member.Physical.Kind == UdonApiMemberKind.FieldSetter;
            if (type.Placement == UdonApiGeneratedPlacement.TopLevel && !field.IsStatic)
            {
                throw new InvalidOperationException(
                    "Instance fields cannot be rendered as top-level declarations.");
            }
            source.Append(indent);
            source.Append("pub ");
            if (field.IsStatic && type.Placement != UdonApiGeneratedPlacement.TopLevel)
                source.Append("static ");
            source.Append("fn ");
            source.Append(member.FunctionName);
            if (isSetter)
            {
                source.Append("(value: ");
                source.Append(FormatType(field.FieldType, type.Physical.ClrType));
                source.AppendLine(")");
            }
            else
            {
                source.Append(" -> ");
                source.Append(FormatProjectedType(
                    field.FieldType,
                    type.Physical.ClrType,
                    member.ReturnProjection));
                source.AppendLine();
            }

            source.Append(indent);
            source.Append("  = ");
            if (member.ReturnProjection == UdonApiGeneratedProjection.Maybe)
                source.Append("maybe ");
            source.Append("extern ");
            AppendMemberReceiver(source, type, field.IsStatic, field.DeclaringType);
            source.Append(field.Name);
            if (isSetter)
                source.Append(" = value");
            source.AppendLine();
        }

        private static void AppendMemberReceiver(
            StringBuilder source,
            UdonApiGeneratedTypeModel type,
            bool isStatic,
            Type declaringType)
        {
            if (!isStatic)
            {
                source.Append("self.");
            }
            else if (type.Placement != UdonApiGeneratedPlacement.TopLevel)
            {
                source.Append("Self.");
            }
            else
            {
                source.Append(GetQualifiedTypeName(declaringType));
                source.Append('.');
            }
        }

        private string FormatProjectedType(
            Type type,
            Type declaringType,
            UdonApiGeneratedProjection projection)
        {
            var formatted = FormatType(type, declaringType);
            return projection == UdonApiGeneratedProjection.Maybe
                ? $"Maybe<{formatted}>"
                : formatted;
        }

        private string FormatType(Type type, Type declaringType)
        {
            if (_typeFormatter.TryFormat(
                    type,
                    declaringType,
                    out var typeName,
                    out var reason))
            {
                return typeName;
            }

            throw new InvalidOperationException(reason);
        }

        private string FormatOperatorType(Type type, Type hostType)
        {
            var normalizedType = type != null && type.IsByRef
                ? type.GetElementType()
                : type;
            return normalizedType == hostType
                ? "Self"
                : FormatType(type, hostType);
        }

        private static string GetQualifiedTypeName(Type type)
        {
            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        private ParameterList FormatParameters(
            IReadOnlyList<ParameterInfo> parameters,
            Type declaringType)
        {
            var declarations = new StringBuilder();
            var arguments = new StringBuilder();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            var wroteInput = false;
            for (var index = 0; index < parameters.Count; index++)
            {
                var baseName = SobakasuNameUtility.ToIdentifier(
                    parameters[index].Name,
                    $"arg{index}");
                var parameterName = baseName;
                var suffix = 2;
                while (!usedNames.Add(parameterName))
                    parameterName = $"{baseName}_{suffix++}";

                if (parameters[index].IsOut)
                    continue;

                if (wroteInput)
                {
                    declarations.Append(", ");
                    arguments.Append(", ");
                }

                declarations.Append(parameterName);
                declarations.Append(": ");
                declarations.Append(FormatType(parameters[index].ParameterType, declaringType));
                arguments.Append(parameterName);
                wroteInput = true;
            }

            return new ParameterList(declarations.ToString(), arguments.ToString());
        }

        private string FormatAbiParameters(
            IReadOnlyList<ParameterInfo> parameters,
            Type declaringType,
            UdonApiGeneratedMemberModel member)
        {
            var result = new StringBuilder();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < parameters.Count; index++)
            {
                if (index > 0)
                    result.Append(", ");

                var parameter = parameters[index];
                if (parameter.IsOut)
                {
                    if (member.GetOutProjection(index) == UdonApiGeneratedProjection.Maybe)
                        result.Append("maybe ");
                    result.Append("out ");
                }
                else if (parameter.ParameterType.IsByRef && !parameter.IsIn)
                    result.Append("ref ");

                result.Append(FormatType(parameter.ParameterType, declaringType));
                result.Append(' ');
                var baseName = SobakasuNameUtility.ToIdentifier(parameter.Name, $"arg{index}");
                var name = baseName;
                var suffix = 2;
                while (!usedNames.Add(name))
                    name = $"{baseName}_{suffix++}";
                result.Append(name);
            }
            return result.ToString();
        }

        private string FormatAdapterReturnType(
            Type returnType,
            IReadOnlyList<ParameterInfo> parameters,
            UdonApiGeneratedTypeModel type,
            UdonApiGeneratedMemberModel member)
        {
            var outputs = new List<string>();
            if (returnType != typeof(void))
            {
                outputs.Add(FormatProjectedType(
                    returnType,
                    type.Physical.ClrType,
                    member.ReturnProjection));
            }
            for (var index = 0; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                if (parameter.ParameterType.IsByRef &&
                    (parameter.IsOut || !parameter.IsIn))
                {
                    outputs.Add(FormatProjectedType(
                        parameter.ParameterType,
                        type.Physical.ClrType,
                        member.GetOutProjection(index)));
                }
            }

            if (outputs.Count == 0)
                return null;
            if (outputs.Count == 1)
                return outputs[0];
            return $"({string.Join(", ", outputs)})";
        }

        private static bool HasByRefParameters(IReadOnlyList<ParameterInfo> parameters)
        {
            foreach (var parameter in parameters)
            {
                if (parameter.ParameterType.IsByRef)
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
