using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class NamespaceSymbol : Symbol
  {
    private readonly Dictionary<string, NamespaceSymbol> _namespaces =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeSymbol> _types =
        new(StringComparer.Ordinal);

    public override SymbolKind Kind => SymbolKind.Namespace;
    public string QualifiedName { get; }

    public NamespaceSymbol(string name, string qualifiedName = null)
        : base(name)
    {
      QualifiedName = qualifiedName ?? name;
    }

    public NamespaceSymbol GetOrAddNamespace(string name)
    {
      if (_namespaces.TryGetValue(name, out var existingNamespace))
        return existingNamespace;

      var qualifiedName = string.IsNullOrEmpty(QualifiedName)
          ? name
          : $"{QualifiedName}.{name}";
      var namespaceSymbol = new NamespaceSymbol(name, qualifiedName);
      _namespaces.Add(name, namespaceSymbol);
      return namespaceSymbol;
    }

    public void AddNamespace(NamespaceSymbol namespaceSymbol)
    {
      if (namespaceSymbol == null)
        throw new ArgumentNullException(nameof(namespaceSymbol));

      _namespaces[namespaceSymbol.Name] = namespaceSymbol;
    }

    public void AddType(TypeSymbol typeSymbol)
    {
      if (typeSymbol == null)
        throw new ArgumentNullException(nameof(typeSymbol));

      _types[typeSymbol.Name] = typeSymbol;
    }

    public Symbol Lookup(string name)
    {
      if (_namespaces.TryGetValue(name, out var namespaceSymbol))
        return namespaceSymbol;

      if (_types.TryGetValue(name, out var typeSymbol))
        return typeSymbol;

      return null;
    }
  }
}
