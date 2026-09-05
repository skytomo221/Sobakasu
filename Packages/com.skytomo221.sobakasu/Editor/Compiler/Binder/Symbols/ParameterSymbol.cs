using System;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class ParameterSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.Parameter;
    public TypeSymbol Type { get; }
    public int Ordinal { get; }
    public string UdonStorageName { get; }
    public TextSpan? DeclarationSpan { get; }

    public ParameterSymbol(
        string name,
        TypeSymbol type,
        int ordinal,
        string udonStorageName = null,
        TextSpan? declarationSpan = null)
        : base(name)
    {
      Type = type ?? throw new ArgumentNullException(nameof(type));
      Ordinal = ordinal;
      UdonStorageName = udonStorageName ?? name;
      DeclarationSpan = declarationSpan;
    }
  }
}
