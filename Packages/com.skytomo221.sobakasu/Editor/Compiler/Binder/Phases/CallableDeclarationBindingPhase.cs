using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class CallableDeclarationBindingPhase : BinderComponent
  {
    internal CallableDeclarationBindingPhase(BindingSession session) : base(session)
    {
    }

    internal void Execute(StandardLibraryModuleGraph graph)
    {
      foreach (var module in graph.Modules)
      {
        Session.ModuleResolver.SetCurrentModule(module, includeFunctions: false);
        foreach (var member in module.Syntax.Members)
        {
          if (member is not FunctionDeclarationSyntax functionDeclaration)
            continue;

          Session.CallableDeclarationBinder.CollectFunctionSignature(functionDeclaration);
          Session.Callables.FunctionModulesBySyntax[functionDeclaration] = module;
          if (Session.Callables.FunctionSymbolsBySyntax.TryGetValue(
                  functionDeclaration,
                  out var collectedFunction))
          {
            Session.Callables.ModulesByFunctionSymbol[collectedFunction] = module;
          }
        }

        Session.Modules.Functions[module] = new Dictionary<string, FunctionGroupSymbol>(
            Session.Modules.VisibleFunctions,
            StringComparer.Ordinal);
      }

      Session.ImportResolver.BuildModuleImports(graph, includeFunctions: true);
      Session.PreludeResolver.BuildPreludeImports(graph, includeFunctions: true);

      foreach (var module in graph.Modules)
      {
        Session.ModuleResolver.SetCurrentModule(module, includeFunctions: true);
        foreach (var member in module.Syntax.Members)
        {
          if (member is not ImplDeclarationSyntax implDeclaration)
            continue;

          Session.CallableDeclarationBinder.CollectImplMethodSignatures(implDeclaration);
          foreach (var method in implDeclaration.Methods)
          {
            Session.Callables.FunctionModulesBySyntax[method] = module;
            if (Session.Callables.MethodSymbolsBySyntax.TryGetValue(method, out var collectedMethod))
              Session.Callables.ModulesByFunctionSymbol[collectedMethod] = module;
          }
        }
      }

      Session.ModuleResolver.SetCurrentModule(graph.EntryModule, includeFunctions: true);
      Session.CallableDeclarationBinder.CollectNetworkReceiveSignatures(
          graph.EntryModule.Syntax.Members);
    }
  }
}
