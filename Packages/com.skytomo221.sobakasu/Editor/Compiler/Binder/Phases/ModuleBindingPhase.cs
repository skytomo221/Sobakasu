using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class ModuleBindingPhase : BinderComponent
  {
    internal ModuleBindingPhase(BindingSession session) : base(session)
    {
    }

    internal void Execute(StandardLibraryModuleGraph graph)
    {
      foreach (var module in graph.Modules)
      {
        Session.Modules.Types[module] = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal);
        Session.Modules.Functions[module] = new Dictionary<string, FunctionGroupSymbol>(StringComparer.Ordinal);
        Session.Constants.ModuleConstants[module] = new Dictionary<string, ConstantSymbol>(StringComparer.Ordinal);
        Session.Modules.Imports[module] = new Dictionary<string, Symbol>(StringComparer.Ordinal);
        Session.Modules.Aliases[module] = new Dictionary<string, Symbol>(StringComparer.Ordinal);
        Session.Modules.GlobImportNames[module] = new HashSet<string>(StringComparer.Ordinal);
        Session.Modules.PreludeImports[module] = new Dictionary<string, Symbol>(StringComparer.Ordinal);
        Session.Modules.Symbols[module] = new ModuleSymbol(module);
      }

      foreach (var module in graph.Modules)
      {
        if (module.Parent != null &&
            Session.Modules.Symbols.TryGetValue(module.Parent, out var parentSymbol))
        {
          parentSymbol.AttachChild(Session.Modules.Symbols[module]);
        }
      }

      foreach (var module in graph.Modules)
      {
        Session.ModuleResolver.SetCurrentModule(module, includeFunctions: false);
        foreach (var member in module.Syntax.Members)
        {
          if (member is StructDeclarationSyntax structDeclaration)
            Session.AggregateDeclarationBinder.CollectAggregateType(structDeclaration);
          else if (member is EnumDeclarationSyntax enumDeclaration)
            Session.AggregateDeclarationBinder.CollectAggregateType(enumDeclaration);

          if (member is ImplDeclarationSyntax implDeclaration &&
              implDeclaration.IsExternalBinding)
          {
            Session.ExternDeclarationBinder.CollectExternalTypeBinding(implDeclaration);
          }
        }

        Session.ConstantDeclarationBinder.CollectConstantDeclarations(module.Syntax.Members);
        Session.Modules.Types[module] = new Dictionary<string, TypeSymbol>(
            Session.Modules.VisibleTypes,
            StringComparer.Ordinal);
      }

      Session.ImportResolver.BuildModuleImports(graph, includeFunctions: false);
      Session.PreludeResolver.BuildPreludeImports(graph, includeFunctions: false);
    }
  }
}
