using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundCallExpression : BoundExpression
  {
    public BoundExpression Target { get; }
    public IReadOnlyList<BoundExpression> Arguments { get; }
    public MethodSymbol Method { get; }
    public override TypeSymbol Type { get; }
    public BoundExpression ConstantEvaluationExpression { get; }

    public BoundCallExpression(
        BoundExpression target,
        IReadOnlyList<BoundExpression> arguments,
        MethodSymbol method,
        TypeSymbol type,
        BoundExpression constantEvaluationExpression = null)
    {
      Target = target;
      Arguments = arguments;
      Method = method;
      Type = type;
      ConstantEvaluationExpression = constantEvaluationExpression;
    }
  }
}
