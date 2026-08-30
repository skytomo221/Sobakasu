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
  internal sealed class ExternDeclarationBinder : BinderComponent
  {
    internal ExternDeclarationBinder(BindingSession session) : base(session)
    {
    }
  
    internal void CollectExternalTypeBinding(ImplDeclarationSyntax syntax)
    {
      var typeName = syntax.TargetType.GetText();
      var span = syntax.TargetType.GetSpan();
      if (syntax.GenericParameters != null || syntax.TargetType.TypeArgumentList != null)
      {
        Session.Diagnostics.ReportInvalidGenericImplTarget(span, typeName);
        return;
      }
  
      if (syntax.TargetType.Parts.Count != 1 || syntax.TargetType.Parts[0].Kind != SyntaxKind.Identifier)
      {
        Session.Diagnostics.ReportInvalidExternalBindingTarget(span, typeName);
        return;
      }
  
      if (TypeResolver.BuiltInTypes.ContainsKey(typeName))
      {
        Session.Diagnostics.ReportCannotExternallyBindBuiltInType(span, typeName);
        return;
      }
  
      if (Session.Modules.VisibleTypes.ContainsKey(typeName))
      {
        Session.Diagnostics.ReportDuplicateExternalTypeBinding(span, typeName);
        return;
      }
  
      var runtimeTypeName = syntax.ExternalTypeName?.GetText() ?? string.Empty;
      if (!Session.Environment.ExternCatalog.TryGetTypeSymbol(runtimeTypeName, out var runtimeType))
      {
        Session.Diagnostics.ReportUnknownExternalType(span, runtimeTypeName);
        return;
      }
  
      if (runtimeType.IsBuiltIn)
      {
        Session.Diagnostics.ReportCannotExternallyBindBuiltInType(span, runtimeTypeName);
        return;
      }
  
      if (!Session.Environment.ExternCatalog.IsTypeExposed(runtimeType))
      {
        Session.Diagnostics.ReportExternalTypeNotExposed(span, runtimeTypeName);
        return;
      }
  
      if (Session.Declarations.ExternalBindingsByRuntimeType.TryGetValue(runtimeType.RuntimeQualifiedName, out var existingBinding))
      {
        Session.Diagnostics.ReportExternalRuntimeTypeAlreadyBound(span, runtimeTypeName, existingBinding.Name);
        return;
      }
  
      var type = TypeSymbol.CreateExternalBinding(typeName, string.IsNullOrEmpty(Session.Modules.CurrentModule?.LogicalName) ? typeName : $"{Session.Modules.CurrentModule.LogicalName}.{typeName}", runtimeType, syntax.PubKeyword != null, Session.Modules.CurrentModule?.LogicalName);
      Session.Modules.VisibleTypes.Add(typeName, type);
      Session.Declarations.ExternalBindingsByRuntimeType.Add(type.RuntimeQualifiedName, type);
      Session.Declarations.ExternalTypesBySyntax.Add(syntax, type);
      Session.CallableDeclarationBinder.RegisterModuleDeclaration(typeName, type, type.IsPublic);
    }
  
    internal void BindExternalFunctionSignature(FunctionDeclarationSyntax syntax, FunctionSymbol function)
    {
      if (syntax.ExternalBinding?.IsMalformed != false || syntax.ExternalBinding.ExternExpression == null && syntax.ExternalBinding.AbiSignature == null)
      {
        if (function.ReturnType == TypeSymbol.Error && syntax.ReturnTypeAnnotation == null)
        {
          function.SetInferredReturnType(TypeSymbol.Error);
        }
  
        return;
      }
  
      var previousBody = Session.Body;
      Session.Body = new BodyBindingContext
      {
        Scope = new BoundScope(previousBody.Scope),
        CurrentType = function.ContainingType,
        CurrentFunction = function,
        CurrentReturnType = function.ReturnType,
        CurrentEventName = function.Name,
        NextDestructuringTemporaryId = previousBody.NextDestructuringTemporaryId
      };
      foreach (var parameter in function.Parameters)
        Session.Body.Scope.DeclareParameter(parameter);
      if (function.SelfParameter != null)
        Session.Body.Scope.DeclareParameter(function.SelfParameter);
      BoundExpression bindingExpression;
      ExternMethodSymbol externalMethod = null;
      try
      {
        var rawExpression = syntax.ExternalBinding.AbiSignature != null ? Session.ExternDeclarationBinder.BindExternAbiSignature(syntax.ExternalBinding.AbiSignature, function) : Session.ExternResolver.BindExternExpression(syntax.ExternalBinding.ExternExpression);
        if (rawExpression is not BoundCallExpression rawCall || rawCall.Method is not ExternMethodSymbol resolvedMethod)
        {
          if (rawExpression.Type != TypeSymbol.Error)
          {
            Session.Diagnostics.ReportExternalFunctionBindingRequiresMember(syntax.ExternalBinding.AbiSignature != null ? Session.BinderSyntaxFacts.GetExternalAbiSignatureSpan(syntax.ExternalBinding.AbiSignature) : Session.BinderSyntaxFacts.GetExpressionSpan(syntax.ExternalBinding.ExternExpression));
          }
  
          bindingExpression = BoundErrorExpression.Instance;
        }
        else
        {
          externalMethod = resolvedMethod;
          bindingExpression = syntax.ExternalBinding.IsMaybe ? Session.ExternDeclarationBinder.BindMaybeExternFunctionBinding(rawCall, syntax.ExternalBinding.AbiSignature != null ? Session.BinderSyntaxFacts.GetExternalAbiSignatureSpan(syntax.ExternalBinding.AbiSignature) : Session.BinderSyntaxFacts.GetExpressionSpan(syntax.ExternalBinding.ExternExpression)) : rawCall;
        }
      }
      finally
      {
        previousBody.NextDestructuringTemporaryId = Session.Body.NextDestructuringTemporaryId;
        Session.Body = previousBody;
      }
  
      Session.Callables.ExternalBindingExpressions[function] = bindingExpression;
      if (syntax.ReturnTypeAnnotation == null)
      {
        function.SetInferredReturnType(bindingExpression.Type);
      }
      else if (bindingExpression.Type != TypeSymbol.Error && !Session.ConversionClassifier.CanAssignToLocal(function.ReturnType, bindingExpression.Type))
      {
        if (syntax.ExternalBinding.IsMaybe)
        {
          Session.Diagnostics.ReportMaybeExternalBindingReturnTypeMismatch(syntax.ReturnTypeAnnotation.Type.GetSpan(), function.ReturnType.Name, bindingExpression.Type.Name);
        }
        else
        {
          Session.Diagnostics.ReportExternalBindingReturnTypeMismatch(syntax.ReturnTypeAnnotation.Type.GetSpan(), function.ReturnType.Name, bindingExpression.Type.Name);
        }
      }
  
      if (externalMethod != null)
      {
        function.SetExternalBinding(new ExternalFunctionBinding(function, externalMethod, syntax.ExternalBinding.IsMaybe ? ExternalReturnBindingMode.Maybe : ExternalReturnBindingMode.Raw));
      }
    }
  
    internal BoundExpression BindExternAbiSignature(ExternalAbiSignatureSyntax syntax, FunctionSymbol function)
    {
      var span = Session.BinderSyntaxFacts.GetExternalAbiSignatureSpan(syntax);
      TypeSymbol containingType;
      BoundExpression receiver;
      bool isStatic;
      string memberName;
      ExternMemberKind memberKind;
      if (syntax.IsConstructor)
      {
        containingType = Session.TypeResolver.BindTypeSyntax(syntax.ConstructorType);
        if (containingType == TypeSymbol.Error)
          return BoundErrorExpression.Instance;
        if (Session.ExpressionBinder.IsAggregateStorageType(containingType))
        {
          Session.Diagnostics.ReportAggregateExternBoundary(syntax.ConstructorType.GetSpan(), containingType.Name);
          return BoundErrorExpression.Instance;
        }
  
        receiver = null;
        isStatic = true;
        memberName = "new";
        memberKind = ExternMemberKind.Constructor;
      }
      else
      {
        if (syntax.Target is not MemberAccessExpressionSyntax member || !Session.ExternResolver.TryBindExternalReceiver(member.Expression, out containingType, out receiver, out isStatic))
        {
          Session.Diagnostics.ReportUnsupportedExternalExpression(span);
          return BoundErrorExpression.Instance;
        }
  
        memberName = member.MemberName;
        memberKind = ExternMemberKind.Method;
      }
  
      var declaredTypes = new TypeSymbol[syntax.Parameters.Count];
      var declaredModes = new ExternParameterPassingMode[syntax.Parameters.Count];
      for (var index = 0; index < syntax.Parameters.Count; index++)
      {
        declaredTypes[index] = Session.TypeResolver.BindTypeSyntax(syntax.Parameters[index].Type);
        declaredModes[index] = syntax.Parameters[index].Modifier?.Kind switch
        {
          SyntaxKind.RefKeyword => ExternParameterPassingMode.Ref,
          SyntaxKind.OutKeyword => ExternParameterPassingMode.Out,
          _ => ExternParameterPassingMode.Normal
        };
      }
  
      var group = Session.Environment.ExternCatalog.GetExternalMethodGroup(containingType, memberName);
      var matches = new List<ExternMethodSymbol>();
      if (group != null)
      {
        foreach (var candidate in group.Methods)
        {
          if (candidate is not ExternMethodSymbol external || external.MemberKind != memberKind || external.IsStatic != isStatic || external.AbiParameters == null || external.AbiParameters.Count != declaredTypes.Length)
          {
            continue;
          }
  
          var matchesSignature = true;
          for (var index = 0; index < declaredTypes.Length; index++)
          {
            var actualMode = external.AbiParameters[index].PassingMode;
            if (actualMode == ExternParameterPassingMode.In)
              actualMode = ExternParameterPassingMode.Normal;
            if (actualMode != declaredModes[index] || !string.Equals(external.AbiParameters[index].Type.RuntimeQualifiedName, declaredTypes[index].RuntimeQualifiedName, StringComparison.Ordinal))
            {
              matchesSignature = false;
              break;
            }
          }
  
          if (matchesSignature)
            matches.Add(external);
        }
      }
  
      var arguments = new List<BoundExpression>();
      if (!isStatic && receiver != null)
        arguments.Add(receiver);
      foreach (var parameter in function.Parameters)
      {
        arguments.Add(new BoundNameExpression(parameter.Name, parameter, parameter.Type));
      }
  
      ExternMethodSymbol selected = null;
      foreach (var candidate in matches)
      {
        if (candidate.Parameters.Count == arguments.Count && Session.OverloadResolver.IsApplicable(candidate, arguments))
        {
          if (selected != null)
          {
            Session.Diagnostics.ReportAmbiguousExternalOverload(span, group.DisplayName, Session.OverloadResolver.BuildMethodCandidateList(matches));
            return BoundErrorExpression.Instance;
          }
  
          selected = candidate;
        }
      }
  
      if (selected == null)
      {
        Session.Diagnostics.ReportNoApplicableExternalOverload(span, group?.DisplayName ?? $"{containingType.Name}.{memberName}", Session.OverloadResolver.BuildArgumentTypeList(arguments));
        return BoundErrorExpression.Instance;
      }
  
      selected = Session.ExternDeclarationBinder.ApplyExternOutputProjections(selected, syntax.Parameters, declaredTypes);
      if (selected == null)
        return BoundErrorExpression.Instance;
      return new BoundCallExpression(new BoundNameExpression(selected.DisplayName, group, TypeSymbol.MethodGroupPseudoType), arguments, selected, Session.ExternResolver.MapExternalResultType(selected.ReturnType));
    }
  
    internal BoundExpression BindMaybeExternFunctionBinding(BoundCallExpression rawCall, TextSpan span)
    {
      if (!Session.ExternDeclarationBinder.TryBindMaybeOutputProjection(rawCall.Type, span, isOutParameter: false, out var projection))
      {
        return BoundErrorExpression.Instance;
      }
  
      return new BoundMaybeExternBindingExpression(rawCall, projection);
    }
  
    internal ExternMethodSymbol ApplyExternOutputProjections(ExternMethodSymbol selected, IReadOnlyList<ExternalAbiParameterSyntax> syntaxParameters, IReadOnlyList<TypeSymbol> declaredTypes)
    {
      var hasProjection = false;
      var parameters = new ExternParameterSymbol[selected.AbiParameters.Count];
      for (var index = 0; index < parameters.Length; index++)
      {
        var parameter = selected.AbiParameters[index];
        if (!syntaxParameters[index].IsMaybe)
        {
          parameters[index] = parameter;
          continue;
        }
  
        if (parameter.PassingMode != ExternParameterPassingMode.Out)
        {
          Session.Diagnostics.ReportMaybeOutExternalBindingUnsupported(syntaxParameters[index].MaybeKeyword.Span, declaredTypes[index].Name, "The 'maybe' projection is only supported on physical out parameters.");
          return null;
        }
  
        if (!Session.ExternDeclarationBinder.TryBindMaybeOutputProjection(declaredTypes[index], syntaxParameters[index].MaybeKeyword.Span, isOutParameter: true, out var projection))
        {
          return null;
        }
  
        parameters[index] = new ExternParameterSymbol(parameter.Name, parameter.Type, parameter.PassingMode, parameter.LogicalInputOrdinal, projection);
        hasProjection = true;
      }
  
      if (!hasProjection)
        return selected;
      return new ExternMethodSymbol(selected.Name, selected.ContainingType, selected.Parameters, ReflectionExternCatalogBuilder.BuildLogicalReturnType(selected.AbiReturnType, parameters), selected.MethodBase, selected.ExternSignature, selected.IsStatic, selected.MemberKind, parameters, selected.AbiReturnType);
    }
  
    internal bool TryBindMaybeOutputProjection(TypeSymbol valueType, TextSpan span, bool isOutParameter, out ExternMaybeOutputProjection projection)
    {
      projection = null;
      if (valueType == TypeSymbol.Unit || valueType == TypeSymbol.Error || !valueType.IsReferenceType)
      {
        Session.ExternDeclarationBinder.ReportMaybeProjectionUnsupported(span, valueType.Name, isOutParameter, "Only reference-like external values can be checked with the configured validity policy.");
        return false;
      }
  
      if (!Session.LanguageItems.TryGetType(LanguageItemNames.Maybe, out var maybeDefinition) ||
          !maybeDefinition.IsGenericDefinition ||
          maybeDefinition.GenericParameters.Count != 1 ||
          maybeDefinition.AggregateKind != UserAggregateKind.Enum)
      {
        Session.ExternDeclarationBinder.ReportMaybeProjectionUnsupported(span, valueType.Name, isOutParameter, "The 'maybe' language item must identify a generic enum with one type parameter.");
        return false;
      }
  
      var maybeType = maybeDefinition.Construct(new[] { valueType });
      EnumVariantSymbol justVariant = null;
      EnumVariantSymbol nothingVariant = null;
      foreach (var variant in maybeType.EnumVariants)
      {
        if (variant.VariantKind == EnumVariantKind.Unit && variant.Fields.Count == 0)
        {
          nothingVariant ??= variant;
        }
        else if (variant.VariantKind == EnumVariantKind.Tuple && variant.Fields.Count == 1 && variant.Fields[0].Type == valueType)
        {
          justVariant ??= variant;
        }
      }
  
      if (justVariant == null || nothingVariant == null)
      {
        Session.ExternDeclarationBinder.ReportMaybeProjectionUnsupported(span, valueType.Name, isOutParameter, "Maybe<T> must provide one unit variant and one single-value tuple variant.");
        return false;
      }
  
      if (!Session.Environment.ExternCatalog.TryGetTypeSymbol("VRC.SDKBase.Utilities", out var utilitiesType))
      {
        Session.ExternDeclarationBinder.ReportMaybeProjectionUnsupported(span, valueType.Name, isOutParameter, "VRC.SDKBase.Utilities is not available in the extern catalog.");
        return false;
      }
  
      var validityGroup = Session.Environment.ExternCatalog.GetExternalMethodGroup(utilitiesType, "IsValid");
      var validityExpression = Session.ExternResolver.BindExternalMethodGroup(validityGroup, utilitiesType, "IsValid", new BoundExpression[] { new BoundLiteralExpression(null, valueType, span) }, true, ExternMemberKind.Method, span);
      if (validityExpression is not BoundCallExpression validityCall || validityCall.Method is not ExternMethodSymbol validityMethod || validityCall.Type != TypeSymbol.Bool)
      {
        if (validityExpression.Type != TypeSymbol.Error)
        {
          Session.ExternDeclarationBinder.ReportMaybeProjectionUnsupported(span, valueType.Name, isOutParameter, "The configured Utilities.IsValid overload must return bool.");
        }
  
        return false;
      }
  
      projection = new ExternMaybeOutputProjection(validityMethod, justVariant, nothingVariant);
      return true;
    }
  
    internal void ReportMaybeProjectionUnsupported(TextSpan span, string type, bool isOutParameter, string reason)
    {
      if (isOutParameter)
      {
        Session.Diagnostics.ReportMaybeOutExternalBindingUnsupported(span, type, reason);
      }
      else
      {
        Session.Diagnostics.ReportMaybeExternalBindingUnsupported(span, type, reason);
      }
    }
  }
}
