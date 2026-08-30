namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal abstract class BinderComponent
  {
    protected BinderComponent(BindingSession session)
    {
      Session = session ?? throw new System.ArgumentNullException(nameof(session));
    }

    protected BindingSession Session { get; }
  }
}