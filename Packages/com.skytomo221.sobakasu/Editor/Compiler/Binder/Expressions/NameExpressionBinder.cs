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
  internal sealed class NameExpressionBinder : BinderComponent
  {
    internal NameExpressionBinder(BindingSession session) : base(session)
    {
    }
  
    internal BoundExpression BindNameExpression(NameExpressionSyntax syntax, TypeSymbol expectedType = null)
    {
      var name = syntax.Name;
      var span = Session.BinderSyntaxFacts.GetExpressionSpan(syntax);
      if (string.Equals(name, "self", StringComparison.Ordinal) && Session.Body.CurrentFunction?.IsMethod == true && Session.Body.CurrentFunction.IsStatic)
      {
        Session.Diagnostics.ReportSelfUnavailableInStaticFunction(span);
        return BoundErrorExpression.Instance;
      }
  
      if (string.Equals(name, "Self", StringComparison.Ordinal))
      {
        if (Session.Body.CurrentType != null)
          return new BoundNameExpression(name, Session.Body.CurrentType, Session.Body.CurrentType);
        Session.Diagnostics.ReportSelfTypeOutsideImpl(span);
        return BoundErrorExpression.Instance;
      }
  
      var scopedSymbol = Session.NameResolver.LookupScopedSymbol(name);
      if (scopedSymbol != null)
      {
        return new BoundNameExpression(name, scopedSymbol, Session.NameResolver.GetExpressionType(scopedSymbol));
      }
  
      if ((Session.Modules.CurrentModule == null || Session.Modules.CurrentModule.IsEntry) && Session.Declarations.StateSymbols.TryGetValue(name, out var stateSymbol))
      {
        return new BoundNameExpression(name, stateSymbol, stateSymbol.Type);
      }
  
      if (Session.Modules.VisibleConstants.TryGetValue(name, out var constantSymbol))
      {
        Session.ConstantDependencyAnalyzer.EnsureConstantBound(constantSymbol, span);
        return new BoundNameExpression(name, constantSymbol, constantSymbol.Type);
      }
  
      if (Session.NameResolver.TryGetCurrentModuleType(name, out var declaredType))
        return new BoundNameExpression(name, declaredType, declaredType);
      var hasFunction = Session.NameResolver.TryGetCurrentModuleFunctionGroup(name, out var functionGroup);
      var visibleSymbol = Session.NameResolver.ResolveVisibleSymbol(name, span, out var resolutionHadDiagnostic);
      if (hasFunction && Session.NameResolver.IsExternCallableSymbol(visibleSymbol))
      {
        Session.Diagnostics.ReportAmbiguousUserFunctionExternCall(span, name, Session.NameResolver.GetSymbolDisplayName(visibleSymbol));
        return BoundErrorExpression.Instance;
      }
  
      if (hasFunction)
        return Session.CallExpressionBinder.BindImplicitFunctionGroupCall(span, functionGroup);
      if (visibleSymbol is FunctionGroupSymbol visibleFunctions)
        return Session.CallExpressionBinder.BindImplicitFunctionGroupCall(span, visibleFunctions);
      if (visibleSymbol is ConstantSymbol visibleConstant)
      {
        Session.ConstantDependencyAnalyzer.EnsureConstantBound(visibleConstant, span);
        return new BoundNameExpression(name, visibleConstant, visibleConstant.Type);
      }
  
      if (visibleSymbol is EnumVariantSymbol enumVariant)
        return Session.NameExpressionBinder.BindUnitEnumVariant(enumVariant, expectedType, span);
      if (visibleSymbol == null)
      {
        if (resolutionHadDiagnostic)
          return new BoundNameExpression(name, null, TypeSymbol.Error);
        Session.Diagnostics.ReportUndefinedName(span, name);
        return new BoundNameExpression(name, null, TypeSymbol.Error);
      }
  
      if (visibleSymbol is MethodGroupSymbol methodGroup)
        return Session.NameExpressionBinder.BindImplicitMethodCall(syntax, methodGroup);
      return new BoundNameExpression(name, visibleSymbol, Session.NameResolver.GetExpressionType(visibleSymbol));
    }
  
    internal BoundExpression BindUnitEnumVariant(EnumVariantSymbol variant, TypeSymbol expectedType, TextSpan span)
    {
      if (variant.VariantKind != EnumVariantKind.Unit)
      {
        Session.Diagnostics.ReportEnumVariantRequiresPayload(span, variant.ContainingType.Name, variant.Name);
        return BoundErrorExpression.Instance;
      }
  
      if (variant.ContainingType.IsGenericDefinition)
      {
        var substitutions = new Dictionary<TypeSymbol, TypeSymbol>();
        Session.GenericInference.SeedInferenceFromExpectedType(variant.ContainingType, expectedType, substitutions);
        if (!Session.GenericInference.CompleteTypeArgumentInference(variant.ContainingType, substitutions, span, out var constructed) || !constructed.TryGetEnumVariant(variant.Name, out variant))
        {
          return BoundErrorExpression.Instance;
        }
      }
  
      return new BoundEnumConstructionExpression(variant, Array.Empty<BoundAggregateFieldInitializer>());
    }
  
    internal BoundExpression BindImplicitMethodCall(NameExpressionSyntax syntax, MethodGroupSymbol methodGroup)
    {
      var target = new BoundNameExpression(syntax.Name, methodGroup, Session.NameResolver.GetExpressionType(methodGroup));
      if (methodGroup.Methods.Count > 0)
      {
        var hasZeroArgumentCandidate = false;
        foreach (var method in methodGroup.Methods)
        {
          if (method.Parameters.Count == 0)
          {
            hasZeroArgumentCandidate = true;
            break;
          }
        }
  
        if (!hasZeroArgumentCandidate)
        {
          Session.Diagnostics.ReportCallableRequiresArguments(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.DisplayName, Session.OverloadResolver.GetSharedParameterCount(methodGroup.Methods));
          return new BoundCallExpression(target, Array.Empty<BoundExpression>(), null, TypeSymbol.Error);
        }
      }
  
      var end = Session.BinderSyntaxFacts.GetExpressionSpan(syntax).End;
      var openParen = new SyntaxToken(SyntaxKind.LeftParen, new TextSpan(end, 0), string.Empty);
      var closeParen = new SyntaxToken(SyntaxKind.RightParen, new TextSpan(end, 0), string.Empty);
      var implicitCall = new CallExpressionSyntax(syntax, openParen, Array.Empty<ExpressionSyntax>(), closeParen);
      return Session.CallExpressionBinder.BindMethodCall(implicitCall, target, methodGroup, Array.Empty<BoundExpression>());
    }
  
    internal BoundExpression BindImplicitUserMethodCall(MemberAccessExpressionSyntax syntax, BoundExpression receiver, MethodGroupSymbol methodGroup)
    {
      var target = new BoundMemberAccessExpression(receiver, syntax.MemberName, methodGroup, TypeSymbol.MethodGroupPseudoType);
      var end = syntax.QuestionToken?.Span.End ?? syntax.Name.Span.End;
      var implicitCall = new CallExpressionSyntax(syntax, new SyntaxToken(SyntaxKind.LeftParen, new TextSpan(end, 0), string.Empty), Array.Empty<ExpressionSyntax>(), new SyntaxToken(SyntaxKind.RightParen, new TextSpan(end, 0), string.Empty));
      return Session.CallExpressionBinder.BindMethodCall(implicitCall, target, methodGroup, Array.Empty<BoundExpression>());
    }
  }
}
