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
  internal sealed class VisibilityResolver : BinderComponent
  {
    internal VisibilityResolver(BindingSession session) : base(session)
    {
    }
  
    internal bool CanAccessModule(StandardLibraryModule source, ResolvedUseDirective import)
    {
      var target = import.TargetModule;
      if (ReferenceEquals(source, target))
        return true;
      if (!target.IsConnected)
        return false;
      if (ReferenceEquals(target.Parent, source))
        return true;
      for (var current = target; current != null && !current.IsRoot; current = current.Parent)
      {
        if (!current.IsPublic)
          return Session.VisibilityResolver.IsPubliclyReExported(import);
      }
  
      return true;
    }
  
    internal bool IsPubliclyReExported(ResolvedUseDirective import)
    {
      if (!Session.Modules.Symbols.TryGetValue(import.TargetModule, out var targetSymbol))
        return false;
      var segments = import.Path.Split('.');
      var moduleSegmentCount = segments.Length - import.DeclarationPath.Count;
      if (moduleSegmentCount <= 0)
        return false;
      return targetSymbol.HasPublicPath(string.Join(".", segments, 0, moduleSegmentCount));
    }
  
    internal bool IsUserMethodVisible(UserMethodSymbol method)
    {
      return method.Function.IsPublic || string.Equals(method.Function.DeclaringModule ?? string.Empty, Session.Modules.CurrentModule?.LogicalName ?? string.Empty, StringComparison.Ordinal);
    }
  }
}
