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
  internal sealed class PreludeResolver : BinderComponent
  {
    internal PreludeResolver(BindingSession session) : base(session)
    {
    }
  
    internal void BuildPreludeImports(StandardLibraryModuleGraph graph, bool includeFunctions)
    {
      foreach (var module in graph.Modules)
        Session.Modules.PreludeImports[module].Clear();
      if (graph.PreludeModule == null || !Session.Modules.Symbols.TryGetValue(graph.PreludeModule, out var preludeSymbol))
      {
        return;
      }
  
      foreach (var module in graph.Modules)
      {
        if (module.IsStandardLibrary || ReferenceEquals(module, graph.PreludeModule))
          continue;
        var imports = Session.Modules.PreludeImports[module];
        foreach (var pair in preludeSymbol.Exports)
        {
          if (!includeFunctions && pair.Value is FunctionGroupSymbol)
            continue;
          imports[pair.Key] = pair.Value;
        }
      }
    }
  }
}
