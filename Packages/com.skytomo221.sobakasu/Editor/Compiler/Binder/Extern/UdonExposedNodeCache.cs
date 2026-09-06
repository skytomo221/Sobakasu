using System;
using System.Collections.Generic;
using System.Reflection;
using VRC.Udon.Editor;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class UdonExposedNodeCache
  {
    private static readonly Lazy<UdonExposedNodeCache> DefaultInstance =
        new(CreateDefault);

    private readonly HashSet<string> _exposedSignatures;
    private readonly HashSet<string> _exposedDeclaringTypeNames;

    public static UdonExposedNodeCache Default => DefaultInstance.Value;
    public IReadOnlyCollection<string> ExposedSignatures => _exposedSignatures;

    public UdonExposedNodeCache(IReadOnlyCollection<string> exposedSignatures)
    {
      if (exposedSignatures == null)
        throw new ArgumentNullException(nameof(exposedSignatures));

      _exposedSignatures = new HashSet<string>(exposedSignatures, StringComparer.Ordinal);
      _exposedDeclaringTypeNames = new HashSet<string>(StringComparer.Ordinal);
      foreach (var signature in _exposedSignatures)
      {
        var separator = signature.IndexOf('.');
        if (separator > 0)
          _exposedDeclaringTypeNames.Add(signature.Substring(0, separator));
      }
    }

    public bool IsExposed(string signature)
    {
      return !string.IsNullOrEmpty(signature) &&
             _exposedSignatures.Contains(signature);
    }

    public bool IsTypeExposed(Type type)
    {
      if (type == null)
        return false;

      if (type == typeof(void))
        return true;

      var typeName = UdonExternSignatureFormatter.GetUdonTypeName(type);
      return _exposedDeclaringTypeNames.Contains(typeName) ||
          UdonEditorManager.Instance.GetTypeFromTypeString(typeName) != null;
    }

    private static UdonExposedNodeCache CreateDefault()
    {
      UdonEditorManager.Instance.GetNodeRegistries();

      var signatures = new List<string>();
      foreach (var nodeDefinition in UdonEditorManager.Instance.GetNodeDefinitions())
        signatures.Add(nodeDefinition.fullName);

      return new UdonExposedNodeCache(signatures);
    }
  }

}
