using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundNameExpression : BoundExpression
  {
    public string Name { get; }
    public Symbol Symbol { get; }
    public override TypeSymbol Type { get; }

    public BoundNameExpression(
        string name,
        Symbol symbol,
        TypeSymbol type)
    {
      Name = name;
      Symbol = symbol;
      Type = type;
    }
  }
}
