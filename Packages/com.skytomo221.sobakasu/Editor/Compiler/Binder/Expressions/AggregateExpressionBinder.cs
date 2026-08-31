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
  internal sealed class AggregateExpressionBinder : BinderComponent
  {
    internal AggregateExpressionBinder(BindingSession session) : base(session)
    {
    }
  
    internal BoundExpression BindAggregateInitializerExpression(AggregateInitializerExpressionSyntax syntax, TypeSymbol expectedType)
    {
      if (syntax.Target is NameExpressionSyntax importedVariantTarget && Session.NameResolver.TryResolveImportedEnumVariant(importedVariantTarget, out var importedVariant))
      {
        return Session.AggregateExpressionBinder.BindStructEnumVariant(syntax, importedVariant, expectedType);
      }
  
      if (syntax.Target is MemberAccessExpressionSyntax variantTarget && Session.AggregateExpressionBinder.TryResolveEnumVariant(variantTarget, out var variant, out var enumTargetHandled))
      {
        if (variant == null)
          return BoundErrorExpression.Instance;
        return Session.AggregateExpressionBinder.BindStructEnumVariant(syntax, variant, expectedType);
      }
  
      TypeSymbol targetType = null;
      if (syntax.Target is NameExpressionSyntax typeName)
      {
        Session.TypeResolver.TryResolveTypeNameQuiet(typeName.Name, Session.BinderSyntaxFacts.GetExpressionSpan(typeName), out targetType);
      }
      else if (syntax.Target is MemberAccessExpressionSyntax qualifiedType && Session.TypeResolver.TryGetQualifiedName(qualifiedType, out var qualifiedName))
      {
        Session.TypeResolver.TryResolveTypeNameQuiet(qualifiedName, Session.BinderSyntaxFacts.GetExpressionSpan(qualifiedType), out targetType);
      }
  
      if (targetType == null)
      {
        var target = Session.ExpressionBinder.BindExpression(syntax.Target);
        if (target.Type == TypeSymbol.Error)
          return BoundErrorExpression.Instance;
        targetType = Session.NameResolver.GetReferencedSymbol(target) as TypeSymbol;
      }
  
      if (targetType?.IsGenericDefinition == true && targetType.AggregateKind == UserAggregateKind.Struct)
      {
        return Session.AggregateExpressionBinder.BindInferredStructInitializer(syntax, targetType, expectedType);
      }
  
      if (targetType?.AggregateKind != UserAggregateKind.Struct || targetType.IsExternalBinding)
      {
        Session.Diagnostics.ReportStructInitializerRequiresStruct(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), targetType?.Name ?? syntax.Target.GetType().Name);
        foreach (var field in syntax.Fields)
          Session.ExpressionBinder.BindExpression(field.Expression);
        return BoundErrorExpression.Instance;
      }
  
      return new BoundStructConstructionExpression(targetType, Session.AggregateExpressionBinder.BindNamedAggregateInitializers(syntax.Fields, targetType.AggregateFields, targetType.Name));
    }
  
    internal BoundExpression BindStructEnumVariant(AggregateInitializerExpressionSyntax syntax, EnumVariantSymbol variant, TypeSymbol expectedType)
    {
      if (variant.VariantKind != EnumVariantKind.Struct)
      {
        Session.Diagnostics.ReportEnumVariantConstructionForm(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), variant.ContainingType.Name, variant.Name, "struct");
        foreach (var field in syntax.Fields)
          Session.ExpressionBinder.BindExpression(field.Expression);
        return BoundErrorExpression.Instance;
      }
  
      if (variant.ContainingType.IsGenericDefinition)
        return Session.AggregateExpressionBinder.BindInferredStructEnumVariant(syntax, variant, expectedType);
      return new BoundEnumConstructionExpression(variant, Session.AggregateExpressionBinder.BindNamedAggregateInitializers(syntax.Fields, variant.Fields, $"{variant.ContainingType.Name}.{variant.Name}"));
    }
  
    internal BoundExpression BindInferredStructInitializer(AggregateInitializerExpressionSyntax syntax, TypeSymbol definition, TypeSymbol expectedType)
    {
      var substitutions = new Dictionary<TypeSymbol, TypeSymbol>();
      Session.GenericInference.SeedInferenceFromExpectedType(definition, expectedType, substitutions);
      var declaredByName = new Dictionary<string, AggregateFieldSymbol>(StringComparer.Ordinal);
      foreach (var field in definition.AggregateFields)
        declaredByName[field.Name] = field;
      var seen = new HashSet<string>(StringComparer.Ordinal);
      var inferredFields = new List<InferredFieldInitializer>();
      foreach (var syntaxField in syntax.Fields)
      {
        var name = syntaxField.Identifier.Text ?? string.Empty;
        if (!declaredByName.TryGetValue(name, out var field))
        {
          Session.Diagnostics.ReportUnknownAggregateInitializerField(syntaxField.Identifier.Span, definition.Name, name);
          Session.ExpressionBinder.BindExpression(syntaxField.Expression);
          continue;
        }
  
        if (!seen.Add(name))
        {
          Session.Diagnostics.ReportDuplicateAggregateInitializerField(syntaxField.Identifier.Span, definition.Name, name);
          Session.ExpressionBinder.BindExpression(syntaxField.Expression);
          continue;
        }
  
        var contextualType = Session.GenericSubstitution.Substitute(field.Type, substitutions);
        if (contextualType.ContainsGenericParameters)
          contextualType = null;
        var expression = Session.ExpressionBinder.BindExpression(syntaxField.Expression, contextualType);
        Session.GenericInference.InferTypeArguments(field.Type, expression.Type, substitutions, Session.BinderSyntaxFacts.GetExpressionSpan(syntaxField.Expression));
        inferredFields.Add(new InferredFieldInitializer(syntaxField, field, expression));
      }
  
      foreach (var field in definition.AggregateFields)
      {
        if (!seen.Contains(field.Name))
        {
          Session.Diagnostics.ReportMissingAggregateInitializerField(syntax.Fields.Count > 0 ? syntax.Fields[syntax.Fields.Count - 1].Identifier.Span : field.DeclarationSpan, definition.Name, field.Name);
        }
      }
  
      if (!Session.GenericInference.CompleteTypeArgumentInference(definition, substitutions, Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), out var constructed))
      {
        return BoundErrorExpression.Instance;
      }
  
      var initializers = new List<BoundAggregateFieldInitializer>();
      foreach (var inferred in inferredFields)
      {
        if (!constructed.TryGetAggregateField(inferred.TemplateField.Name, out var field))
          continue;
        if (!Session.ConversionClassifier.CanAssignToLocal(field.Type, inferred.Expression.Type))
        {
          Session.Diagnostics.ReportAggregateInitializerTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(inferred.Syntax.Expression), constructed.Name, field.Name, field.Type.Name, inferred.Expression.Type.Name);
        }
  
        initializers.Add(new BoundAggregateFieldInitializer(field, inferred.Expression));
      }
  
      return new BoundStructConstructionExpression(constructed, initializers);
    }
  
    internal BoundExpression BindInferredStructEnumVariant(AggregateInitializerExpressionSyntax syntax, EnumVariantSymbol templateVariant, TypeSymbol expectedType)
    {
      var definition = templateVariant.ContainingType;
      var substitutions = new Dictionary<TypeSymbol, TypeSymbol>();
      Session.GenericInference.SeedInferenceFromExpectedType(definition, expectedType, substitutions);
      var declaredByName = new Dictionary<string, AggregateFieldSymbol>(StringComparer.Ordinal);
      foreach (var field in templateVariant.Fields)
        declaredByName[field.Name] = field;
      var seen = new HashSet<string>(StringComparer.Ordinal);
      var inferredFields = new List<InferredFieldInitializer>();
      foreach (var syntaxField in syntax.Fields)
      {
        var name = syntaxField.Identifier.Text ?? string.Empty;
        if (!declaredByName.TryGetValue(name, out var field))
        {
          Session.Diagnostics.ReportUnknownAggregateInitializerField(syntaxField.Identifier.Span, $"{definition.Name}.{templateVariant.Name}", name);
          Session.ExpressionBinder.BindExpression(syntaxField.Expression);
          continue;
        }
  
        if (!seen.Add(name))
        {
          Session.Diagnostics.ReportDuplicateAggregateInitializerField(syntaxField.Identifier.Span, $"{definition.Name}.{templateVariant.Name}", name);
          Session.ExpressionBinder.BindExpression(syntaxField.Expression);
          continue;
        }
  
        var contextualType = Session.GenericSubstitution.Substitute(field.Type, substitutions);
        if (contextualType.ContainsGenericParameters)
          contextualType = null;
        var expression = Session.ExpressionBinder.BindExpression(syntaxField.Expression, contextualType);
        Session.GenericInference.InferTypeArguments(field.Type, expression.Type, substitutions, Session.BinderSyntaxFacts.GetExpressionSpan(syntaxField.Expression));
        inferredFields.Add(new InferredFieldInitializer(syntaxField, field, expression));
      }
  
      foreach (var field in templateVariant.Fields)
      {
        if (!seen.Contains(field.Name))
        {
          Session.Diagnostics.ReportMissingAggregateInitializerField(syntax.Fields.Count > 0 ? syntax.Fields[syntax.Fields.Count - 1].Identifier.Span : field.DeclarationSpan, $"{definition.Name}.{templateVariant.Name}", field.Name);
        }
      }
  
      if (!Session.GenericInference.CompleteTypeArgumentInference(definition, substitutions, Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target), out var constructed) || !constructed.TryGetEnumVariant(templateVariant.Name, out var variant))
      {
        return BoundErrorExpression.Instance;
      }
  
      var initializers = new List<BoundAggregateFieldInitializer>();
      foreach (var inferred in inferredFields)
      {
        if (!variant.TryGetField(inferred.TemplateField.Name, out var field))
          continue;
        if (!Session.ConversionClassifier.CanAssignToLocal(field.Type, inferred.Expression.Type))
        {
          Session.Diagnostics.ReportAggregateInitializerTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(inferred.Syntax.Expression), $"{constructed.Name}.{variant.Name}", field.Name, field.Type.Name, inferred.Expression.Type.Name);
        }
  
        initializers.Add(new BoundAggregateFieldInitializer(field, inferred.Expression));
      }
  
      return new BoundEnumConstructionExpression(variant, initializers);
    }
  
    internal IReadOnlyList<BoundAggregateFieldInitializer> BindNamedAggregateInitializers(IReadOnlyList<AggregateInitializerFieldSyntax> syntaxFields, IReadOnlyList<AggregateFieldSymbol> declaredFields, string targetName)
    {
      var declaredByName = new Dictionary<string, AggregateFieldSymbol>(StringComparer.Ordinal);
      foreach (var field in declaredFields)
        declaredByName[field.Name] = field;
      var seen = new HashSet<string>(StringComparer.Ordinal);
      var result = new List<BoundAggregateFieldInitializer>();
      foreach (var syntaxField in syntaxFields)
      {
        var name = syntaxField.Identifier.Text ?? string.Empty;
        if (!declaredByName.TryGetValue(name, out var field))
        {
          Session.Diagnostics.ReportUnknownAggregateInitializerField(syntaxField.Identifier.Span, targetName, name);
          Session.ExpressionBinder.BindExpression(syntaxField.Expression);
          continue;
        }
  
        if (!seen.Add(name))
        {
          Session.Diagnostics.ReportDuplicateAggregateInitializerField(syntaxField.Identifier.Span, targetName, name);
          Session.ExpressionBinder.BindExpression(syntaxField.Expression, field.Type);
          continue;
        }
  
        var expression = Session.ExpressionBinder.BindExpression(syntaxField.Expression, field.Type);
        if (!Session.ConversionClassifier.CanAssignToLocal(field.Type, expression.Type))
        {
          Session.Diagnostics.ReportAggregateInitializerTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntaxField.Expression), targetName, name, field.Type.Name, expression.Type.Name);
        }
  
        result.Add(new BoundAggregateFieldInitializer(field, expression));
      }
  
      foreach (var field in declaredFields)
      {
        if (!seen.Contains(field.Name))
        {
          Session.Diagnostics.ReportMissingAggregateInitializerField(syntaxFields.Count > 0 ? syntaxFields[syntaxFields.Count - 1].Identifier.Span : field.DeclarationSpan, targetName, field.Name);
        }
      }
  
      return result;
    }
  
    internal bool TryResolveEnumVariant(MemberAccessExpressionSyntax syntax, out EnumVariantSymbol variant, out bool handled)
    {
      variant = null;
      handled = false;
      var receiver = Session.ExpressionBinder.BindExpression(syntax.Expression);
      if (receiver.Type == TypeSymbol.Error)
        return false;
      var enumType = Session.NameResolver.GetReferencedSymbol(receiver) as TypeSymbol;
      if (enumType?.AggregateKind != UserAggregateKind.Enum)
        return false;
      handled = true;
      if (!enumType.TryGetEnumVariant(syntax.MemberName, out variant))
      {
        Session.Diagnostics.ReportUnknownEnumVariant(syntax.Name.Span, enumType.Name, syntax.MemberName);
      }
  
      return true;
    }
  }
}
