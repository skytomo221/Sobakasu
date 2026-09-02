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
  internal sealed class CallExpressionBinder : BinderComponent
  {
    internal CallExpressionBinder(BindingSession session) : base(session)
    {
    }
  
    internal BoundExpression BindCallExpression(CallExpressionSyntax syntax, TypeSymbol expectedType = null)
    {
      if (syntax.Target is GenericTypeExpressionSyntax genericApplication)
        return Session.CallExpressionBinder.BindExplicitGenericCall(
            syntax, genericApplication);

      if (syntax.Target is MemberAccessExpressionSyntax enumVariantTarget && Session.AggregateExpressionBinder.TryResolveEnumVariant(enumVariantTarget, out var enumVariant, out var enumTargetHandled))
      {
        if (enumVariant == null)
        {
          foreach (var argument in syntax.Arguments)
            Session.ExpressionBinder.BindExpression(argument);
          return BoundErrorExpression.Instance;
        }
  
        if (enumVariant.VariantKind != EnumVariantKind.Tuple)
        {
          foreach (var argument in syntax.Arguments)
            Session.ExpressionBinder.BindExpression(argument);
          Session.Diagnostics.ReportEnumVariantConstructionForm(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), enumVariant.ContainingType.Name, enumVariant.Name, "tuple");
          return BoundErrorExpression.Instance;
        }
  
        return Session.CallExpressionBinder.BindTupleEnumVariant(syntax, enumVariant, expectedType);
      }
  
      if (syntax.Target is NameExpressionSyntax importedVariantTarget && Session.NameResolver.TryResolveImportedEnumVariant(importedVariantTarget, out var importedVariant))
      {
        if (importedVariant.VariantKind != EnumVariantKind.Tuple)
        {
          foreach (var argument in syntax.Arguments)
            Session.ExpressionBinder.BindExpression(argument);
          Session.Diagnostics.ReportEnumVariantConstructionForm(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), importedVariant.ContainingType.Name, importedVariant.Name, "tuple");
          return BoundErrorExpression.Instance;
        }
  
        return Session.CallExpressionBinder.BindTupleEnumVariant(syntax, importedVariant, expectedType);
      }
  
      if (syntax.Target is MemberAccessExpressionSyntax arrayLengthSyntax && string.Equals(arrayLengthSyntax.MemberName, "length", StringComparison.Ordinal))
      {
        var lengthReceiver = Session.ExpressionBinder.BindExpression(arrayLengthSyntax.Expression);
        if (lengthReceiver.Type.TypeKind == TypeKind.Array)
        {
          if (syntax.Arguments.Count != 0)
          {
            Session.Diagnostics.ReportInvalidArgumentCount(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), "length", 0, syntax.Arguments.Count);
            return BoundErrorExpression.Instance;
          }
  
          return Session.CallExpressionBinder.BindArrayLengthExpression(lengthReceiver, arrayLengthSyntax.Name.Span);
        }
      }
  
      if (syntax.Target is NameExpressionSyntax contextualName && Session.CallExpressionBinder.TryResolveContextualUserFunction(contextualName.Name, Session.BinderSyntaxFacts.GetExpressionSpan(contextualName), out var contextualFunction))
      {
        return Session.CallExpressionBinder.BindUserFunctionCall(syntax, contextualFunction, Session.CallExpressionBinder.BindArguments(syntax.Arguments, contextualFunction.Parameters));
      }
  
      var arguments = new List<BoundExpression>();
      foreach (var argument in syntax.Arguments)
        arguments.Add(Session.ExpressionBinder.BindExpression(argument));
      if (syntax.Target is NameExpressionSyntax nameExpression)
        return Session.CallExpressionBinder.BindSimpleNameCall(syntax, nameExpression, arguments);
      if (syntax.Target is MemberAccessExpressionSyntax memberAccessSyntax)
      {
        var receiver = Session.ExpressionBinder.BindExpression(memberAccessSyntax.Expression);
        if (receiver.Type == TypeSymbol.Error)
          return BoundErrorExpression.Instance;
        var memberSymbol = Session.MemberResolver.LookupMember(receiver, memberAccessSyntax.MemberName, memberAccessSyntax.Name.Span, out var memberDiagnosticReported);
        if (memberSymbol is FunctionGroupSymbol moduleFunctions)
          return Session.CallExpressionBinder.BindFunctionGroupCall(syntax, moduleFunctions, arguments);
        if (memberSymbol is not MethodGroupSymbol memberMethodGroup)
        {
          if (!memberDiagnosticReported)
          {
            Session.Diagnostics.ReportUndefinedMember(memberAccessSyntax.Name.Span, Session.NameResolver.GetReceiverDisplayName(receiver), memberAccessSyntax.MemberName);
          }
  
          return BoundErrorExpression.Instance;
        }
  
        var memberTarget = new BoundMemberAccessExpression(receiver, memberAccessSyntax.MemberName, memberMethodGroup, TypeSymbol.MethodGroupPseudoType);
        return Session.CallExpressionBinder.BindMethodCall(syntax, memberTarget, memberMethodGroup, arguments);
      }
  
      var target = Session.ExpressionBinder.BindExpression(syntax.Target);
      if (target.Type == TypeSymbol.Error)
      {
        return new BoundCallExpression(target, arguments, null, TypeSymbol.Error);
      }
  
      if (Session.NameResolver.GetReferencedSymbol(target)is MethodGroupSymbol methodGroup)
        return Session.CallExpressionBinder.BindMethodCall(syntax, target, methodGroup, arguments);
      Session.Diagnostics.ReportCallTargetIsNotMethod(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), Session.NameResolver.GetCallTargetDisplayName(target));
      return new BoundCallExpression(target, arguments, null, TypeSymbol.Error);
    }

    internal BoundExpression BindExplicitGenericCall(
        CallExpressionSyntax syntax,
        GenericTypeExpressionSyntax application)
    {
      var typeArguments = Session.TypeResolver.BindTypeArguments(
          application.TypeArgumentList);
      if (Session.TypeResolver.ContainsTypeError(typeArguments))
        return BoundErrorExpression.Instance;

      var arguments = new List<BoundExpression>();
      foreach (var argumentSyntax in syntax.Arguments)
        arguments.Add(Session.ExpressionBinder.BindExpression(argumentSyntax));

      if (application.Target is MemberAccessExpressionSyntax member)
      {
        var receiver = Session.ExpressionBinder.BindExpression(member.Expression);
        if (receiver.Type == TypeSymbol.Error)
          return BoundErrorExpression.Instance;
        var symbol = Session.MemberResolver.LookupMember(
            receiver, member.MemberName, member.Name.Span, out var reported);
        if (symbol is not MethodGroupSymbol methodGroup)
        {
          if (!reported)
            Session.Diagnostics.ReportUndefinedMember(
                member.Name.Span,
                Session.NameResolver.GetReceiverDisplayName(receiver),
                member.MemberName);
          return BoundErrorExpression.Instance;
        }
        return Session.CallExpressionBinder.BindExplicitGenericMethodGroup(
            syntax, receiver, methodGroup, arguments, typeArguments);
      }

      if (application.Target is NameExpressionSyntax name &&
          Session.Modules.VisibleFunctions.TryGetValue(name.Name, out var functionGroup))
      {
        return Session.CallExpressionBinder.BindExplicitGenericFunctionGroup(
            syntax, functionGroup, arguments, typeArguments);
      }

      Session.Diagnostics.ReportCallTargetIsNotMethod(
          Session.BinderSyntaxFacts.GetExpressionSpan(application.Target),
          application.Target.GetType().Name);
      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindExplicitGenericMethodGroup(
        CallExpressionSyntax syntax,
        BoundExpression receiver,
        MethodGroupSymbol group,
        IReadOnlyList<BoundExpression> arguments,
        IReadOnlyList<TypeSymbol> typeArguments)
    {
      var candidates = new List<KeyValuePair<ExternMethodSymbol, IReadOnlyList<BoundExpression>>>();
      foreach (var method in group.Methods)
      {
        ExternMethodSymbol openExtern = method as ExternMethodSymbol;
        if (method is UserMethodSymbol userMethod)
          openExtern = userMethod.Function.ExternalBinding?.ExternalMethod;
        if (openExtern == null || openExtern.GenericParameters.Count != typeArguments.Count)
          continue;
        if (!Session.ExternResolver.TryConstructGenericMethod(
                openExtern, typeArguments,
                Session.BinderSyntaxFacts.GetExpressionSpan(syntax),
                out var constructedMethod))
          continue;
        var constructedExtern = (ExternMethodSymbol)constructedMethod;
        var logicalArguments = new List<BoundExpression>();
        if (!constructedExtern.IsStatic)
          logicalArguments.Add(receiver);
        logicalArguments.AddRange(arguments);
        if (constructedExtern.Parameters.Count == logicalArguments.Count &&
            Session.OverloadResolver.IsApplicable(constructedExtern, logicalArguments))
        {
          candidates.Add(new KeyValuePair<ExternMethodSymbol, IReadOnlyList<BoundExpression>>(
              constructedExtern, logicalArguments));
        }
      }
      return Session.CallExpressionBinder.SelectExplicitGenericExternCandidate(
          syntax, group.DisplayName, candidates);
    }

    private BoundExpression BindExplicitGenericFunctionGroup(
        CallExpressionSyntax syntax,
        FunctionGroupSymbol group,
        IReadOnlyList<BoundExpression> arguments,
        IReadOnlyList<TypeSymbol> typeArguments)
    {
      var candidates = new List<KeyValuePair<ExternMethodSymbol, IReadOnlyList<BoundExpression>>>();
      foreach (var function in group.Functions)
      {
        var openExtern = function.ExternalBinding?.ExternalMethod;
        if (function.GenericParameters.Count != typeArguments.Count || openExtern == null)
          continue;
        if (!Session.ExternResolver.TryConstructGenericMethod(
                openExtern, typeArguments,
                Session.BinderSyntaxFacts.GetExpressionSpan(syntax),
                out var constructedMethod))
          continue;
        var constructedExtern = (ExternMethodSymbol)constructedMethod;
        if (constructedExtern.Parameters.Count == arguments.Count &&
            Session.OverloadResolver.IsApplicable(constructedExtern, arguments))
        {
          candidates.Add(new KeyValuePair<ExternMethodSymbol, IReadOnlyList<BoundExpression>>(
              constructedExtern, arguments));
        }
      }
      return Session.CallExpressionBinder.SelectExplicitGenericExternCandidate(
          syntax, group.Name, candidates);
    }

    private BoundExpression SelectExplicitGenericExternCandidate(
        CallExpressionSyntax syntax,
        string displayName,
        IReadOnlyList<KeyValuePair<ExternMethodSymbol, IReadOnlyList<BoundExpression>>> candidates)
    {
      if (candidates.Count == 0)
      {
        Session.Diagnostics.ReportNoMatchingOverload(
            Session.BinderSyntaxFacts.GetExpressionSpan(syntax),
            displayName,
            string.Empty);
        return BoundErrorExpression.Instance;
      }
      var methods = new List<MethodSymbol>();
      foreach (var candidate in candidates)
        methods.Add(candidate.Key);
      var selected = Session.OverloadResolver.SelectBestOverload(
          methods, candidates[0].Value, out var ambiguous) as ExternMethodSymbol;
      if (ambiguous || selected == null)
      {
        Session.Diagnostics.ReportAmbiguousExternOverload(
            Session.BinderSyntaxFacts.GetExpressionSpan(syntax),
            displayName,
            Session.OverloadResolver.BuildMethodCandidateList(methods));
        return BoundErrorExpression.Instance;
      }
      foreach (var candidate in candidates)
      {
        if (!ReferenceEquals(candidate.Key, selected))
          continue;
        return new BoundCallExpression(
            new BoundNameExpression(displayName, selected, TypeSymbol.MethodGroupPseudoType),
            candidate.Value,
            selected,
            selected.ReturnType);
      }
      return BoundErrorExpression.Instance;
    }
  
    internal BoundExpression BindTupleEnumVariant(CallExpressionSyntax syntax, EnumVariantSymbol variant, TypeSymbol expectedType)
    {
      if (variant.ContainingType.IsGenericDefinition)
        return Session.CallExpressionBinder.BindInferredTupleEnumVariant(syntax, variant, expectedType);
      if (syntax.Arguments.Count != variant.Fields.Count)
      {
        Session.Diagnostics.ReportEnumTuplePayloadArity(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), variant.ContainingType.Name, variant.Name, variant.Fields.Count, syntax.Arguments.Count);
      }
  
      var initializers = new List<BoundAggregateFieldInitializer>();
      for (var index = 0; index < syntax.Arguments.Count; index++)
      {
        var field = index < variant.Fields.Count ? variant.Fields[index] : null;
        var argument = Session.ExpressionBinder.BindExpression(syntax.Arguments[index], field?.Type);
        if (field == null)
          continue;
        if (!Session.ConversionClassifier.CanAssignToLocal(field.Type, argument.Type))
        {
          Session.Diagnostics.ReportEnumTuplePayloadTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Arguments[index]), variant.ContainingType.Name, variant.Name, index, field.Type.Name, argument.Type.Name);
        }
  
        initializers.Add(new BoundAggregateFieldInitializer(field, argument));
      }
  
      return new BoundEnumConstructionExpression(variant, initializers);
    }
  
    internal BoundExpression BindInferredTupleEnumVariant(CallExpressionSyntax syntax, EnumVariantSymbol templateVariant, TypeSymbol expectedType)
    {
      var definition = templateVariant.ContainingType;
      var substitutions = new Dictionary<TypeSymbol, TypeSymbol>();
      Session.GenericInference.SeedInferenceFromExpectedType(definition, expectedType, substitutions);
      if (syntax.Arguments.Count != templateVariant.Fields.Count)
      {
        Session.Diagnostics.ReportEnumTuplePayloadArity(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), definition.Name, templateVariant.Name, templateVariant.Fields.Count, syntax.Arguments.Count);
      }
  
      var arguments = new List<BoundExpression>();
      for (var index = 0; index < syntax.Arguments.Count; index++)
      {
        var templateField = index < templateVariant.Fields.Count ? templateVariant.Fields[index] : null;
        var contextualType = templateField == null ? null : Session.GenericSubstitution.Substitute(templateField.Type, substitutions);
        if (contextualType?.ContainsGenericParameters == true)
          contextualType = null;
        var argument = Session.ExpressionBinder.BindExpression(syntax.Arguments[index], contextualType);
        arguments.Add(argument);
        if (templateField != null)
        {
          Session.GenericInference.InferTypeArguments(templateField.Type, argument.Type, substitutions, Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Arguments[index]));
        }
      }
  
      if (!Session.GenericInference.CompleteTypeArgumentInference(definition, substitutions, Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), out var constructed) || !constructed.TryGetEnumVariant(templateVariant.Name, out var variant))
      {
        return BoundErrorExpression.Instance;
      }
  
      var initializers = new List<BoundAggregateFieldInitializer>();
      for (var index = 0; index < arguments.Count; index++)
      {
        if (index >= variant.Fields.Count)
          continue;
        var field = variant.Fields[index];
        var argument = arguments[index];
        if (!Session.ConversionClassifier.CanAssignToLocal(field.Type, argument.Type))
        {
          Session.Diagnostics.ReportEnumTuplePayloadTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Arguments[index]), constructed.Name, variant.Name, index, field.Type.Name, argument.Type.Name);
        }
  
        initializers.Add(new BoundAggregateFieldInitializer(field, argument));
      }
  
      return new BoundEnumConstructionExpression(variant, initializers);
    }
  
    internal IReadOnlyList<BoundExpression> BindArguments(IReadOnlyList<ExpressionSyntax> syntaxArguments, IReadOnlyList<ParameterSymbol> parameters)
    {
      var arguments = new List<BoundExpression>(syntaxArguments.Count);
      for (var index = 0; index < syntaxArguments.Count; index++)
      {
        var expectedType = index < parameters.Count ? parameters[index].Type : null;
        arguments.Add(Session.ExpressionBinder.BindExpression(syntaxArguments[index], expectedType));
      }
  
      return arguments;
    }
  
    internal bool RequiresContextualArrayBinding(IReadOnlyList<ExpressionSyntax> arguments)
    {
      foreach (var argument in arguments)
      {
        if (argument is ArrayLiteralExpressionSyntax)
        {
          return true;
        }
      }
  
      return false;
    }
  
    internal bool TryResolveContextualUserFunction(string name, TextSpan span, out FunctionSymbol function)
    {
      function = null;
      if (Session.NameResolver.LookupScopedSymbol(name) != null || (Session.Modules.CurrentModule == null || Session.Modules.CurrentModule.IsEntry) && Session.Declarations.StateSymbols.ContainsKey(name) || Session.Modules.VisibleConstants.ContainsKey(name) || Session.NameResolver.ResolveVisibleSymbol(name, span) is ConstantSymbol)
      {
        return false;
      }
  
      var hasCurrent = Session.NameResolver.TryGetCurrentModuleFunctionGroup(name, out var currentFunctions);
      var visible = Session.NameResolver.ResolveVisibleSymbol(name, span);
      if (hasCurrent && currentFunctions.Functions.Count == 1 && !Session.NameResolver.IsExternCallableSymbol(visible))
      {
        function = currentFunctions.Functions[0];
        return true;
      }
  
      if (!hasCurrent && visible is FunctionGroupSymbol visibleFunctions && visibleFunctions.Functions.Count == 1)
      {
        function = visibleFunctions.Functions[0];
        return true;
      }
  
      return false;
    }
  
    internal BoundExpression BindArrayLengthExpression(BoundExpression array, TextSpan span)
    {
      if (Session.ExpressionBinder.IsAggregateStorageType(array.Type))
      {
        return new BoundArrayLengthExpression(array, null, Session.ExpressionBinder.GetAggregateArrayIntrinsics(array.Type));
      }
  
      if (!Session.Environment.ExternCatalog.TryGetArrayIntrinsics(array.Type, out var intrinsics, out var reason))
      {
        Session.Diagnostics.ReportArrayTypeNotAvailable(span, array.Type.Name, reason);
        return BoundErrorExpression.Instance;
      }
  
      return new BoundArrayLengthExpression(array, intrinsics);
    }
  
    internal BoundExpression BindSimpleNameCall(CallExpressionSyntax syntax, NameExpressionSyntax nameExpression, IReadOnlyList<BoundExpression> arguments)
    {
      var name = nameExpression.Name;
      var span = Session.BinderSyntaxFacts.GetExpressionSpan(nameExpression);
      var scopedSymbol = Session.NameResolver.LookupScopedSymbol(name);
      if (scopedSymbol != null)
      {
        var scopedTarget = new BoundNameExpression(name, scopedSymbol, Session.NameResolver.GetExpressionType(scopedSymbol));
        Session.Diagnostics.ReportCallTargetIsNotMethod(span, name);
        return new BoundCallExpression(scopedTarget, arguments, null, TypeSymbol.Error);
      }
  
      var hasFunction = Session.NameResolver.TryGetCurrentModuleFunctionGroup(name, out var functionGroup);
      var visibleSymbol = Session.NameResolver.ResolveVisibleSymbol(name, span, out var resolutionHadDiagnostic);
      if (hasFunction && Session.NameResolver.IsExternCallableSymbol(visibleSymbol))
      {
        Session.Diagnostics.ReportAmbiguousUserFunctionExternCall(span, name, Session.NameResolver.GetSymbolDisplayName(visibleSymbol));
        return new BoundCallExpression(new BoundNameExpression(name, visibleSymbol, Session.NameResolver.GetExpressionType(visibleSymbol)), arguments, null, TypeSymbol.Error);
      }
  
      if (hasFunction)
        return Session.CallExpressionBinder.BindFunctionGroupCall(syntax, functionGroup, arguments);
      if (visibleSymbol is FunctionGroupSymbol visibleFunctions)
        return Session.CallExpressionBinder.BindFunctionGroupCall(syntax, visibleFunctions, arguments);
      if (visibleSymbol == null)
      {
        if (resolutionHadDiagnostic)
        {
          return new BoundCallExpression(new BoundNameExpression(name, null, TypeSymbol.Error), arguments, null, TypeSymbol.Error);
        }
  
        Session.Diagnostics.ReportUndefinedName(span, name);
        return new BoundCallExpression(new BoundNameExpression(name, null, TypeSymbol.Error), arguments, null, TypeSymbol.Error);
      }
  
      var target = new BoundNameExpression(name, visibleSymbol, Session.NameResolver.GetExpressionType(visibleSymbol));
      if (visibleSymbol is MethodGroupSymbol methodGroup)
        return Session.CallExpressionBinder.BindMethodCall(syntax, target, methodGroup, arguments);
      Session.Diagnostics.ReportCallTargetIsNotMethod(span, name);
      return new BoundCallExpression(target, arguments, null, TypeSymbol.Error);
    }
  
    internal BoundExpression BindUserFunctionCall(CallExpressionSyntax syntax, FunctionSymbol functionSymbol, IReadOnlyList<BoundExpression> arguments)
    {
      if (Session.NameResolver.ContainsError(arguments))
        return new BoundUserFunctionCallExpression(functionSymbol, arguments);
      if (functionSymbol.Parameters.Count != arguments.Count)
      {
        Session.Diagnostics.ReportInvalidArgumentCount(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), functionSymbol.Name, functionSymbol.Parameters.Count, arguments.Count);
        return new BoundUserFunctionCallExpression(functionSymbol, arguments);
      }
  
      for (var index = 0; index < arguments.Count; index++)
      {
        if (Session.OverloadResolver.TryGetCallConversionDistance(functionSymbol.Parameters[index].Type, arguments[index].Type, isExternalCall: false, out _))
        {
          continue;
        }
  
        Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Arguments[index]), functionSymbol.Parameters[index].Type.Name, arguments[index].Type.Name);
      }
  
      return new BoundUserFunctionCallExpression(functionSymbol, arguments);
    }
  
    internal BoundExpression BindImplicitFunctionGroupCall(TextSpan span, FunctionGroupSymbol functionGroup)
    {
      var arguments = Array.Empty<BoundExpression>();
      if (functionGroup.Functions.Count == 1)
      {
        var function = functionGroup.Functions[0];
        if (function.Parameters.Count == 0)
          return new BoundUserFunctionCallExpression(function, arguments);
        Session.Diagnostics.ReportCallableRequiresArguments(span, function.Name, function.Parameters.Count);
        return BoundErrorExpression.Instance;
      }
  
      var candidates = new List<FunctionSymbol>();
      foreach (var function in functionGroup.Functions)
      {
        if (function.Parameters.Count == 0)
          candidates.Add(function);
      }
  
      if (candidates.Count == 1)
        return new BoundUserFunctionCallExpression(candidates[0], arguments);
      if (candidates.Count == 0)
      {
        Session.Diagnostics.ReportNoMatchingFunctionOverload(span, functionGroup.Name, string.Empty, Session.OverloadResolver.BuildFunctionCandidateList(functionGroup.Functions));
        return BoundErrorExpression.Instance;
      }
  
      Session.Diagnostics.ReportAmbiguousFunctionOverload(span, functionGroup.Name, string.Empty, Session.OverloadResolver.BuildFunctionCandidateList(candidates));
      return BoundErrorExpression.Instance;
    }
  
    internal BoundExpression BindTupleExpression(TupleExpressionSyntax syntax, TypeSymbol expectedType)
    {
      var expectedElements = expectedType?.TypeKind == TypeKind.Tuple ? expectedType.TupleElementTypes : null;
      var elements = new List<BoundExpression>();
      var elementTypes = new TypeSymbol[syntax.Elements.Count];
      for (var index = 0; index < syntax.Elements.Count; index++)
      {
        var expectedElement = expectedElements != null && index < expectedElements.Count ? expectedElements[index] : null;
        var element = Session.ExpressionBinder.BindExpression(syntax.Elements[index], expectedElement);
        elements.Add(element);
        elementTypes[index] = element.Type;
      }
  
      if (Session.TypeResolver.ContainsTypeError(elementTypes))
        return BoundErrorExpression.Instance;
      return new BoundTupleExpression(TypeSymbol.Tuple(elementTypes), elements);
    }
  
    internal BoundExpression BindFunctionGroupCall(CallExpressionSyntax syntax, FunctionGroupSymbol functionGroup, IReadOnlyList<BoundExpression> arguments)
    {
      if (functionGroup.Functions.Count == 0)
        return BoundErrorExpression.Instance;
      if (functionGroup.Functions.Count == 1)
        return Session.CallExpressionBinder.BindUserFunctionCall(syntax, functionGroup.Functions[0], arguments);
      if (Session.NameResolver.ContainsError(arguments))
        return BoundErrorExpression.Instance;
      var sameArity = new List<FunctionSymbol>();
      foreach (var function in functionGroup.Functions)
      {
        if (function.Parameters.Count == arguments.Count)
          sameArity.Add(function);
      }
  
      if (sameArity.Count == 0)
      {
        Session.Diagnostics.ReportNoMatchingFunctionOverload(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), functionGroup.Name, Session.OverloadResolver.BuildFunctionArgumentTypeList(arguments), Session.OverloadResolver.BuildFunctionCandidateList(functionGroup.Functions));
        return BoundErrorExpression.Instance;
      }
  
      var applicable = new List<FunctionSymbol>();
      foreach (var function in sameArity)
      {
        if (Session.OverloadResolver.IsApplicable(function, arguments))
          applicable.Add(function);
      }
  
      if (applicable.Count == 0)
      {
        Session.Diagnostics.ReportNoMatchingFunctionOverload(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), functionGroup.Name, Session.OverloadResolver.BuildFunctionArgumentTypeList(arguments), Session.OverloadResolver.BuildFunctionCandidateList(functionGroup.Functions));
        return BoundErrorExpression.Instance;
      }
  
      var selected = Session.OverloadResolver.SelectBestOverload(applicable, arguments, out var overloadResolutionWasAmbiguous);
      if (overloadResolutionWasAmbiguous || selected == null)
      {
        Session.Diagnostics.ReportAmbiguousFunctionOverload(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), functionGroup.Name, Session.OverloadResolver.BuildFunctionArgumentTypeList(arguments), Session.OverloadResolver.BuildFunctionCandidateList(applicable));
        return BoundErrorExpression.Instance;
      }
  
      return new BoundUserFunctionCallExpression(selected, arguments);
    }
  
    internal BoundExpression BindMethodCall(CallExpressionSyntax syntax, BoundExpression target, MethodGroupSymbol methodGroup, IReadOnlyList<BoundExpression> arguments)
    {
      if (Session.NameResolver.ContainsError(arguments))
      {
        return new BoundCallExpression(target, arguments, null, TypeSymbol.Error);
      }
  
      var visibleMethods = new List<MethodSymbol>();
      var hasInaccessibleUserMethod = false;
      foreach (var method in methodGroup.Methods)
      {
        if (method is UserMethodSymbol candidateUserMethod && !Session.VisibilityResolver.IsUserMethodVisible(candidateUserMethod))
        {
          hasInaccessibleUserMethod = true;
          continue;
        }
  
        visibleMethods.Add(method);
      }
  
      if (visibleMethods.Count == 0)
      {
        if (hasInaccessibleUserMethod)
        {
          Session.Diagnostics.ReportDeclarationNotPublic(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.Name);
        }
        else if (methodGroup.RejectedCandidates.Count > 0)
        {
          Session.Diagnostics.ReportExternCandidatesNotUdonCallable(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.DisplayName, Session.OverloadResolver.BuildRejectedCandidateDetail(methodGroup.RejectedCandidates));
        }
        else
        {
          Session.Diagnostics.ReportNoCallableExternCandidate(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.DisplayName);
        }
  
        return new BoundCallExpression(target, arguments, null, TypeSymbol.Error);
      }
  
      var sameArityMethods = new List<MethodSymbol>();
      var targetMemberAccess = target as BoundMemberAccessExpression;
      var targetReceiver = targetMemberAccess?.Receiver;
      var targetIsType = Session.NameResolver.GetReferencedSymbol(targetReceiver) is TypeSymbol;
      foreach (var method in visibleMethods)
      {
        if (method.Parameters.Count == arguments.Count && (targetMemberAccess == null || method.IsStatic == targetIsType))
        {
          sameArityMethods.Add(method);
        }
      }
  
      if (sameArityMethods.Count == 0)
      {
        var expectedCount = Session.OverloadResolver.GetSharedParameterCount(visibleMethods);
        if (expectedCount >= 0)
        {
          Session.Diagnostics.ReportInvalidArgumentCount(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.DisplayName, expectedCount, arguments.Count);
        }
        else
        {
          Session.Diagnostics.ReportNoMatchingOverload(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.DisplayName, Session.OverloadResolver.BuildArgumentTypeList(arguments));
        }
  
        return new BoundCallExpression(target, arguments, null, TypeSymbol.Error);
      }
  
      var applicableMethods = new List<MethodSymbol>();
      foreach (var method in sameArityMethods)
      {
        if (Session.OverloadResolver.IsApplicable(method, arguments))
          applicableMethods.Add(method);
      }
  
      if (applicableMethods.Count == 0)
      {
        var hasUserMethod = false;
        foreach (var method in visibleMethods)
          hasUserMethod |= method is UserMethodSymbol;
        if (hasUserMethod)
        {
          Session.Diagnostics.ReportNoApplicableMethodOverload(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.DisplayName, Session.OverloadResolver.BuildArgumentTypeList(arguments));
        }
        else
        {
          Session.Diagnostics.ReportNoMatchingOverload(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.DisplayName, Session.OverloadResolver.BuildArgumentTypeList(arguments));
        }
  
        return new BoundCallExpression(target, arguments, null, TypeSymbol.Error);
      }
  
      var selectedMethod = Session.OverloadResolver.SelectBestOverload(applicableMethods, arguments, out var overloadResolutionWasAmbiguous);
      if (overloadResolutionWasAmbiguous || selectedMethod == null)
      {
        var hasUserMethod = false;
        foreach (var method in applicableMethods)
          hasUserMethod |= method is UserMethodSymbol;
        if (hasUserMethod)
        {
          Session.Diagnostics.ReportAmbiguousMethodOverload(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.DisplayName, Session.OverloadResolver.BuildMethodCandidateList(applicableMethods));
        }
        else
        {
          Session.Diagnostics.ReportAmbiguousExternOverload(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), methodGroup.DisplayName, Session.OverloadResolver.BuildMethodCandidateList(applicableMethods));
        }
  
        return new BoundCallExpression(target, arguments, null, TypeSymbol.Error);
      }
  
      if (selectedMethod is UserMethodSymbol userMethod)
      {
        return new BoundUserFunctionCallExpression(userMethod.Function, arguments, userMethod.IsStatic ? null : targetReceiver);
      }
  
      return new BoundCallExpression(target, arguments, selectedMethod, selectedMethod.ReturnType);
    }
  }
}
