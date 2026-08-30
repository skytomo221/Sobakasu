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
  internal sealed class GenericInference : BinderComponent
  {
    internal GenericInference(BindingSession session) : base(session)
    {
    }
  
    internal void SeedInferenceFromExpectedType(TypeSymbol definition, TypeSymbol expectedType, IDictionary<TypeSymbol, TypeSymbol> substitutions)
    {
      if (expectedType?.GenericDefinition != definition)
        return;
      for (var index = 0; index < definition.GenericParameters.Count; index++)
        substitutions[definition.GenericParameters[index]] = expectedType.TypeArguments[index];
    }
  
    internal void InferTypeArguments(TypeSymbol template, TypeSymbol actual, IDictionary<TypeSymbol, TypeSymbol> substitutions, TextSpan span)
    {
      if (template == null || actual == null || actual == TypeSymbol.Error || actual.ContainsGenericParameters)
      {
        return;
      }
  
      if (template.IsGenericParameter)
      {
        if (!substitutions.TryGetValue(template, out var existing))
        {
          substitutions[template] = actual;
        }
        else if (existing != actual)
        {
          Session.Diagnostics.ReportConflictingGenericInference(span, template.Name, existing.Name, actual.Name);
        }
  
        return;
      }
  
      if (template.TypeKind == TypeKind.Array && actual.TypeKind == TypeKind.Array)
      {
        Session.GenericInference.InferTypeArguments(template.ElementType, actual.ElementType, substitutions, span);
        return;
      }
  
      if (!template.IsConstructedGenericType || !actual.IsConstructedGenericType || template.GenericDefinition != actual.GenericDefinition)
      {
        return;
      }
  
      for (var index = 0; index < template.TypeArguments.Count; index++)
      {
        Session.GenericInference.InferTypeArguments(template.TypeArguments[index], actual.TypeArguments[index], substitutions, span);
      }
    }
  
    internal bool CompleteTypeArgumentInference(TypeSymbol definition, IReadOnlyDictionary<TypeSymbol, TypeSymbol> substitutions, TextSpan span, out TypeSymbol constructed)
    {
      var arguments = new TypeSymbol[definition.GenericParameters.Count];
      var success = true;
      for (var index = 0; index < arguments.Length; index++)
      {
        var parameter = definition.GenericParameters[index];
        if (!substitutions.TryGetValue(parameter, out var argument) || argument == null || argument == TypeSymbol.Error || argument.ContainsGenericParameters)
        {
          Session.Diagnostics.ReportCannotInferGenericParameter(span, parameter.Name, $"{definition.Name}<{string.Join(", ", Session.GenericInference.GetGenericParameterNames(definition))}>");
          success = false;
          continue;
        }
  
        arguments[index] = argument;
      }
  
      constructed = success ? definition.Construct(arguments) : null;
      return success;
    }
  
    internal IEnumerable<string> GetGenericParameterNames(TypeSymbol definition)
    {
      foreach (var parameter in definition.GenericParameters)
        yield return parameter.Name;
    }
  }
}
