using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class NetworkSendBinder : BinderComponent
  {
    internal NetworkSendBinder(BindingSession session) : base(session)
    {
    }
  
    internal BoundStatement BindNetworkSendStatement(SendStatementSyntax syntax)
    {
      var receiverName = syntax.ReceiverName.Text ?? string.Empty;
      Session.Callables.NetworkReceiveSymbols.TryGetValue(receiverName, out var receiver);
      IReadOnlyList<ParameterSymbol> expectedParameters = receiver?.Parameters ?? Array.Empty<ParameterSymbol>();
      var arguments = Session.CallExpressionBinder.BindArguments(syntax.Arguments, expectedParameters);
      if (receiver == null)
      {
        if (Session.Modules.VisibleFunctions.ContainsKey(receiverName))
        {
          Session.Diagnostics.ReportFunctionIsNotNetworkReceiver(syntax.ReceiverName.Span, receiverName);
        }
        else
        {
          Session.Diagnostics.ReportUnknownNetworkReceiver(syntax.ReceiverName.Span, receiverName);
        }
      }
      else
      {
        if (arguments.Count != receiver.Parameters.Count)
        {
          Session.Diagnostics.ReportNetworkArgumentCountMismatch(Session.BinderSyntaxFacts.GetStatementSpan(syntax), receiver.Name, receiver.Parameters.Count, arguments.Count);
        }
  
        var comparableCount = Math.Min(arguments.Count, receiver.Parameters.Count);
        for (var index = 0; index < comparableCount; index++)
        {
          if (arguments[index].Type == TypeSymbol.Error || Session.ConversionClassifier.CanAssignToLocal(receiver.Parameters[index].Type, arguments[index].Type))
          {
            continue;
          }
  
          Session.Diagnostics.ReportNetworkArgumentTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Arguments[index]), receiver.Name, index, receiver.Parameters[index].Type.Name, arguments[index].Type.Name);
        }
      }
  
      var target = Session.NetworkSendBinder.BindNetworkSendTarget(syntax.Target);
      var targetType = Session.NetworkSendBinder.GetNetworkEventTargetType(
          Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target));
  
      if (target.Type != TypeSymbol.Error && targetType != TypeSymbol.Error && !Session.NetworkSendBinder.HaveSameRuntimeType(target.Type, targetType))
      {
        Session.Diagnostics.ReportNetworkTargetTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), targetType.Name, target.Type.Name);
      }
  
      if (receiver == null || target.Type == TypeSymbol.Error || !Session.Environment.ExternCatalog.TryGetTypeSymbol(typeof(UdonBehaviour), out var behaviourType))
      {
        return new BoundExpressionStatement(BoundErrorExpression.Instance);
      }
  
      return new BoundNetworkSendStatement(receiver, arguments, target, behaviourType, Session.NetworkSendBinder.BuildNetworkSendExternSignature(receiver.PhysicalParameters.Count));
    }
  
    internal BoundExpression BindNetworkSendTarget(ExpressionSyntax syntax)
    {
      if (syntax is NameExpressionSyntax name &&
          Session.NetworkSendBinder.TryGetContextualNetworkTargetMember(name.Name, out var memberName))
      {
        var span = Session.BinderSyntaxFacts.GetExpressionSpan(syntax);
        if (!Session.LanguageItems.TryGetType(
                LanguageItemNames.NetworkEventTarget,
                out var targetType))
        {
          return BoundErrorExpression.Instance;
        }
        if (targetType != TypeSymbol.Error &&
            Session.NetworkSendBinder.TryBindExternalEnumConstant(targetType, memberName, span, out var enumConstant))
        {
          return enumConstant;
        }
      }
  
      return Session.ExpressionBinder.BindExpression(syntax);
    }

    internal TypeSymbol GetNetworkEventTargetType(TextSpan span)
    {
      if (Session.LanguageItems.TryGetType(
          LanguageItemNames.NetworkEventTarget,
          out var targetType))
      {
        return targetType;
      }

      Session.Diagnostics.ReportMissingLanguageItem(
          span,
          LanguageItemNames.NetworkEventTarget);
      return TypeSymbol.Error;
    }
  
    internal bool TryBindExternalEnumConstant(TypeSymbol containingType, string memberName, TextSpan span, out BoundExpression expression)
    {
      expression = null;
      if (!Session.Environment.ExternCatalog.TryGetClrType(containingType, out var clrType) || !clrType.IsEnum)
      {
        return false;
      }
  
      var field = clrType.GetField(memberName);
      if (field == null || !field.IsLiteral || !field.IsStatic)
        return false;
      expression = new BoundLiteralExpression(field.GetValue(null), containingType, span);
      return true;
    }
  
    internal bool TryGetContextualNetworkTargetMember(string name, out string memberName)
    {
      memberName = name switch
      {
        "all" => "All",
        "others" => "Others",
        "owner" => "Owner",
        "self" => "Self",
        _ => null
      };
      return memberName != null;
    }
  
    internal bool HaveSameRuntimeType(TypeSymbol left, TypeSymbol right)
    {
      return left == right || string.Equals(left?.RuntimeQualifiedName, right?.RuntimeQualifiedName, StringComparison.Ordinal);
    }
  
    internal string BuildNetworkSendExternSignature(int parameterCount)
    {
      var signature = "VRCSDK3UdonNetworkCallingNetworkCalling.__SendCustomNetworkEvent__" + "VRCUdonCommonInterfacesIUdonEventReceiver_" + "VRCUdonCommonInterfacesNetworkEventTarget_SystemString";
      for (var index = 0; index < parameterCount; index++)
        signature += "_SystemObject";
      return signature + "__SystemVoid";
    }
  }
}
