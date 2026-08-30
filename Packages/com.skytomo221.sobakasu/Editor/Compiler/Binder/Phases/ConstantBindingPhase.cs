using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class ConstantBindingPhase : BinderComponent
  {
    internal ConstantBindingPhase(BindingSession session) : base(session)
    {
    }

    internal IReadOnlyList<BoundConstantDeclaration> Execute()
    {
      return Session.ConstantDependencyAnalyzer.BindConstantDeclarations();
    }
  }
}
