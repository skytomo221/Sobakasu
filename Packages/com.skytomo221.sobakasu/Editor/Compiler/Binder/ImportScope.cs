using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class UseDirectiveBinding
  {
    public string ImportedPath { get; }
    public string IntroducedName { get; }
    public Symbol ImportedSymbol { get; }
    public bool IsAlias { get; }

    public UseDirectiveBinding(
        string importedPath,
        string introducedName,
        Symbol importedSymbol,
        bool isAlias)
    {
      ImportedPath = importedPath ?? throw new ArgumentNullException(nameof(importedPath));
      IntroducedName = introducedName ?? throw new ArgumentNullException(nameof(introducedName));
      ImportedSymbol = importedSymbol ?? throw new ArgumentNullException(nameof(importedSymbol));
      IsAlias = isAlias;
    }

    public string GetDisplayTarget()
    {
      if (ImportedSymbol is NamespaceSymbol namespaceSymbol)
        return namespaceSymbol.QualifiedName;

      if (ImportedSymbol is TypeSymbol typeSymbol)
        return typeSymbol.QualifiedName;

      if (ImportedSymbol is MethodGroupSymbol methodGroup)
        return methodGroup.DisplayName;

      if (ImportedSymbol is MethodSymbol method)
        return method.DisplayName;

      return ImportedSymbol.Name;
    }
  }

  internal sealed class ImportScope
  {
    private readonly Dictionary<string, UseDirectiveBinding> _aliasBindings =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, UseDirectiveBinding> _directBindings =
        new(StringComparer.Ordinal);
    private readonly List<UseDirectiveBinding> _namespaceBindings = new();

    public IReadOnlyList<UseDirectiveBinding> NamespaceBindings => _namespaceBindings;

    public bool TryAddAlias(
        UseDirectiveBinding binding,
        DiagnosticBag diagnostics,
        TextSpan span)
    {
      return TryAddBinding(binding, diagnostics, span, _aliasBindings);
    }

    public bool TryAddDirectImport(
        UseDirectiveBinding binding,
        DiagnosticBag diagnostics,
        TextSpan span)
    {
      return TryAddBinding(binding, diagnostics, span, _directBindings);
    }

    public void TryAddNamespaceImport(UseDirectiveBinding binding)
    {
      if (binding == null)
        throw new ArgumentNullException(nameof(binding));

      foreach (var existingBinding in _namespaceBindings)
      {
        if (ReferenceEquals(existingBinding.ImportedSymbol, binding.ImportedSymbol))
          return;
      }

      _namespaceBindings.Add(binding);
    }

    public bool TryResolveAlias(string name, out Symbol symbol)
    {
      if (_aliasBindings.TryGetValue(name, out var binding))
      {
        symbol = binding.ImportedSymbol;
        return true;
      }

      symbol = null;
      return false;
    }

    public bool TryResolveDirectImport(string name, out Symbol symbol)
    {
      if (_directBindings.TryGetValue(name, out var binding))
      {
        symbol = binding.ImportedSymbol;
        return true;
      }

      symbol = null;
      return false;
    }

    public IReadOnlyList<Symbol> GetNamespaceCandidates(string name)
    {
      var candidates = new List<Symbol>();

      foreach (var binding in _namespaceBindings)
      {
        if (binding.ImportedSymbol is not NamespaceSymbol namespaceSymbol)
          continue;

        var candidate = namespaceSymbol.Lookup(name);
        if (candidate == null)
          continue;

        var duplicate = false;
        foreach (var existingCandidate in candidates)
        {
          if (ReferenceEquals(existingCandidate, candidate))
          {
            duplicate = true;
            break;
          }
        }

        if (!duplicate)
          candidates.Add(candidate);
      }

      return candidates;
    }

    private bool TryAddBinding(
        UseDirectiveBinding binding,
        DiagnosticBag diagnostics,
        TextSpan span,
        IDictionary<string, UseDirectiveBinding> targetDictionary)
    {
      if (binding == null)
        throw new ArgumentNullException(nameof(binding));

      if (diagnostics == null)
        throw new ArgumentNullException(nameof(diagnostics));

      if (TryGetNamedBinding(binding.IntroducedName, out var existingBinding))
      {
        if (ReferenceEquals(existingBinding.ImportedSymbol, binding.ImportedSymbol))
          return true;

        diagnostics.ReportImportConflict(
            span,
            binding.IntroducedName,
            existingBinding.GetDisplayTarget(),
            binding.GetDisplayTarget());
        return false;
      }

      targetDictionary.Add(binding.IntroducedName, binding);
      return true;
    }

    private bool TryGetNamedBinding(string introducedName, out UseDirectiveBinding binding)
    {
      if (_aliasBindings.TryGetValue(introducedName, out binding))
        return true;

      if (_directBindings.TryGetValue(introducedName, out binding))
        return true;

      binding = null;
      return false;
    }
  }
}
