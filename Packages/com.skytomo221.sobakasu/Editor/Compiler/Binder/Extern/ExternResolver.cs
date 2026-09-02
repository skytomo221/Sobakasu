using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class ExternResolver : BinderComponent
  {
    internal ExternResolver(BindingSession session) : base(session)
    {
    }
  
    internal BoundExpression BindExternExpression(ExternExpressionSyntax syntax)
    {
      switch (syntax.Expression)
      {
        case CallExpressionSyntax call:
          return Session.ExternResolver.BindExternMethodCall(call);
        case MemberAccessExpressionSyntax member:
          return Session.ExternResolver.BindExternMemberAccess(member, ExternMemberKind.Getter, null);
        case AssignmentExpressionSyntax assignment when assignment.OperatorToken.Kind == SyntaxKind.EqualsToken && assignment.Target is MemberAccessExpressionSyntax setterMember:
          return Session.ExternResolver.BindExternMemberAccess(setterMember, ExternMemberKind.Setter, Session.ExpressionBinder.BindExpression(assignment.Expression));
        case NewExpressionSyntax constructor:
          return Session.ExternResolver.BindExternConstructor(constructor);
        case UnaryExpressionSyntax unary:
          return Session.ExternResolver.BindExternUnaryOperator(unary);
        case BinaryExpressionSyntax binary:
          return Session.ExternResolver.BindExternBinaryOperator(binary);
        default:
          Session.Diagnostics.ReportUnsupportedExternalExpression(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Expression));
          return BoundErrorExpression.Instance;
      }
    }
  
    internal BoundExpression BindExternMethodCall(CallExpressionSyntax syntax)
    {
      TypeArgumentListSyntax typeArgumentSyntax = null;
      var rawTarget = syntax.Target;
      if (rawTarget is GenericTypeExpressionSyntax genericApplication)
      {
        typeArgumentSyntax = genericApplication.TypeArgumentList;
        rawTarget = genericApplication.Target;
      }
      if (rawTarget is not MemberAccessExpressionSyntax member)
      {
        Session.Diagnostics.ReportUnsupportedExternalExpression(Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
        return BoundErrorExpression.Instance;
      }
  
      var arguments = new List<BoundExpression>();
      foreach (var argumentSyntax in syntax.Arguments)
        arguments.Add(Session.ExpressionBinder.BindExpression(argumentSyntax));
      for (var index = 0; index < arguments.Count; index++)
      {
        if (!Session.ExpressionBinder.IsAggregateStorageType(arguments[index].Type))
          continue;
        Session.Diagnostics.ReportAggregateExternBoundary(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Arguments[index]), arguments[index].Type.Name);
        return BoundErrorExpression.Instance;
      }
  
      if (!Session.ExternResolver.TryBindExternalReceiver(member.Expression, out var containingType, out var receiver, out var isStatic))
      {
        return BoundErrorExpression.Instance;
      }
  
      if (!isStatic)
        arguments.Insert(0, receiver);
      var group = Session.Environment.ExternCatalog.GetExternalMethodGroup(containingType, member.MemberName);
      var typeArguments = typeArgumentSyntax == null
          ? null
          : Session.TypeResolver.BindTypeArguments(typeArgumentSyntax);
      return Session.ExternResolver.BindExternalMethodGroup(group, containingType, member.MemberName, arguments, isStatic, ExternMemberKind.Method, Session.BinderSyntaxFacts.GetExpressionSpan(syntax), typeArguments);
    }
  
    internal BoundExpression BindExternMemberAccess(MemberAccessExpressionSyntax syntax, ExternMemberKind memberKind, BoundExpression value)
    {
      if (value != null && Session.ExpressionBinder.IsAggregateStorageType(value.Type))
      {
        Session.Diagnostics.ReportAggregateExternBoundary(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), value.Type.Name);
        return BoundErrorExpression.Instance;
      }
  
      if (!Session.ExternResolver.TryBindExternalReceiver(syntax.Expression, out var containingType, out var receiver, out var isStatic))
      {
        return BoundErrorExpression.Instance;
      }
  
      var arguments = new List<BoundExpression>();
      if (!isStatic)
        arguments.Add(receiver);
      if (value != null)
        arguments.Add(value);
      if (memberKind == ExternMemberKind.Getter && isStatic && value == null && Session.NetworkSendBinder.TryBindExternalEnumConstant(containingType, syntax.MemberName, Session.BinderSyntaxFacts.GetExpressionSpan(syntax), out var enumConstant))
      {
        return enumConstant;
      }
  
      var group = Session.Environment.ExternCatalog.GetExternalMethodGroup(containingType, syntax.MemberName);
      return Session.ExternResolver.BindExternalMethodGroup(group, containingType, syntax.MemberName, arguments, isStatic, memberKind, Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
    }

    internal BoundExpression BindResolvedExternalMember(
        TypeSymbol containingType,
        BoundExpression receiver,
        string memberName,
        ExternMemberKind memberKind,
        BoundExpression value,
        TextSpan span)
    {
      var arguments = new List<BoundExpression>();
      if (receiver != null)
        arguments.Add(receiver);
      if (value != null)
        arguments.Add(value);
      var group = Session.Environment.ExternCatalog.GetExternalMethodGroup(containingType, memberName);
      return Session.ExternResolver.BindExternalMethodGroup(
          group, containingType, memberName, arguments,
          receiver == null, memberKind, span);
    }
  
    internal BoundExpression BindExternConstructor(NewExpressionSyntax syntax)
    {
      var type = Session.TypeResolver.BindTypeSyntax(syntax.Type);
      if (type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;
      if (Session.ExpressionBinder.IsAggregateStorageType(type))
      {
        Session.Diagnostics.ReportAggregateExternBoundary(syntax.Type.GetSpan(), type.Name);
        return BoundErrorExpression.Instance;
      }
  
      var arguments = new List<BoundExpression>();
      foreach (var argumentSyntax in syntax.Arguments)
        arguments.Add(Session.ExpressionBinder.BindExpression(argumentSyntax));
      var group = Session.Environment.ExternCatalog.GetExternalMethodGroup(type, "new");
      return Session.ExternResolver.BindExternalMethodGroup(group, type, "new", arguments, isStatic: true, ExternMemberKind.Constructor, Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
    }
  
    internal BoundExpression BindExternUnaryOperator(UnaryExpressionSyntax syntax)
    {
      var operand = Session.ExpressionBinder.BindExpression(syntax.Operand);
      if (operand.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;
      if (Session.ExpressionBinder.IsAggregateStorageType(operand.Type))
      {
        Session.Diagnostics.ReportAggregateExternBoundary(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Operand), operand.Type.Name);
        return BoundErrorExpression.Instance;
      }
  
      var methodName = Session.ExternResolver.GetExternOperatorMethodName(syntax.OperatorToken.Kind, unary: true);
      if (methodName == null)
      {
        Session.Diagnostics.ReportUnsupportedExternalExpression(Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
        return BoundErrorExpression.Instance;
      }
  
      var group = Session.Environment.ExternCatalog.GetExternalMethodGroup(operand.Type, methodName);
      if (group != null)
      {
        return Session.ExternResolver.BindExternalMethodGroup(group, operand.Type, methodName, new[] { operand }, isStatic: true, ExternMemberKind.Operator, Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
      }
  
      var builtIn = Session.OperatorResolver.BindUnaryOperator(syntax.OperatorToken.Kind, operand.Type, Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
      return builtIn == null ? BoundErrorExpression.Instance : new BoundUnaryExpression(builtIn, operand);
    }
  
    internal BoundExpression BindExternBinaryOperator(BinaryExpressionSyntax syntax)
    {
      var left = Session.ExpressionBinder.BindExpression(syntax.Left);
      var right = Session.ExpressionBinder.BindExpression(syntax.Right);
      if (left.Type == TypeSymbol.Error || right.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;
      if (Session.ExpressionBinder.IsAggregateStorageType(left.Type) || Session.ExpressionBinder.IsAggregateStorageType(right.Type))
      {
        var rejected = Session.ExpressionBinder.IsAggregateStorageType(left.Type) ? left : right;
        Session.Diagnostics.ReportAggregateExternBoundary(Session.BinderSyntaxFacts.GetExpressionSpan(Session.ExpressionBinder.IsAggregateStorageType(left.Type) ? syntax.Left : syntax.Right), rejected.Type.Name);
        return BoundErrorExpression.Instance;
      }
  
      var methodName = Session.ExternResolver.GetExternOperatorMethodName(syntax.OperatorToken.Kind, unary: false);
      if (methodName == null)
      {
        Session.Diagnostics.ReportUnsupportedExternalExpression(Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
        return BoundErrorExpression.Instance;
      }
  
      var group = Session.Environment.ExternCatalog.GetExternalMethodGroup(left.Type, methodName);
      if (group != null)
      {
        return Session.ExternResolver.BindExternalMethodGroup(group, left.Type, methodName, new[] { left, right }, isStatic: true, ExternMemberKind.Operator, Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
      }
  
      var builtIn = Session.OperatorResolver.BindBinaryOperator(syntax.OperatorToken.Kind, left.Type, right.Type, Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
      return builtIn == null ? BoundErrorExpression.Instance : new BoundBinaryExpression(left, builtIn, right);
    }
  
    internal BoundExpression BindExternalMethodGroup(MethodGroupSymbol group, TypeSymbol containingType, string memberName, IReadOnlyList<BoundExpression> arguments, bool isStatic, ExternMemberKind memberKind, TextSpan span, IReadOnlyList<TypeSymbol> explicitTypeArguments = null)
    {
      if (group == null)
      {
        Session.Diagnostics.ReportUnknownExternalMember(span, containingType.RuntimeQualifiedName, memberName);
        return BoundErrorExpression.Instance;
      }
  
      var applicable = new List<MethodSymbol>();
      var matchingKindCount = 0;
      var matchingGenericArityCount = 0;
      foreach (var method in group.Methods)
      {
        if (method is not ExternMethodSymbol externMethod || externMethod.MemberKind != memberKind || externMethod.IsStatic != isStatic)
        {
          continue;
        }
  
        matchingKindCount++;
        MethodSymbol candidate = method;
        if (explicitTypeArguments != null)
        {
          if (!externMethod.IsGenericDefinition ||
              externMethod.GenericParameters.Count != explicitTypeArguments.Count)
            continue;
          matchingGenericArityCount++;
          if (!Session.ExternResolver.TryConstructGenericMethod(
                  externMethod, explicitTypeArguments, span, out candidate))
            continue;
        }
        else if (externMethod.IsGenericDefinition)
        {
          continue;
        }
        if (candidate.Parameters.Count == arguments.Count && Session.OverloadResolver.IsApplicable(candidate, arguments))
        {
          applicable.Add(candidate);
        }
      }
  
      if (applicable.Count == 0)
      {
        if (explicitTypeArguments != null && matchingKindCount > 0 &&
            matchingGenericArityCount == 0)
        {
          Session.Diagnostics.ReportWrongGenericMethodArity(
              span, group.DisplayName, explicitTypeArguments.Count);
          return BoundErrorExpression.Instance;
        }
        if (matchingKindCount > 0)
        {
          Session.Diagnostics.ReportNoApplicableExternalOverload(span, group.DisplayName, Session.OverloadResolver.BuildArgumentTypeList(arguments));
        }
        else if (group.RejectedCandidates.Count > 0)
        {
          Session.Diagnostics.ReportExternalMemberNotExposed(span, group.DisplayName, Session.OverloadResolver.BuildRejectedCandidateDetail(group.RejectedCandidates));
        }
        else
        {
          Session.Diagnostics.ReportUnknownExternalMember(span, containingType.RuntimeQualifiedName, memberName);
        }
  
        return BoundErrorExpression.Instance;
      }
  
      var selected = Session.OverloadResolver.SelectBestOverload(applicable, arguments, out var ambiguous);
      if (ambiguous || selected == null)
      {
        Session.Diagnostics.ReportAmbiguousExternalOverload(span, group.DisplayName, Session.OverloadResolver.BuildMethodCandidateList(applicable));
        return BoundErrorExpression.Instance;
      }
  
      var resultType = Session.ExternResolver.MapExternalResultType(selected.ReturnType);
      return new BoundCallExpression(new BoundNameExpression(group.DisplayName, group, TypeSymbol.MethodGroupPseudoType), arguments, selected, resultType);
    }

    internal bool TryConstructGenericMethod(
        ExternMethodSymbol definition,
        IReadOnlyList<TypeSymbol> typeArguments,
        TextSpan span,
        out MethodSymbol constructed)
    {
      constructed = null;
      if (definition == null || definition.GenericParameters.Count == 0 ||
          typeArguments == null || definition.GenericParameters.Count != typeArguments.Count)
        return false;

      var substitutions = new Dictionary<TypeSymbol, TypeSymbol>();
      for (var index = 0; index < typeArguments.Count; index++)
      {
        substitutions[definition.GenericParameters[index]] = typeArguments[index];
        if (definition.TypeArguments.Count == typeArguments.Count)
          substitutions[definition.TypeArguments[index]] = typeArguments[index];
      }

      var allConcrete = true;
      var runtimeArguments = new Type[typeArguments.Count];
      for (var index = 0; index < typeArguments.Count; index++)
      {
        allConcrete &= !typeArguments[index].ContainsGenericParameters;
        if (allConcrete &&
            !Session.Environment.ExternCatalog.TryGetClrType(typeArguments[index], out runtimeArguments[index]))
        {
          Session.Diagnostics.ReportGenericExternConstraintViolation(
              span, definition.DisplayName,
              $"runtime type for '{typeArguments[index].Name}' is unavailable");
          return false;
        }
      }
      if (allConcrete && definition.MethodInfo?.IsGenericMethodDefinition == true)
      {
        try
        {
          definition.MethodInfo.MakeGenericMethod(runtimeArguments);
        }
        catch (ArgumentException exception)
        {
          Session.Diagnostics.ReportGenericExternConstraintViolation(
              span, definition.DisplayName, exception.Message);
          return false;
        }
      }

      var parameters = new ParameterSymbol[definition.Parameters.Count];
      for (var index = 0; index < parameters.Length; index++)
      {
        var parameter = definition.Parameters[index];
        parameters[index] = new ParameterSymbol(
            parameter.Name,
            TypeSymbol.Substitute(parameter.Type, substitutions),
            parameter.Ordinal,
            parameter.UdonStorageName,
            parameter.DeclarationSpan);
      }
      var abiParameters = new ExternParameterSymbol[definition.AbiParameters.Count];
      for (var index = 0; index < abiParameters.Length; index++)
      {
        var parameter = definition.AbiParameters[index];
        abiParameters[index] = new ExternParameterSymbol(
            parameter.Name,
            TypeSymbol.Substitute(parameter.Type, substitutions),
            parameter.PassingMode,
            parameter.LogicalInputOrdinal,
            parameter.MaybeProjection);
      }
      constructed = new ExternMethodSymbol(
          definition.Name,
          definition.ContainingType,
          parameters,
          TypeSymbol.Substitute(definition.ReturnType, substitutions),
          definition.MethodBase,
          definition.ExternSignature,
          definition.IsStatic,
          definition.MemberKind,
          abiParameters,
          TypeSymbol.Substitute(definition.AbiReturnType, substitutions),
          definition.GenericParameters,
          definition.GenericConstraints,
          typeArguments);
      return true;
    }
  
    internal bool TryBindExternalReceiver(ExpressionSyntax syntax, out TypeSymbol containingType, out BoundExpression receiver, out bool isStatic)
    {
      if (Session.ExternResolver.TryResolveExternalTypeExpression(syntax, out containingType))
      {
        receiver = null;
        isStatic = true;
        return true;
      }
  
      receiver = Session.ExpressionBinder.BindExpression(syntax);
      if (receiver.Type == TypeSymbol.Error)
      {
        containingType = TypeSymbol.Error;
        isStatic = false;
        return false;
      }
  
      containingType = receiver.Type;
      isStatic = false;
      return true;
    }
  
    internal bool TryResolveExternalTypeExpression(ExpressionSyntax syntax, out TypeSymbol type)
    {
      if (syntax is NameExpressionSyntax name)
      {
        if (name.Name == "Self" && Session.Body.CurrentType != null)
        {
          type = Session.Body.CurrentType;
          return true;
        }
  
        if (Session.Modules.VisibleTypes.TryGetValue(name.Name, out type) || TypeResolver.BuiltInTypes.TryGetValue(name.Name, out type))
        {
          return true;
        }
      }
  
      if (Session.ExternResolver.TryGetQualifiedExpressionText(syntax, out var qualifiedName) && Session.Environment.ExternCatalog.TryGetTypeSymbol(qualifiedName, out type))
      {
        return true;
      }
  
      type = null;
      return false;
    }
  
    internal bool TryGetQualifiedExpressionText(ExpressionSyntax syntax, out string text)
    {
      if (syntax is NameExpressionSyntax name && name.QuestionToken == null)
      {
        text = name.Name;
        return true;
      }
  
      if (syntax is MemberAccessExpressionSyntax member && member.QuestionToken == null && Session.ExternResolver.TryGetQualifiedExpressionText(member.Expression, out var receiverText))
      {
        text = $"{receiverText}.{member.Name.Text}";
        return true;
      }
  
      text = null;
      return false;
    }
  
    internal TypeSymbol MapExternalResultType(TypeSymbol runtimeType)
    {
      if (runtimeType == null)
        return TypeSymbol.Error;
      if (runtimeType.TypeKind == TypeKind.Tuple)
      {
        var elements = new TypeSymbol[runtimeType.TupleElementTypes.Count];
        for (var index = 0; index < elements.Length; index++)
          elements[index] = Session.ExternResolver.MapExternalResultType(runtimeType.TupleElementTypes[index]);
        return TypeSymbol.Tuple(elements);
      }
  
      return Session.Declarations.ExternalBindingsByRuntimeType.TryGetValue(runtimeType.RuntimeQualifiedName, out var binding) && Session.Modules.VisibleTypes.ContainsValue(binding) ? binding : runtimeType;
    }
  
    internal string GetExternOperatorMethodName(SyntaxKind kind, bool unary)
    {
      if (unary)
      {
        return kind switch
        {
          SyntaxKind.PlusToken => "op_UnaryPlus",
          SyntaxKind.MinusToken => "op_UnaryNegation",
          SyntaxKind.BangToken => "op_LogicalNot",
          SyntaxKind.TildeToken => "op_OnesComplement",
          _ => null
        };
      }
  
      return kind switch
      {
        SyntaxKind.PlusToken => "op_Addition",
        SyntaxKind.MinusToken => "op_Subtraction",
        SyntaxKind.StarToken => "op_Multiply",
        SyntaxKind.SlashToken => "op_Division",
        SyntaxKind.PercentToken => "op_Modulus",
        SyntaxKind.EqualsEqualsToken => "op_Equality",
        SyntaxKind.BangEqualsToken => "op_Inequality",
        SyntaxKind.LessToken => "op_LessThan",
        SyntaxKind.LessOrEqualsToken => "op_LessThanOrEqual",
        SyntaxKind.GreaterToken => "op_GreaterThan",
        SyntaxKind.GreaterOrEqualsToken => "op_GreaterThanOrEqual",
        SyntaxKind.AmpersandToken => "op_BitwiseAnd",
        SyntaxKind.PipeToken => "op_BitwiseOr",
        SyntaxKind.CaretToken => "op_ExclusiveOr",
        SyntaxKind.LessLessToken => "op_LeftShift",
        SyntaxKind.GreaterGreaterToken => "op_RightShift",
        _ => null
      };
    }
  }
}
