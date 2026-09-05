using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal class MethodSymbol : Symbol, ICallableSymbol
  {
    public override SymbolKind Kind => SymbolKind.Method;
    public TypeSymbol ContainingType { get; }
    public IReadOnlyList<ParameterSymbol> Parameters { get; }
    public TypeSymbol ReturnType { get; }
    public bool IsStatic { get; }
    public virtual string ExternSignature => null;
    public virtual bool UsesExternalCallConversions => false;
    public string DisplayName => $"{ContainingType.Name}.{Name}";

    public MethodSymbol(
        string name,
        TypeSymbol containingType,
        IReadOnlyList<ParameterSymbol> parameters,
        TypeSymbol returnType,
        bool isStatic)
        : base(name)
    {
      ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
      Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
      ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
      IsStatic = isStatic;
    }
  }
}
