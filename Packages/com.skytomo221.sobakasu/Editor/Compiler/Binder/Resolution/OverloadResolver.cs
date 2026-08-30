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
  internal sealed class OverloadResolver : BinderComponent
  {
    internal OverloadResolver(BindingSession session) : base(session)
    {
    }
  
    internal int GetSharedParameterCount(IReadOnlyList<MethodSymbol> methods)
    {
      if (methods.Count == 0)
        return -1;
      var count = methods[0].Parameters.Count;
      for (var index = 1; index < methods.Count; index++)
      {
        if (methods[index].Parameters.Count != count)
          return -1;
      }
  
      return count;
    }
  
    internal bool IsApplicable(ICallableSymbol callable, IReadOnlyList<BoundExpression> arguments)
    {
      for (var index = 0; index < arguments.Count; index++)
      {
        if (!Session.OverloadResolver.TryGetCallConversionDistance(callable.Parameters[index].Type, arguments[index].Type, callable.UsesExternalCallConversions, out _))
          return false;
      }
  
      return true;
    }
  
    internal TCallable SelectBestOverload<TCallable>(IReadOnlyList<TCallable> callables, IReadOnlyList<BoundExpression> arguments, out bool overloadResolutionWasAmbiguous)
      where TCallable : class, ICallableSymbol
    {
      overloadResolutionWasAmbiguous = false;
      TCallable bestCallable = null;
      var bestDistance = int.MaxValue;
      foreach (var callable in callables)
      {
        if (!Session.OverloadResolver.TryGetTotalCallDistance(callable, arguments, out var totalDistance))
          continue;
        if (bestCallable == null || totalDistance < bestDistance)
        {
          bestCallable = callable;
          bestDistance = totalDistance;
          overloadResolutionWasAmbiguous = false;
          continue;
        }
  
        if (totalDistance == bestDistance)
          overloadResolutionWasAmbiguous = true;
      }
  
      return bestCallable;
    }
  
    internal bool TryGetTotalCallDistance(ICallableSymbol callable, IReadOnlyList<BoundExpression> arguments, out int totalDistance)
    {
      totalDistance = 0;
      for (var index = 0; index < arguments.Count; index++)
      {
        if (!Session.OverloadResolver.TryGetCallConversionDistance(callable.Parameters[index].Type, arguments[index].Type, callable.UsesExternalCallConversions, out var distance))
        {
          totalDistance = 0;
          return false;
        }
  
        totalDistance += distance;
      }
  
      return true;
    }
  
    internal bool TryGetCallConversionDistance(TypeSymbol targetType, TypeSymbol sourceType, bool isExternalCall, out int distance)
    {
      if (Session.ConversionClassifier.TryGetConversionDistance(targetType, sourceType, out distance))
        return true;
      if (Session.ConversionClassifier.IsImplicitObjectBoxingConversion(targetType, sourceType))
      {
        distance = 1000;
        return true;
      }
  
      if (isExternalCall && !string.IsNullOrEmpty(targetType.RuntimeQualifiedName) && string.Equals(targetType.RuntimeQualifiedName, sourceType.RuntimeQualifiedName, StringComparison.Ordinal))
      {
        distance = 0;
        return true;
      }
  
      distance = 0;
      return false;
    }
  
    internal string BuildMethodCandidateList(IReadOnlyList<MethodSymbol> methods)
    {
      var candidates = new string[methods.Count];
      for (var index = 0; index < methods.Count; index++)
        candidates[index] = Session.OverloadResolver.BuildMethodSignature(methods[index]);
      return string.Join(", ", candidates);
    }
  
    internal string BuildMethodSignature(MethodSymbol method)
    {
      var parameterTypes = new string[method.Parameters.Count];
      for (var index = 0; index < method.Parameters.Count; index++)
        parameterTypes[index] = method.Parameters[index].Type.Name;
      return $"{method.DisplayName}({string.Join(", ", parameterTypes)})";
    }
  
    internal string BuildFunctionCandidateList(IReadOnlyList<FunctionSymbol> functions)
    {
      var candidates = new string[functions.Count];
      for (var index = 0; index < functions.Count; index++)
        candidates[index] = functions[index].Signature;
      return string.Join(", ", candidates);
    }
  
    internal string BuildFunctionArgumentTypeList(IReadOnlyList<BoundExpression> arguments)
    {
      var names = new string[arguments.Count];
      for (var index = 0; index < arguments.Count; index++)
        names[index] = arguments[index].Type.Name;
      return string.Join(", ", names);
    }
  
    internal string BuildRejectedCandidateDetail(IReadOnlyList<ExternCandidate> candidates)
    {
      if (candidates.Count == 0)
        return string.Empty;
      var maxCount = candidates.Count < 3 ? candidates.Count : 3;
      var details = new string[maxCount];
      for (var index = 0; index < maxCount; index++)
      {
        var candidate = candidates[index];
        details[index] = $"{candidate.DisplayName}: {candidate.RejectionReason}";
      }
  
      var detailText = string.Join("; ", details);
      if (candidates.Count > maxCount)
        detailText += $" (+{candidates.Count - maxCount} more)";
      return detailText;
    }
  
    internal string BuildArgumentTypeList(IReadOnlyList<BoundExpression> arguments)
    {
      if (arguments.Count == 0)
        return "(none)";
      var names = new string[arguments.Count];
      for (var index = 0; index < arguments.Count; index++)
        names[index] = arguments[index].Type.Name;
      return string.Join(", ", names);
    }
  }
}
