using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
  [Serializable]
  internal sealed class UdonBindingGenerationConfig
  {
    public string version = "3";
    public UdonBindingRenames renames = new();
    public UdonBindingLangRule[] lang = Array.Empty<UdonBindingLangRule>();
    public UdonBindingPrelude prelude = new();
    public UdonBindingMaybe maybe = new();
    public UdonBindingExcludes excludes = new();

    [NonSerialized]
    private readonly Dictionary<string, int> _ruleMatchCounts = new(
        StringComparer.Ordinal);

    public static UdonBindingGenerationConfig CreateDefault()
    {
      return new UdonBindingGenerationConfig();
    }

    public static UdonBindingGenerationConfig Load(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return CreateDefault();

      var fullPath = Path.GetFullPath(path);
      if (!File.Exists(fullPath))
      {
        throw new UdonBindingConfigurationException(
            $"The Udon binding configuration file does not exist: '{fullPath}'.");
      }

      string json;
      try
      {
        json = File.ReadAllText(fullPath, Encoding.UTF8);
      }
      catch (Exception exception)
      {
        throw new UdonBindingConfigurationException(
            $"The Udon binding configuration could not be read: {exception.Message}",
            exception);
      }

      var namespaceTargets = UdonBindingJsonShapeValidator.Validate(json);
      UdonBindingGenerationConfig config;
      try
      {
        config = JsonUtility.FromJson<UdonBindingGenerationConfig>(json);
      }
      catch (Exception exception)
      {
        throw new UdonBindingConfigurationException(
            $"The Udon binding configuration is not valid JSON: {exception.Message}",
            exception);
      }

      config ??= new UdonBindingGenerationConfig();
      config.Normalize();
      if (namespaceTargets.Count != config.renames.namespaces.Length)
      {
        throw new UdonBindingConfigurationException(
            "The Udon binding configuration namespace renames could not be loaded consistently.");
      }
      for (var index = 0; index < config.renames.namespaces.Length; index++)
      {
        var rule = config.renames.namespaces[index];
        if (rule == null)
          continue;
        rule.ToSpecified = namespaceTargets[index] !=
            UdonBindingJsonShapeValidator.NamespaceTargetKind.Omitted;
        if (namespaceTargets[index] !=
            UdonBindingJsonShapeValidator.NamespaceTargetKind.String)
        {
          rule.to = null;
        }
      }
      return config;
    }

    public void Normalize()
    {
      version ??= "3";
      renames ??= new UdonBindingRenames();
      lang ??= Array.Empty<UdonBindingLangRule>();
      prelude ??= new UdonBindingPrelude();
      maybe ??= new UdonBindingMaybe();
      excludes ??= new UdonBindingExcludes();
      renames.Normalize();
      prelude.Normalize();
      maybe.Normalize();
      excludes.Normalize();
    }

    internal void ResetRuleMatches()
    {
      _ruleMatchCounts.Clear();
    }

    internal void MarkRuleMatched(string identity)
    {
      if (string.IsNullOrEmpty(identity))
        return;
      _ruleMatchCounts.TryGetValue(identity, out var count);
      _ruleMatchCounts[identity] = count + 1;
    }

    internal int GetRuleMatchCount(string identity)
    {
      return _ruleMatchCounts.TryGetValue(identity, out var count) ? count : 0;
    }
  }

  [Serializable]
  internal sealed class UdonBindingRenames
  {
    public UdonBindingNamespaceRenameRule[] namespaces =
        Array.Empty<UdonBindingNamespaceRenameRule>();
    public UdonBindingTypeRenameRule[] types =
        Array.Empty<UdonBindingTypeRenameRule>();
    public UdonBindingMemberRenameRule[] members =
        Array.Empty<UdonBindingMemberRenameRule>();

    public void Normalize()
    {
      namespaces ??= Array.Empty<UdonBindingNamespaceRenameRule>();
      types ??= Array.Empty<UdonBindingTypeRenameRule>();
      members ??= Array.Empty<UdonBindingMemberRenameRule>();
    }
  }

  [Serializable]
  internal sealed class UdonBindingPrelude
  {
    public string[] namespaces = Array.Empty<string>();
    public string[] types = Array.Empty<string>();
    public string[] members = Array.Empty<string>();

    public void Normalize()
    {
      namespaces ??= Array.Empty<string>();
      types ??= Array.Empty<string>();
      members ??= Array.Empty<string>();
    }
  }

  [Serializable]
  internal sealed class UdonBindingMaybe
  {
    public string[] returns = Array.Empty<string>();
    public UdonBindingMaybeOutRule[] outs = Array.Empty<UdonBindingMaybeOutRule>();

    public void Normalize()
    {
      returns ??= Array.Empty<string>();
      outs ??= Array.Empty<UdonBindingMaybeOutRule>();
      foreach (var rule in outs)
      {
        if (rule != null)
          rule.parameters ??= Array.Empty<string>();
      }
    }
  }

  [Serializable]
  internal sealed class UdonBindingExcludes
  {
    public string[] namespaces = Array.Empty<string>();
    public string[] types = Array.Empty<string>();
    public string[] members = Array.Empty<string>();

    public void Normalize()
    {
      namespaces ??= Array.Empty<string>();
      types ??= Array.Empty<string>();
      members ??= Array.Empty<string>();
    }
  }

  [Serializable]
  internal sealed class UdonBindingNamespaceRenameRule
  {
    public string from;
    public string to;

    [NonSerialized]
    internal bool ToSpecified = true;
  }

  [Serializable]
  internal sealed class UdonBindingTypeRenameRule
  {
    public string from;
    public string to;
  }

  [Serializable]
  internal sealed class UdonBindingMemberRenameRule
  {
    public string from;
    public string to;
  }

  [Serializable]
  internal sealed class UdonBindingLangRule
  {
    public string from;
    public string item;
  }

  [Serializable]
  internal sealed class UdonBindingMaybeOutRule
  {
    public string member;
    public string[] parameters = Array.Empty<string>();
  }

  internal sealed class UdonBindingConfigurationException : Exception
  {
    public UdonBindingConfigurationException(string message)
        : base(message)
    {
    }

    public UdonBindingConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
  }

  internal static class UdonBindingJsonShapeValidator
  {
    internal enum NamespaceTargetKind
    {
      Omitted,
      String,
      Null
    }

    private enum ObjectKind
    {
      Root,
      Renames,
      Lang,
      Prelude,
      Maybe,
      Excludes,
      NamespaceRename,
      TypeRename,
      MemberRename,
      MaybeOut
    }

    private sealed class Parser
    {
      private readonly string _text;
      private readonly List<NamespaceTargetKind> _namespaceTargets = new();
      private int _position;

      public IReadOnlyList<NamespaceTargetKind> NamespaceTargets =>
          _namespaceTargets;

      public Parser(string text)
      {
        _text = text ?? string.Empty;
      }

      public void Parse()
      {
        SkipWhitespace();
        ParseObject(ObjectKind.Root);
        SkipWhitespace();
        if (_position != _text.Length)
          Fail("Unexpected text after the root object.");
      }

      private void ParseObject(ObjectKind kind)
      {
        Expect('{');
        SkipWhitespace();
        var properties = new HashSet<string>(StringComparer.Ordinal);
        var nullProperties = new HashSet<string>(StringComparer.Ordinal);
        if (TryConsume('}'))
        {
          CompleteObject(kind, properties, nullProperties);
          return;
        }

        while (true)
        {
          SkipWhitespace();
          var property = ParseString();
          if (!properties.Add(property))
            Fail($"Property '{property}' is declared more than once.");
          SkipWhitespace();
          Expect(':');
          SkipWhitespace();
          ParseProperty(kind, property, nullProperties);
          SkipWhitespace();
          if (TryConsume('}'))
          {
            CompleteObject(kind, properties, nullProperties);
            return;
          }
          Expect(',');
        }
      }

      private void CompleteObject(
          ObjectKind kind,
          ISet<string> properties,
          ISet<string> nullProperties)
      {
        foreach (var required in GetRequiredProperties(kind))
        {
          if (!properties.Contains(required))
            Fail($"Required property '{required}' is missing from {GetObjectName(kind)}.");
        }

        if (kind == ObjectKind.NamespaceRename)
        {
          _namespaceTargets.Add(!properties.Contains("to")
              ? NamespaceTargetKind.Omitted
              : nullProperties.Contains("to")
                  ? NamespaceTargetKind.Null
                  : NamespaceTargetKind.String);
        }
      }

      private void ParseProperty(
          ObjectKind kind,
          string property,
          ISet<string> nullProperties)
      {
        switch (kind)
        {
          case ObjectKind.Root:
            switch (property)
            {
              case "version": ParseStringValue(); return;
              case "renames": ParseObject(ObjectKind.Renames); return;
              case "lang": ParseObjectArray(ObjectKind.Lang); return;
              case "prelude": ParseObject(ObjectKind.Prelude); return;
              case "maybe": ParseObject(ObjectKind.Maybe); return;
              case "excludes": ParseObject(ObjectKind.Excludes); return;
            }
            break;
          case ObjectKind.Renames:
            switch (property)
            {
              case "namespaces": ParseObjectArray(ObjectKind.NamespaceRename); return;
              case "types": ParseObjectArray(ObjectKind.TypeRename); return;
              case "members": ParseObjectArray(ObjectKind.MemberRename); return;
            }
            break;
          case ObjectKind.Prelude:
          case ObjectKind.Excludes:
            switch (property)
            {
              case "namespaces":
              case "types":
              case "members":
                ParseStringArray();
                return;
            }
            break;
          case ObjectKind.Maybe:
            switch (property)
            {
              case "returns": ParseStringArray(); return;
              case "outs": ParseObjectArray(ObjectKind.MaybeOut); return;
            }
            break;
          case ObjectKind.NamespaceRename:
            switch (property)
            {
              case "from": ParseStringValue(); return;
              case "to":
                if (ParseNullableStringValue())
                  nullProperties.Add(property);
                return;
            }
            break;
          case ObjectKind.TypeRename:
          case ObjectKind.MemberRename:
            switch (property)
            {
              case "from":
              case "to":
                ParseStringValue();
                return;
            }
            break;
          case ObjectKind.Lang:
            switch (property)
            {
              case "from":
              case "item":
                ParseStringValue();
                return;
            }
            break;
          case ObjectKind.MaybeOut:
            switch (property)
            {
              case "member": ParseStringValue(); return;
              case "parameters": ParseStringArray(); return;
            }
            break;
        }

        Fail($"Unknown property '{property}' in {GetObjectName(kind)}.");
      }

      private void ParseObjectArray(ObjectKind itemKind)
      {
        Expect('[');
        SkipWhitespace();
        if (TryConsume(']'))
          return;
        while (true)
        {
          ParseObject(itemKind);
          SkipWhitespace();
          if (TryConsume(']'))
            return;
          Expect(',');
          SkipWhitespace();
        }
      }

      private void ParseStringArray()
      {
        Expect('[');
        SkipWhitespace();
        if (TryConsume(']'))
          return;
        while (true)
        {
          ParseStringValue();
          SkipWhitespace();
          if (TryConsume(']'))
            return;
          Expect(',');
          SkipWhitespace();
        }
      }

      private void ParseStringValue()
      {
        ParseString();
      }

      private bool ParseNullableStringValue()
      {
        if (TryConsume("null"))
          return true;
        ParseStringValue();
        return false;
      }

      private string ParseString()
      {
        Expect('"');
        var value = new StringBuilder();
        while (_position < _text.Length)
        {
          var character = _text[_position++];
          if (character == '"')
            return value.ToString();
          if (character < ' ')
            Fail("A JSON string contains an unescaped control character.");
          if (character != '\\')
          {
            value.Append(character);
            continue;
          }

          if (_position >= _text.Length)
            Fail("A JSON string ends after an escape character.");
          var escaped = _text[_position++];
          switch (escaped)
          {
            case '"': value.Append('"'); break;
            case '\\': value.Append('\\'); break;
            case '/': value.Append('/'); break;
            case 'b': value.Append('\b'); break;
            case 'f': value.Append('\f'); break;
            case 'n': value.Append('\n'); break;
            case 'r': value.Append('\r'); break;
            case 't': value.Append('\t'); break;
            case 'u':
              if (_position + 4 > _text.Length)
                Fail("A JSON unicode escape is incomplete.");
              var hex = _text.Substring(_position, 4);
              if (!int.TryParse(
                      hex,
                      NumberStyles.HexNumber,
                      CultureInfo.InvariantCulture,
                      out var codePoint))
              {
                Fail($"'{hex}' is not a valid JSON unicode escape.");
              }
              value.Append((char)codePoint);
              _position += 4;
              break;
            default:
              Fail($"'\\{escaped}' is not a valid JSON escape.");
              break;
          }
        }

        Fail("A JSON string is not terminated.");
        return null;
      }

      private void SkipWhitespace()
      {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
          _position++;
      }

      private void Expect(char expected)
      {
        if (_position >= _text.Length || _text[_position] != expected)
          Fail($"Expected '{expected}'.");
        _position++;
      }

      private bool TryConsume(char value)
      {
        if (_position >= _text.Length || _text[_position] != value)
          return false;
        _position++;
        return true;
      }

      private bool TryConsume(string value)
      {
        if (_position + value.Length > _text.Length ||
            string.CompareOrdinal(_text, _position, value, 0, value.Length) != 0)
        {
          return false;
        }
        _position += value.Length;
        return true;
      }

      private void Fail(string message)
      {
        var line = 1;
        var column = 1;
        for (var index = 0; index < _position && index < _text.Length; index++)
        {
          if (_text[index] == '\n')
          {
            line++;
            column = 1;
          }
          else
          {
            column++;
          }
        }
        throw new UdonBindingConfigurationException(
            $"Invalid Udon binding configuration at line {line}, column {column}: {message}");
      }

      private static IReadOnlyList<string> GetRequiredProperties(ObjectKind kind)
      {
        return kind switch
        {
          ObjectKind.Root => new[] { "version", "renames", "lang", "prelude", "maybe", "excludes" },
          ObjectKind.Renames => new[] { "namespaces", "types", "members" },
          ObjectKind.Prelude => new[] { "namespaces", "types", "members" },
          ObjectKind.Maybe => new[] { "returns", "outs" },
          ObjectKind.Excludes => new[] { "namespaces", "types", "members" },
          ObjectKind.NamespaceRename => new[] { "from", "to" },
          ObjectKind.TypeRename => new[] { "from", "to" },
          ObjectKind.MemberRename => new[] { "from", "to" },
          ObjectKind.Lang => new[] { "from", "item" },
          ObjectKind.MaybeOut => new[] { "member", "parameters" },
          _ => Array.Empty<string>()
        };
      }

      private static string GetObjectName(ObjectKind kind)
      {
        return kind switch
        {
          ObjectKind.Root => "the root object",
          ObjectKind.Renames => "renames",
          ObjectKind.Prelude => "prelude",
          ObjectKind.Maybe => "maybe",
          ObjectKind.Excludes => "excludes",
          ObjectKind.NamespaceRename => "a namespace rename",
          ObjectKind.TypeRename => "a type rename",
          ObjectKind.MemberRename => "a member rename",
          ObjectKind.Lang => "a language item rule",
          ObjectKind.MaybeOut => "a maybe out rule",
          _ => "the configuration"
        };
      }
    }

    public static IReadOnlyList<NamespaceTargetKind> Validate(string json)
    {
      var parser = new Parser(json);
      parser.Parse();
      return parser.NamespaceTargets;
    }
  }
}
