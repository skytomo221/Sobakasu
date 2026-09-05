using System;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class AggregateFieldSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.AggregateField;
    public TypeSymbol ContainingType { get; }
    public TypeSymbol Type { get; }
    public int Ordinal { get; }
    public TextSpan DeclarationSpan { get; }
    public string ExternalMemberName { get; }

    public AggregateFieldSymbol(
        string name,
        TypeSymbol containingType,
        TypeSymbol type,
        int ordinal,
        TextSpan declarationSpan,
        string externalMemberName = null)
        : base(name)
    {
      ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
      Type = type ?? throw new ArgumentNullException(nameof(type));
      Ordinal = ordinal;
      DeclarationSpan = declarationSpan;
      ExternalMemberName = externalMemberName;
    }
  }
}
