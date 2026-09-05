

using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class LocalVariableSymbol : VariableSymbol
  {
    public override SymbolKind Kind => SymbolKind.Local;

    public LocalVariableSymbol(
        string name,
        TypeSymbol type,
        bool isMutable,
        TextSpan declarationSpan)
        : base(name, type, isMutable, declarationSpan)
    {
    }
  }
}
