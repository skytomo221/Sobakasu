using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class MethodGroupSymbol : Symbol
  {
    private readonly List<MethodSymbol> _methods = new();
    private readonly List<ExternCandidate> _rejectedCandidates = new();

    public override SymbolKind Kind => SymbolKind.MethodGroup;
    public TypeSymbol ContainingType { get; }
    public IReadOnlyList<MethodSymbol> Methods => _methods;
    public IReadOnlyList<ExternCandidate> RejectedCandidates => _rejectedCandidates;
    public string DisplayName => $"{ContainingType.Name}.{Name}";

    public MethodGroupSymbol(string name, TypeSymbol containingType)
        : base(name)
    {
      ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
    }

    public void AddMethod(MethodSymbol method)
    {
      if (method == null)
        throw new ArgumentNullException(nameof(method));

      _methods.Add(method);
    }

    public void AddRejectedCandidate(ExternCandidate candidate)
    {
      if (candidate == null)
        throw new ArgumentNullException(nameof(candidate));

      _rejectedCandidates.Add(candidate);
    }
  }
}
