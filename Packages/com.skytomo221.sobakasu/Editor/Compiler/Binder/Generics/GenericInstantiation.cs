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
  internal sealed class GenericInstantiation : BinderComponent
  {
    internal GenericInstantiation(BindingSession session) : base(session)
    {
    }
  
    internal void EnsureConstructedGenericMethods(TypeSymbol concreteType)
    {
      if (concreteType?.IsConstructedGenericType != true || concreteType.ContainsGenericParameters || !Session.Generics.ImplTemplates.TryGetValue(concreteType.GenericDefinition, out var templates))
      {
        return;
      }
  
      foreach (var template in templates)
      {
        var substitutions = new Dictionary<TypeSymbol, TypeSymbol>();
        for (var index = 0; index < template.OpenTarget.TypeArguments.Count; index++)
        {
          substitutions[template.OpenTarget.TypeArguments[index]] = concreteType.TypeArguments[index];
        }
  
        foreach (var methodTemplate in template.Methods)
        {
          if (methodTemplate.Instances.ContainsKey(concreteType))
            continue;
          var parameters = new List<ParameterSymbol>();
          foreach (var parameter in methodTemplate.OpenFunction.Parameters)
          {
            parameters.Add(new ParameterSymbol(parameter.Name, Session.GenericSubstitution.Substitute(parameter.Type, substitutions), parameter.Ordinal, parameter.UdonStorageName, parameter.DeclarationSpan));
          }
  
          var returnType = Session.GenericSubstitution.Substitute(methodTemplate.OpenFunction.ReturnType, substitutions);
          var function = new FunctionSymbol(methodTemplate.OpenFunction.Name, returnType, parameters, methodTemplate.OpenFunction.SourceSpan, concreteType, methodTemplate.OpenFunction.IsStatic ? null : new ParameterSymbol("self", concreteType, -1, "self", methodTemplate.OpenFunction.SourceSpan), methodTemplate.OpenFunction.IsStatic, methodTemplate.OpenFunction.IsPublic, methodTemplate.OpenFunction.IsOperator, methodTemplate.OpenFunction.OperatorKind, methodTemplate.OpenFunction.DeclaringModule);
          methodTemplate.Instances.Add(concreteType, function);
          if (methodTemplate.Syntax.IsExternalBinding)
          {
            var previousModule = Session.Modules.CurrentModule;
            Session.ModuleResolver.SetCurrentModule(template.Module, includeFunctions: true);
            try
            {
              Session.ExternDeclarationBinder.BindExternalFunctionSignature(methodTemplate.Syntax, function);
            }
            finally
            {
              Session.ModuleResolver.SetCurrentModule(previousModule, includeFunctions: true);
            }
          }
  
          var group = Session.CallableDeclarationBinder.GetOrCreateUserMethodGroup(concreteType, function.Name);
          var duplicate = false;
          foreach (var existing in group.Methods)
          {
            if (Session.CallableDeclarationBinder.HaveSameParameterTypes(existing.Parameters, function.Parameters))
            {
              Session.Diagnostics.ReportDuplicateMethodSignature(function.SourceSpan, function.DisplayName);
              duplicate = true;
              break;
            }
          }
  
          if (!duplicate)
            group.AddMethod(new UserMethodSymbol(function));
          Session.Callables.ModulesByFunctionSymbol[function] = template.Module;
          Session.Generics.PendingMethodBindings.Add(new PendingGenericMethodBinding(methodTemplate.Syntax, function, template, substitutions));
        }
      }
    }
  }
}
