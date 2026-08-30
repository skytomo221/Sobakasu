using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal static class LanguageItemNames
  {
    internal const string Maybe = "maybe";
    internal const string NetworkEventTarget = "network_event_target";

    internal static bool IsKnown(string item)
    {
      return string.Equals(item, Maybe, StringComparison.Ordinal) ||
          string.Equals(item, NetworkEventTarget, StringComparison.Ordinal);
    }
  }

  internal sealed class LanguageItemBindingState
  {
    internal Dictionary<string, TypeSymbol> Types { get; } =
        new(StringComparer.Ordinal);

    internal bool TryGetType(string item, out TypeSymbol type)
    {
      return Types.TryGetValue(item, out type);
    }
  }
}
