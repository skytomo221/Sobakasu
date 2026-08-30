namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class ValidationPhase : BinderComponent
  {
    internal ValidationPhase(BindingSession session) : base(session)
    {
    }

    internal void Execute(BodyBindingResult bodies)
    {
      Session.ConstructedTypeValidator.ValidateConstructedAggregateTypes();
      Session.RecursiveFunctionValidator.ReportRecursiveFunctions(bodies.Functions);
    }
  }
}
