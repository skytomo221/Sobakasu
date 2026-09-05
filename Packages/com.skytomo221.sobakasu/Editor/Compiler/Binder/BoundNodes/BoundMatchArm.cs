using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundMatchArm
  {
    public BoundPattern Pattern { get; }
    public BoundExpression Expression { get; }
    public bool IsReachable { get; }

    public BoundMatchArm(
        BoundPattern pattern,
        BoundExpression expression,
        bool isReachable)
    {
      Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
      Expression = expression ?? throw new ArgumentNullException(nameof(expression));
      IsReachable = isReachable;
    }
  }
}
