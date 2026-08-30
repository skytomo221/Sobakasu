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
  internal sealed class AggregateDependencyValidator : BinderComponent
  {
    internal AggregateDependencyValidator(BindingSession session) : base(session)
    {
    }
  
    internal void ValidateAggregateDependencies()
    {
      var states = new Dictionary<TypeSymbol, int>();
      var stack = new List<TypeSymbol>();
      foreach (var pair in Session.Declarations.AggregateTypesBySyntax)
      {
        var type = pair.Value;
        if (!states.ContainsKey(type))
          Session.AggregateDependencyValidator.VisitAggregateDependency(type, states, stack);
      }
  
      var validated = new HashSet<TypeSymbol>();
      foreach (var type in Session.Declarations.AggregateTypesBySyntax.Values)
      {
        if (!validated.Add(type))
          continue;
        if (type.ContainsGenericParameters)
          continue;
        foreach (var leaf in AggregateLayout.GetLeaves(type))
        {
          var supported = leaf.Type.TypeKind == TypeKind.Array ? Session.Environment.ExternCatalog.TryGetArrayIntrinsics(leaf.Type, out _, out _) : leaf.Type != TypeSymbol.Unit && leaf.Type != TypeSymbol.Never && Session.Environment.ExternCatalog.TryGetClrType(leaf.Type, out _);
          if (!supported)
          {
            Session.Diagnostics.ReportUnsupportedAggregateLeafAbi(new TextSpan(0, 0), type.Name, leaf.PathText, leaf.Type.Name);
          }
        }
      }
    }
  
    internal void VisitAggregateDependency(TypeSymbol type, IDictionary<TypeSymbol, int> states, IList<TypeSymbol> stack)
    {
      states[type] = 1;
      stack.Add(type);
      foreach (var dependency in Session.AggregateDependencyValidator.GetAggregateDependencies(type))
      {
        if (!states.TryGetValue(dependency, out var state))
        {
          Session.AggregateDependencyValidator.VisitAggregateDependency(dependency, states, stack);
          continue;
        }
  
        if (state != 1)
          continue;
        var start = 0;
        while (start < stack.Count && !ReferenceEquals(stack[start], dependency))
          start++;
        var cycle = new List<string>();
        for (var index = start; index < stack.Count; index++)
          cycle.Add(stack[index].Name);
        cycle.Add(dependency.Name);
        Session.Diagnostics.ReportRecursiveAggregate(dependency.AggregateFields.Count > 0 ? dependency.AggregateFields[0].DeclarationSpan : dependency.EnumVariants.Count > 0 ? dependency.EnumVariants[0].DeclarationSpan : new TextSpan(0, 0), string.Join(" -> ", cycle));
      }
  
      stack.RemoveAt(stack.Count - 1);
      states[type] = 2;
    }
  
    internal IEnumerable<TypeSymbol> GetAggregateDependencies(TypeSymbol type)
    {
      if (type.AggregateKind == UserAggregateKind.Struct || type.AggregateKind == UserAggregateKind.Tuple)
      {
        foreach (var field in type.AggregateFields)
        {
          var dependency = Session.AggregateDependencyValidator.GetAggregateDependency(field.Type);
          if (dependency != null)
            yield return dependency;
        }
  
        yield break;
      }
  
      foreach (var variant in type.EnumVariants)
        foreach (var field in variant.Fields)
        {
          var dependency = Session.AggregateDependencyValidator.GetAggregateDependency(field.Type);
          if (dependency != null)
            yield return dependency;
        }
    }
  
    internal TypeSymbol GetAggregateDependency(TypeSymbol type)
    {
      while (type?.TypeKind == TypeKind.Array)
        type = type.ElementType;
      return type?.IsAggregate == true ? type : null;
    }
  }
}
