using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BindingPipeline : BinderComponent
  {
    internal BindingPipeline(BindingSession session) : base(session)
    {
    }

    internal BoundProgram BindProgram(CompilationUnitSyntax syntax)
    {
      return BindProgram(StandardLibraryModuleGraph.CreateSingle(syntax));
    }

    internal BoundProgram BindProgram(StandardLibraryModuleGraph graph)
    {
      Session.ModuleBindingPhase.Execute(graph);
      Session.TypeDeclarationBindingPhase.Execute(graph);
      Session.LanguageItemBindingPhase.Execute(graph);
      Session.CallableDeclarationBindingPhase.Execute(graph);
      var constants = Session.ConstantBindingPhase.Execute();
      var states = Session.StateBindingPhase.Execute(graph.EntryModule);
      var bodies = Session.BodyBindingPhase.Execute(graph);
      Session.ValidationPhase.Execute(bodies);

      return new BoundProgram(
          constants,
          states,
          bodies.Functions,
          bodies.Events,
          bodies.NetworkReceivers);
    }
  }
}
