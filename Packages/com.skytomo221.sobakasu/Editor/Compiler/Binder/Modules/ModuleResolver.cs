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
  internal sealed class ModuleResolver : BinderComponent
  {
    internal ModuleResolver(BindingSession session) : base(session)
    {
    }
  
    internal void SetCurrentModule(StandardLibraryModule module, bool includeFunctions)
    {
      Session.Modules.CurrentModule = module;
      Session.Diagnostics.SourcePath = module?.SourcePath ?? string.Empty;
      Session.Modules.VisibleFunctions.Clear();
      Session.Modules.VisibleTypes.Clear();
      Session.Modules.VisibleConstants.Clear();
      if (module == null)
        return;
      if (Session.Modules.Types.TryGetValue(module, out var types))
      {
        foreach (var pair in types)
          Session.Modules.VisibleTypes[pair.Key] = pair.Value;
      }
  
      if (includeFunctions && Session.Modules.Functions.TryGetValue(module, out var functions))
      {
        foreach (var pair in functions)
          Session.Modules.VisibleFunctions[pair.Key] = pair.Value;
      }
  
      if (Session.Constants.ModuleConstants.TryGetValue(module, out var constants))
      {
        foreach (var pair in constants)
          Session.Modules.VisibleConstants[pair.Key] = pair.Value;
      }
  
      if (Session.Modules.Aliases.TryGetValue(module, out var aliases))
        Session.ModuleResolver.AddVisibleImports(aliases, includeFunctions);
      if (Session.Modules.Imports.TryGetValue(module, out var imports))
        Session.ModuleResolver.AddVisibleImports(imports, includeFunctions);
      if (Session.Modules.PreludeImports.TryGetValue(module, out var preludeImports))
        Session.ModuleResolver.AddVisibleImports(preludeImports, includeFunctions);
    }
  
    internal void AddVisibleImports(IReadOnlyDictionary<string, Symbol> imports, bool includeFunctions)
    {
      foreach (var pair in imports)
      {
        if (pair.Value is TypeSymbol importedType)
        {
          if (!Session.Modules.VisibleTypes.ContainsKey(pair.Key))
            Session.Modules.VisibleTypes.Add(pair.Key, importedType);
        }
        else if (includeFunctions && pair.Value is FunctionGroupSymbol importedFunctions)
        {
          if (!Session.Modules.VisibleFunctions.ContainsKey(pair.Key))
            Session.Modules.VisibleFunctions.Add(pair.Key, importedFunctions);
        }
      }
    }
  }
}
