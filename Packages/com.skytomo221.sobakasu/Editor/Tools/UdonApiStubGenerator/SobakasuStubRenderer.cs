using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tools.UdonApiStubGenerator
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

  internal sealed class SobakasuStubRenderer
  {
    private readonly UdonApiStubTypeFormatter _typeFormatter;

    public SobakasuStubRenderer(UdonApiStubTypeFormatter typeFormatter)
    {
      _typeFormatter = typeFormatter ??
          throw new ArgumentNullException(nameof(typeFormatter));
    }

    public string Render(UdonApiTypeModel type)
    {
      if (type == null)
        throw new ArgumentNullException(nameof(type));
      if (!type.IsGenerated)
        throw new InvalidOperationException("A skipped type cannot be rendered.");

      var source = new StringBuilder();
      source.Append("pub impl ");
      source.Append(type.WrapperName);
      source.Append(" = extern ");
      source.Append(type.QualifiedName);
      source.AppendLine(" {");

      var wroteMember = false;
      foreach (var member in type.Members)
      {
        if (!member.IsGenerated)
          continue;

        if (wroteMember)
          source.AppendLine();
        RenderMember(source, type, member);
        wroteMember = true;
      }

      source.AppendLine("}");
      return source.ToString().Replace("\r\n", "\n");
    }

    public string GetDeclarationKey(
        UdonApiTypeModel type,
        UdonApiMemberModel member)
    {
      var parameterTypes = new List<string>();
      switch (member.Kind)
      {
        case UdonApiMemberKind.Constructor:
        case UdonApiMemberKind.StaticMethod:
        case UdonApiMemberKind.InstanceMethod:
          foreach (var parameter in member.Callable.GetParameters())
          {
            if (!parameter.IsOut)
              parameterTypes.Add(FormatType(parameter.ParameterType, type.ClrType));
          }
          break;

        case UdonApiMemberKind.PropertySetter:
          var property = (PropertyInfo)member.Member;
          parameterTypes.Add(FormatType(property.PropertyType, type.ClrType));
          break;

        case UdonApiMemberKind.FieldSetter:
          var field = (FieldInfo)member.Member;
          parameterTypes.Add(FormatType(field.FieldType, type.ClrType));
          break;
      }

      var dispatch = IsStatic(member) ? "static" : "instance";
      return
          $"{dispatch}|{GetFunctionName(member)}|" +
          string.Join(",", parameterTypes);
    }

    private void RenderMember(
        StringBuilder source,
        UdonApiTypeModel type,
        UdonApiMemberModel member)
    {
      switch (member.Kind)
      {
        case UdonApiMemberKind.Constructor:
          RenderConstructor(source, type, member);
          break;
        case UdonApiMemberKind.StaticMethod:
        case UdonApiMemberKind.InstanceMethod:
          RenderMethod(source, type, member);
          break;
        case UdonApiMemberKind.PropertyGetter:
        case UdonApiMemberKind.PropertySetter:
          RenderProperty(source, type, member);
          break;
        case UdonApiMemberKind.FieldGetter:
        case UdonApiMemberKind.FieldSetter:
          RenderField(source, type, member);
          break;
        default:
          throw new InvalidOperationException(
              $"Unsupported generated member kind '{member.Kind}'.");
      }
    }

    private void RenderConstructor(
        StringBuilder source,
        UdonApiTypeModel type,
        UdonApiMemberModel member)
    {
      var constructor = (ConstructorInfo)member.Callable;
      var parameters = FormatParameters(constructor.GetParameters(), type.ClrType);
      source.Append("  pub static fn new(");
      source.Append(parameters.Declarations);
      source.Append(") -> ");
      source.AppendLine(FormatAdapterReturnType(
          type.ClrType,
          constructor.GetParameters(),
          type.ClrType));
      source.Append("    = extern new Self(");
      source.Append(HasByRefParameters(constructor.GetParameters())
          ? FormatAbiParameters(constructor.GetParameters(), type.ClrType)
          : parameters.Arguments);
      source.AppendLine(")");
    }

    private void RenderMethod(
        StringBuilder source,
        UdonApiTypeModel type,
        UdonApiMemberModel member)
    {
      var method = (MethodInfo)member.Callable;
      var parameters = FormatParameters(method.GetParameters(), type.ClrType);
      source.Append("  pub ");
      if (method.IsStatic)
        source.Append("static ");
      source.Append("fn ");
      source.Append(GetFunctionName(member));
      source.Append('(');
      source.Append(parameters.Declarations);
      source.Append(')');
      var adapterReturnType = FormatAdapterReturnType(
          method.ReturnType,
          method.GetParameters(),
          type.ClrType);
      if (adapterReturnType != null)
      {
        source.Append(" -> ");
        source.Append(adapterReturnType);
      }
      source.AppendLine();
      source.Append("    = extern ");
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
      source.Append('(');
      source.Append(HasByRefParameters(method.GetParameters())
          ? FormatAbiParameters(method.GetParameters(), type.ClrType)
          : parameters.Arguments);
      source.Append(')');
      source.AppendLine();
    }

    private void RenderProperty(
        StringBuilder source,
        UdonApiTypeModel type,
        UdonApiMemberModel member)
    {
      var property = (PropertyInfo)member.Member;
      var accessor = (MethodInfo)member.Callable;
      var isSetter = member.Kind == UdonApiMemberKind.PropertySetter;
      source.Append("  pub ");
      if (accessor.IsStatic)
        source.Append("static ");
      source.Append("fn ");
      source.Append(GetFunctionName(member));
      if (isSetter)
      {
        source.Append("(value: ");
        source.Append(FormatType(property.PropertyType, type.ClrType));
        source.AppendLine(")");
      }
      else
      {
        source.Append(" -> ");
        source.Append(FormatType(property.PropertyType, type.ClrType));
        source.AppendLine();
      }

      source.Append("    = extern ");
      source.Append(accessor.IsStatic ? "Self." : "self.");
      source.Append(property.Name);
      if (isSetter)
        source.Append(" = value");
      source.AppendLine();
    }

    private void RenderField(
        StringBuilder source,
        UdonApiTypeModel type,
        UdonApiMemberModel member)
    {
      var field = (FieldInfo)member.Member;
      var isSetter = member.Kind == UdonApiMemberKind.FieldSetter;
      source.Append("  pub ");
      if (field.IsStatic)
        source.Append("static ");
      source.Append("fn ");
      source.Append(GetFunctionName(member));
      if (isSetter)
      {
        source.Append("(value: ");
        source.Append(FormatType(field.FieldType, type.ClrType));
        source.AppendLine(")");
      }
      else
      {
        source.Append(" -> ");
        source.Append(FormatType(field.FieldType, type.ClrType));
        source.AppendLine();
      }

      source.Append("    = extern ");
      source.Append(field.IsStatic ? "Self." : "self.");
      source.Append(field.Name);
      if (isSetter)
        source.Append(" = value");
      source.AppendLine();
    }

    private string GetFunctionName(UdonApiMemberModel member)
    {
      switch (member.Kind)
      {
        case UdonApiMemberKind.Constructor:
          return "new";
        case UdonApiMemberKind.PropertySetter:
        case UdonApiMemberKind.FieldSetter:
          return SobakasuNameUtility.ToIdentifier(
              $"set_{member.MemberName}",
              "set_value");
        default:
          return SobakasuNameUtility.ToIdentifier(member.MemberName, "member");
      }
    }

    private static bool IsStatic(UdonApiMemberModel member)
    {
      if (member.Kind == UdonApiMemberKind.Constructor)
        return true;
      if (member.Callable is MethodInfo method)
        return method.IsStatic;
      return member.Member is FieldInfo field && field.IsStatic;
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
        Type declaringType)
    {
      var result = new StringBuilder();
      var usedNames = new HashSet<string>(StringComparer.Ordinal);
      for (var index = 0; index < parameters.Count; index++)
      {
        if (index > 0)
          result.Append(", ");

        var parameter = parameters[index];
        if (parameter.IsOut)
          result.Append("out ");
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
        Type declaringType)
    {
      var outputs = new List<string>();
      if (returnType != typeof(void))
        outputs.Add(FormatType(returnType, declaringType));
      foreach (var parameter in parameters)
      {
        if (parameter.ParameterType.IsByRef &&
            (parameter.IsOut || !parameter.IsIn))
        {
          outputs.Add(FormatType(parameter.ParameterType, declaringType));
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
