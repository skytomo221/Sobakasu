using System;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler.Semantics.Events
{
  internal sealed class EventParameterDefinition
  {
    public string SuggestedName { get; }
    public TypeSymbol Type { get; }
    public string UdonStorageName { get; }

    public EventParameterDefinition(
        string suggestedName,
        TypeSymbol type,
        string udonStorageName)
    {
      SuggestedName = suggestedName ?? throw new ArgumentNullException(nameof(suggestedName));
      Type = type ?? throw new ArgumentNullException(nameof(type));
      UdonStorageName = udonStorageName ?? throw new ArgumentNullException(nameof(udonStorageName));
    }
  }
}
