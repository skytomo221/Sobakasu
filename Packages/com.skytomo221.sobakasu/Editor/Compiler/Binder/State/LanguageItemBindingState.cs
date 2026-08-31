using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal static class LanguageItemNames
  {
    internal const string Maybe = "maybe";
    internal const string NetworkEventTarget = "network_event_target";

    private static readonly HashSet<string> KnownItems = new(StringComparer.Ordinal)
    {
      Maybe,
      NetworkEventTarget,
      "bool",
      "char",
      "i8",
      "u8",
      "i16",
      "u16",
      "i32",
      "u32",
      "i64",
      "u64",
      "f32",
      "f64",
      "string"
    };

    internal static bool IsKnown(string item)
    {
      return KnownItems.Contains(item);
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
