using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Modules;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class StateBindingPhase : BinderComponent
  {
    internal StateBindingPhase(BindingSession session) : base(session)
    {
    }

    internal IReadOnlyList<BoundStateDeclaration> Execute(StandardLibraryModule entryModule)
    {
      Session.ModuleResolver.SetCurrentModule(entryModule, includeFunctions: true);
      var declarations = Session.StateDeclarationBinder.CollectStateDeclarations(
          entryModule.Syntax.Members);
      return Session.StateDeclarationBinder.BindStateDeclarations(declarations);
    }
  }
}
