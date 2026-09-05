

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundExpressionStatement : BoundStatement
  {
    public BoundExpression Expression { get; }

    public BoundExpressionStatement(BoundExpression expression)
    {
      Expression = expression;
    }
  }
}
