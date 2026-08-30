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
      if (syntax.Target is not MemberAccessExpressionSyntax member)
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
      return Session.ExternResolver.BindExternalMethodGroup(group, containingType, member.MemberName, arguments, isStatic, ExternMemberKind.Method, Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
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
  
    internal BoundExpression BindExternalMethodGroup(MethodGroupSymbol group, TypeSymbol containingType, string memberName, IReadOnlyList<BoundExpression> arguments, bool isStatic, ExternMemberKind memberKind, TextSpan span)
    {
      if (group == null)
      {
        Session.Diagnostics.ReportUnknownExternalMember(span, containingType.RuntimeQualifiedName, memberName);
        return BoundErrorExpression.Instance;
      }
  
      var applicable = new List<MethodSymbol>();
      var matchingKindCount = 0;
      foreach (var method in group.Methods)
      {
        if (method is not ExternMethodSymbol externMethod || externMethod.MemberKind != memberKind || externMethod.IsStatic != isStatic)
        {
          continue;
        }
  
        matchingKindCount++;
        if (method.Parameters.Count == arguments.Count && Session.OverloadResolver.IsApplicable(method, arguments))
        {
          applicable.Add(method);
        }
      }
  
      if (applicable.Count == 0)
      {
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
