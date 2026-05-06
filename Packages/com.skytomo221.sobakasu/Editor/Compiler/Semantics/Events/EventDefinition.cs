using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler.Semantics.Events
{
  internal sealed class EventDefinition
  {
    public string SourceName { get; }
    public string UdonName { get; }
    public EventCategory Category { get; }
    public TypeSymbol ReturnType { get; }
    public IReadOnlyList<EventParameterDefinition> Parameters { get; }
    public string Requirement { get; }
    public EventSupportLevel SupportLevel { get; }
    public string ReturnValueStorageName { get; }

    public EventDefinition(
        string sourceName,
        string udonName,
        EventCategory category,
        TypeSymbol returnType,
        IReadOnlyList<EventParameterDefinition> parameters,
        string requirement,
        EventSupportLevel supportLevel,
        string returnValueStorageName = null)
    {
      SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
      UdonName = udonName ?? throw new ArgumentNullException(nameof(udonName));
      Category = category;
      ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
      Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
      Requirement = requirement;
      SupportLevel = supportLevel;
      ReturnValueStorageName = returnValueStorageName;
    }
  }
}
