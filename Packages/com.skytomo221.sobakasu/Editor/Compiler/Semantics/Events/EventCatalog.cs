using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler.Semantics.Events
{
  internal static class EventCatalog
  {
    private static readonly Dictionary<string, EventDefinition> DefinitionsByName;
    private static readonly EventDefinition[] AllDefinitions;
    private static readonly Dictionary<string, TypeSymbol> KnownTypes;

    static EventCatalog()
    {
      KnownTypes = CreateKnownTypes();

      var definitions = BuildDefinitions();
      AllDefinitions = definitions.ToArray();
      DefinitionsByName = new Dictionary<string, EventDefinition>(StringComparer.Ordinal);
      foreach (var definition in definitions)
        DefinitionsByName.Add(definition.SourceName, definition);
    }

    public static IReadOnlyCollection<EventDefinition> All => AllDefinitions;

    public static bool TryGet(string sourceName, out EventDefinition definition)
    {
      return DefinitionsByName.TryGetValue(sourceName, out definition);
    }

    public static bool TryGetKnownType(string sourceName, out TypeSymbol type)
    {
      return KnownTypes.TryGetValue(sourceName, out type);
    }

    private static List<EventDefinition> BuildDefinitions()
    {
      var definitions = new List<EventDefinition>();

      definitions.Add(Supported("PostLateUpdate", EventCategory.UdonUpdate));

      definitions.Add(Supported("Interact", EventCategory.UdonInput));
      AddInput(definitions, "InputJump", TypeSymbol.Bool, "boolValue");
      AddInput(definitions, "InputUse", TypeSymbol.Bool, "boolValue");
      AddInput(definitions, "InputGrab", TypeSymbol.Bool, "boolValue");
      AddInput(definitions, "InputDrop", TypeSymbol.Bool, "boolValue");
      AddInput(definitions, "InputMoveHorizontal", TypeSymbol.F32, "floatValue");
      AddInput(definitions, "InputMoveVertical", TypeSymbol.F32, "floatValue");
      AddInput(definitions, "InputLookHorizontal", TypeSymbol.F32, "floatValue");
      AddInput(definitions, "InputLookVertical", TypeSymbol.F32, "floatValue");

      definitions.Add(Supported("OnDrop", EventCategory.UdonPickup, requirement: "VRC_Pickup"));
      definitions.Add(Supported("OnPickup", EventCategory.UdonPickup, requirement: "VRC_Pickup"));
      definitions.Add(Supported("OnPickupUseDown", EventCategory.UdonPickup, requirement: "VRC_Pickup"));
      definitions.Add(Supported("OnPickupUseUp", EventCategory.UdonPickup, requirement: "VRC_Pickup"));

      definitions.Add(Supported(
          "OnOwnershipRequest",
          EventCategory.UdonNetworking,
          TypeSymbol.Bool,
          new[]
          {
            Param("OnOwnershipRequest", "requester", VrcPlayerApi, "requestingPlayer"),
            Param("OnOwnershipRequest", "newOwner", VrcPlayerApi, "requestedOwner")
          },
          returnValueStorageName: "__returnValue"));
      definitions.Add(Supported("OnOwnershipTransferred", EventCategory.UdonNetworking, parameters: new[] { Param("OnOwnershipTransferred", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPreSerialization", EventCategory.UdonNetworking));
      definitions.Add(Supported("OnPostSerialization", EventCategory.UdonNetworking, parameters: new[] { Param("OnPostSerialization", "result", SerializationResult) }));
      definitions.Add(Supported("OnDeserialization", EventCategory.UdonNetworking));

      definitions.Add(Supported("OnPlayerJoined", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerJoined", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPlayerLeft", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerLeft", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPlayerRespawn", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerRespawn", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPlayerTriggerEnter", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerTriggerEnter", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPlayerTriggerStay", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerTriggerStay", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPlayerTriggerExit", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerTriggerExit", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPlayerCollisionEnter", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerCollisionEnter", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPlayerCollisionStay", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerCollisionStay", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPlayerCollisionExit", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerCollisionExit", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnPlayerParticleCollision", EventCategory.UdonPlayer, parameters: new[] { Param("OnPlayerParticleCollision", "player", VrcPlayerApi) }));
      definitions.Add(Supported("OnControllerColliderHitPlayer", EventCategory.UdonPlayer, parameters: new[] { Param("OnControllerColliderHitPlayer", "hit", ControllerColliderPlayerHit) }));

      definitions.Add(Supported("OnStationEntered", EventCategory.UdonStation, parameters: new[] { Param("OnStationEntered", "player", VrcPlayerApi) }, requirement: "VRC_Station"));
      definitions.Add(Supported("OnStationExited", EventCategory.UdonStation, parameters: new[] { Param("OnStationExited", "player", VrcPlayerApi) }, requirement: "VRC_Station"));

      definitions.Add(Supported("OnVideoEnd", EventCategory.UdonVideo));
      definitions.Add(Supported("OnVideoError", EventCategory.UdonVideo, parameters: new[] { Param("OnVideoError", "videoError", VideoError) }));
      definitions.Add(Supported("OnVideoLoop", EventCategory.UdonVideo));
      definitions.Add(Supported("OnVideoPause", EventCategory.UdonVideo));
      definitions.Add(Supported("OnVideoPlay", EventCategory.UdonVideo));
      definitions.Add(Supported("OnVideoStart", EventCategory.UdonVideo));
      definitions.Add(Supported("OnVideoReady", EventCategory.UdonVideo));

      definitions.Add(Supported("MidiNoteOn", EventCategory.UdonMidi, parameters: new[] { Param("MidiNoteOn", "channel", TypeSymbol.I32), Param("MidiNoteOn", "number", TypeSymbol.I32), Param("MidiNoteOn", "velocity", TypeSymbol.I32) }));
      definitions.Add(Supported("MidiNoteOff", EventCategory.UdonMidi, parameters: new[] { Param("MidiNoteOff", "channel", TypeSymbol.I32), Param("MidiNoteOff", "number", TypeSymbol.I32), Param("MidiNoteOff", "velocity", TypeSymbol.I32) }));
      definitions.Add(Supported("MidiControlChange", EventCategory.UdonMidi, parameters: new[] { Param("MidiControlChange", "channel", TypeSymbol.I32), Param("MidiControlChange", "number", TypeSymbol.I32), Param("MidiControlChange", "value", TypeSymbol.I32) }));

      definitions.Add(Supported("OnImageLoadSuccess", EventCategory.UdonStringImageLoading, parameters: new[] { Param("OnImageLoadSuccess", "result", VrcImageDownload) }));
      definitions.Add(Supported("OnImageLoadError", EventCategory.UdonStringImageLoading, parameters: new[] { Param("OnImageLoadError", "result", VrcImageDownload) }));
      definitions.Add(Supported("OnStringLoadSuccess", EventCategory.UdonStringImageLoading, parameters: new[] { Param("OnStringLoadSuccess", "result", VrcStringDownload) }));
      definitions.Add(Supported("OnStringLoadError", EventCategory.UdonStringImageLoading, parameters: new[] { Param("OnStringLoadError", "result", VrcStringDownload) }));

      definitions.Add(Supported("Start", EventCategory.Unity));
      definitions.Add(Supported("Update", EventCategory.Unity));
      definitions.Add(Supported("FixedUpdate", EventCategory.Unity));
      definitions.Add(Supported("LateUpdate", EventCategory.Unity));
      definitions.Add(Supported("OnEnable", EventCategory.Unity));
      definitions.Add(Supported("OnDisable", EventCategory.Unity));
      definitions.Add(Supported("OnDestroy", EventCategory.Unity));

      foreach (var pendingUnityEvent in PendingUnityEvents)
        definitions.Add(Pending(pendingUnityEvent));

      return definitions;
    }

    private static EventDefinition Supported(
        string sourceName,
        EventCategory category,
        TypeSymbol returnType = null,
        IReadOnlyList<EventParameterDefinition> parameters = null,
        string requirement = null,
        string returnValueStorageName = null)
    {
      return new EventDefinition(
          sourceName,
          UdonName(sourceName),
          category,
          returnType ?? TypeSymbol.U0,
          parameters ?? Array.Empty<EventParameterDefinition>(),
          requirement,
          EventSupportLevel.Supported,
          returnValueStorageName);
    }

    private static EventDefinition Pending(string sourceName)
    {
      return new EventDefinition(
          sourceName,
          UdonName(sourceName),
          EventCategory.Unity,
          TypeSymbol.U0,
          Array.Empty<EventParameterDefinition>(),
          null,
          EventSupportLevel.PendingSignature);
    }

    private static void AddInput(
        ICollection<EventDefinition> definitions,
        string sourceName,
        TypeSymbol valueType,
        string udonValueName)
    {
      definitions.Add(Supported(
          sourceName,
          EventCategory.UdonInput,
          parameters: new[]
          {
            Param(sourceName, "value", valueType, udonValueName),
            Param(sourceName, "args", UdonInputEventArgs)
          }));
    }

    private static EventParameterDefinition Param(
        string eventName,
        string suggestedName,
        TypeSymbol type,
        string udonParameterName = null)
    {
      return new EventParameterDefinition(
          suggestedName,
          type,
          StorageName(eventName, udonParameterName ?? suggestedName));
    }

    private static string UdonName(string sourceName)
    {
      return "_" + LowerFirst(sourceName);
    }

    private static string StorageName(string eventName, string parameterName)
    {
      return LowerFirst(eventName) + UpperFirst(parameterName);
    }

    private static string LowerFirst(string value)
    {
      if (string.IsNullOrEmpty(value))
        return value;

      return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string UpperFirst(string value)
    {
      if (string.IsNullOrEmpty(value))
        return value;

      return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static Dictionary<string, TypeSymbol> CreateKnownTypes()
    {
      var knownTypes = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal);
      AddType(knownTypes, "VRCPlayerApi", VrcPlayerApi);
      AddType(knownTypes, "UdonInputEventArgs", UdonInputEventArgs);
      AddType(knownTypes, "SerializationResult", SerializationResult);
      AddType(knownTypes, "ControllerColliderPlayerHit", ControllerColliderPlayerHit);
      AddType(knownTypes, "VideoError", VideoError);
      AddType(knownTypes, "IVRCImageDownload", VrcImageDownload);
      AddType(knownTypes, "IVRCStringDownload", VrcStringDownload);
      return knownTypes;
    }

    private static void AddType(
        IDictionary<string, TypeSymbol> knownTypes,
        string shortName,
        TypeSymbol type)
    {
      knownTypes[shortName] = type;
      knownTypes[type.QualifiedName] = type;
    }

    private static TypeSymbol Named(string name, string qualifiedName)
    {
      return TypeSymbol.CreateNamed(name, qualifiedName);
    }

    private static readonly TypeSymbol VrcPlayerApi =
        Named("VRCPlayerApi", "VRC.SDKBase.VRCPlayerApi");
    private static readonly TypeSymbol UdonInputEventArgs =
        Named("UdonInputEventArgs", "VRC.Udon.Common.UdonInputEventArgs");
    private static readonly TypeSymbol SerializationResult =
        Named("SerializationResult", "VRC.Udon.Common.SerializationResult");
    private static readonly TypeSymbol ControllerColliderPlayerHit =
        Named("ControllerColliderPlayerHit", "VRC.SDK3.ControllerColliderPlayerHit");
    private static readonly TypeSymbol VideoError =
        Named("VideoError", "VRC.SDK3.Components.Video.VideoError");
    private static readonly TypeSymbol VrcImageDownload =
        Named("IVRCImageDownload", "VRC.SDK3.Image.IVRCImageDownload");
    private static readonly TypeSymbol VrcStringDownload =
        Named("IVRCStringDownload", "VRC.SDK3.StringLoading.IVRCStringDownload");

    private static readonly string[] PendingUnityEvents =
    {
      "OnCollisionEnter2D",
      "OnAnimatorIK",
      "OnAnimatorMove",
      "OnAudioFilterRead",
      "OnBecameVisible",
      "OnBecameInvisible",
      "OnCollisionEnter",
      "OnCollisionExit",
      "OnCollisionExit2D",
      "OnCollisionStay",
      "OnCollisionStay2D",
      "OnControllerColliderHit",
      "OnDrawGizmos",
      "OnDrawGizmosSelected",
      "OnJointBreak",
      "OnJointBreak2D",
      "OnMouseDown",
      "OnMouseDrag",
      "OnMouseEnter",
      "OnMouseExit",
      "OnMouseOver",
      "OnMouseUp",
      "OnMouseUpAsButton",
      "OnParticleCollision",
      "OnParticleTrigger",
      "OnPostRender",
      "OnPreCull",
      "OnPreRender",
      "OnRenderImage",
      "OnRenderObject",
      "OnTransformChildrenChanged",
      "OnTransformParentChanged",
      "OnTriggerEnter",
      "OnTriggerEnter2D",
      "OnTriggerExit",
      "OnTriggerExit2D",
      "OnTriggerStay",
      "OnTriggerStay2D",
      "OnValidate",
      "OnWillRenderObject",
      "Reset"
    };
  }
}
