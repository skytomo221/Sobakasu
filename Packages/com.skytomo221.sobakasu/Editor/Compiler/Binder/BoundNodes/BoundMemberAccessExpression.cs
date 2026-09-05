using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundMemberAccessExpression : BoundExpression
  {
    public BoundExpression Receiver { get; }
    public string MemberName { get; }
    public Symbol MemberSymbol { get; }
    public override TypeSymbol Type { get; }

    public BoundMemberAccessExpression(
        BoundExpression receiver,
        string memberName,
        Symbol memberSymbol,
        TypeSymbol type)
    {
      Receiver = receiver;
      MemberName = memberName;
      MemberSymbol = memberSymbol;
      Type = type;
    }
  }
}
