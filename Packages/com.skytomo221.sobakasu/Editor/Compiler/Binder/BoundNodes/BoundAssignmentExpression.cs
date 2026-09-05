using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundAssignmentExpression : BoundExpression
  {
    public VariableSymbol Variable { get; }
    public BoundExpression Expression { get; }
    public override TypeSymbol Type => Variable.Type;

    public BoundAssignmentExpression(
        VariableSymbol variable,
        BoundExpression expression)
    {
      Variable = variable ?? throw new ArgumentNullException(nameof(variable));
      Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }
  }
}
