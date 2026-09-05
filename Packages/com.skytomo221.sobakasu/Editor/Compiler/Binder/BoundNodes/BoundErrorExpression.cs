using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundErrorExpression : BoundExpression
  {
    public static readonly BoundErrorExpression Instance = new();

    public override TypeSymbol Type => TypeSymbol.Error;

    private BoundErrorExpression()
    {
    }
  }
}
