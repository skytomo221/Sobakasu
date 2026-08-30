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
  internal sealed class AggregateDeclarationBinder : BinderComponent
  {
    internal AggregateDeclarationBinder(BindingSession session) : base(session)
    {
    }
  
    internal void CollectAggregateType(StructDeclarationSyntax syntax)
    {
      Session.AggregateDeclarationBinder.CollectAggregateType(syntax, syntax.Identifier, syntax.GenericParameters, syntax.PubKeyword != null, UserAggregateKind.Struct);
    }
  
    internal void CollectAggregateType(EnumDeclarationSyntax syntax)
    {
      Session.AggregateDeclarationBinder.CollectAggregateType(syntax, syntax.Identifier, syntax.GenericParameters, syntax.PubKeyword != null, UserAggregateKind.Enum);
    }
  
    internal void CollectAggregateType(MemberSyntax syntax, SyntaxToken identifier, GenericParameterListSyntax genericParameters, bool isPublic, UserAggregateKind kind)
    {
      var name = identifier.Text ?? string.Empty;
      if (TypeResolver.BuiltInTypes.ContainsKey(name) || Session.Modules.VisibleTypes.ContainsKey(name))
      {
        Session.Diagnostics.ReportDuplicateAggregateType(identifier.Span, name);
        return;
      }
  
      var type = TypeSymbol.CreateAggregate(name, string.IsNullOrEmpty(Session.Modules.CurrentModule?.LogicalName) ? name : $"{Session.Modules.CurrentModule.LogicalName}.{name}", kind, isPublic, Session.Modules.CurrentModule?.LogicalName);
      var parameters = new List<TypeSymbol>();
      var parameterNames = new HashSet<string>(StringComparer.Ordinal);
      if (genericParameters != null)
      {
        for (var index = 0; index < genericParameters.Parameters.Count; index++)
        {
          var parameterSyntax = genericParameters.Parameters[index];
          var parameterName = parameterSyntax.Text ?? string.Empty;
          if (!parameterNames.Add(parameterName))
          {
            Session.Diagnostics.ReportDuplicateGenericParameter(parameterSyntax.Span, name, parameterName);
          }
  
          parameters.Add(TypeSymbol.CreateGenericParameter(parameterName, type, index, type.QualifiedName));
        }
      }
  
      type.SetGenericParameters(parameters);
      Session.Modules.VisibleTypes.Add(name, type);
      Session.Declarations.AggregateTypesBySyntax.Add(syntax, type);
      Session.CallableDeclarationBinder.RegisterModuleDeclaration(name, type, isPublic);
    }
  
    internal void BindStructDeclaration(StructDeclarationSyntax syntax)
    {
      if (!Session.Declarations.AggregateTypesBySyntax.TryGetValue(syntax, out var type))
        return;
      var previousGenericParameters = Session.Generics.CurrentTypeParameters;
      Session.Generics.CurrentTypeParameters = Session.AggregateDeclarationBinder.CreateGenericParameterScope(type.GenericParameters);
      try
      {
        var fields = new List<AggregateFieldSymbol>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fieldSyntax in syntax.Fields)
        {
          var name = fieldSyntax.Identifier.Text ?? string.Empty;
          if (!names.Add(name))
          {
            Session.Diagnostics.ReportDuplicateAggregateField(fieldSyntax.Identifier.Span, type.Name, name);
            continue;
          }
  
          fields.Add(new AggregateFieldSymbol(name, type, Session.TypeResolver.BindTypeSyntax(fieldSyntax.Type), fields.Count, fieldSyntax.Identifier.Span));
        }
  
        type.SetAggregateFields(fields);
      }
      finally
      {
        Session.Generics.CurrentTypeParameters = previousGenericParameters;
      }
    }
  
    internal void BindEnumDeclaration(EnumDeclarationSyntax syntax)
    {
      if (!Session.Declarations.AggregateTypesBySyntax.TryGetValue(syntax, out var type))
        return;
      var previousGenericParameters = Session.Generics.CurrentTypeParameters;
      Session.Generics.CurrentTypeParameters = Session.AggregateDeclarationBinder.CreateGenericParameterScope(type.GenericParameters);
      try
      {
        var variants = new List<EnumVariantSymbol>();
        var variantNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variantSyntax in syntax.Variants)
        {
          var variantName = variantSyntax.Identifier.Text ?? string.Empty;
          if (!variantNames.Add(variantName))
          {
            Session.Diagnostics.ReportDuplicateEnumVariant(variantSyntax.Identifier.Span, type.Name, variantName);
            continue;
          }
  
          var fields = new List<AggregateFieldSymbol>();
          var fieldNames = new HashSet<string>(StringComparer.Ordinal);
          if (variantSyntax.VariantKind == EnumVariantSyntaxKind.Tuple)
          {
            for (var index = 0; index < variantSyntax.TuplePayloadTypes.Count; index++)
            {
              fields.Add(new AggregateFieldSymbol(index.ToString(), type, Session.TypeResolver.BindTypeSyntax(variantSyntax.TuplePayloadTypes[index]), index, variantSyntax.TuplePayloadTypes[index].GetSpan()));
            }
          }
          else if (variantSyntax.VariantKind == EnumVariantSyntaxKind.Struct)
          {
            foreach (var fieldSyntax in variantSyntax.NamedPayloadFields)
            {
              var fieldName = fieldSyntax.Identifier.Text ?? string.Empty;
              if (!fieldNames.Add(fieldName))
              {
                Session.Diagnostics.ReportDuplicateEnumPayloadField(fieldSyntax.Identifier.Span, type.Name, variantName, fieldName);
                continue;
              }
  
              fields.Add(new AggregateFieldSymbol(fieldName, type, Session.TypeResolver.BindTypeSyntax(fieldSyntax.Type), fields.Count, fieldSyntax.Identifier.Span));
            }
          }
  
          var variantKind = variantSyntax.VariantKind switch
          {
            EnumVariantSyntaxKind.Tuple => EnumVariantKind.Tuple,
            EnumVariantSyntaxKind.Struct => EnumVariantKind.Struct,
            _ => EnumVariantKind.Unit
          };
          variants.Add(new EnumVariantSymbol(variantName, type, variantKind, variants.Count, fields, variantSyntax.Identifier.Span));
        }
  
        type.SetEnumVariants(variants);
        if (!string.IsNullOrEmpty(type.CanonicalPublicPath))
        {
          foreach (var variant in variants)
          {
            variant.RegisterPublicPath($"{type.CanonicalPublicPath}.{variant.Name}");
          }
        }
      }
      finally
      {
        Session.Generics.CurrentTypeParameters = previousGenericParameters;
      }
    }
  
    internal Dictionary<string, TypeSymbol> CreateGenericParameterScope(IReadOnlyList<TypeSymbol> parameters)
    {
      var result = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal);
      foreach (var parameter in parameters)
      {
        if (!result.ContainsKey(parameter.Name))
          result.Add(parameter.Name, parameter);
      }
  
      return result;
    }
  }
}
