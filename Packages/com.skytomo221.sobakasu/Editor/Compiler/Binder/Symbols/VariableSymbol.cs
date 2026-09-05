using System;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal abstract class VariableSymbol : Symbol
  {
    protected VariableSymbol(
        string name,
        TypeSymbol type,
        bool isMutable,
        TextSpan declarationSpan)
        : base(name)
    {
      Type = type ?? throw new ArgumentNullException(nameof(type));
      IsMutable = isMutable;
      DeclarationSpan = declarationSpan;
    }

    public TypeSymbol Type { get; }
    public bool IsMutable { get; }
    public TextSpan DeclarationSpan { get; }
  }
}
