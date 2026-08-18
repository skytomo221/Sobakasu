using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tools.UdonApiStubGenerator
{
  [Serializable]
  internal sealed class UdonApiStubGenerationConfig
  {
    public string version = "1";
    public UdonApiStubGenerationDefaults defaults = new();
    public UdonApiStubNamespaceRule[] namespaces = Array.Empty<UdonApiStubNamespaceRule>();
    public UdonApiStubTypeRule[] types = Array.Empty<UdonApiStubTypeRule>();
    public UdonApiStubMemberRule[] members = Array.Empty<UdonApiStubMemberRule>();

    public static UdonApiStubGenerationConfig CreateDefault()
    {
      return new UdonApiStubGenerationConfig();
    }

    public static UdonApiStubGenerationConfig Load(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return CreateDefault();

      var fullPath = Path.GetFullPath(path);
      if (!File.Exists(fullPath))
      {
        throw new UdonApiStubConfigurationException(
            $"The Udon API stub configuration file does not exist: '{fullPath}'.");
      }

      string json;
      try
      {
        json = File.ReadAllText(fullPath, Encoding.UTF8);
      }
      catch (Exception exception)
      {
        throw new UdonApiStubConfigurationException(
            $"The Udon API stub configuration could not be read: {exception.Message}",
            exception);
      }

      UdonApiStubJsonShapeValidator.Validate(json);
      UdonApiStubGenerationConfig config;
      try
      {
        config = JsonUtility.FromJson<UdonApiStubGenerationConfig>(json);
      }
      catch (Exception exception)
      {
        throw new UdonApiStubConfigurationException(
            $"The Udon API stub configuration is not valid JSON: {exception.Message}",
            exception);
      }

      config ??= new UdonApiStubGenerationConfig();
      config.Normalize();
      return config;
    }

    public void Normalize()
    {
      version ??= "1";
      defaults ??= new UdonApiStubGenerationDefaults();
      namespaces ??= Array.Empty<UdonApiStubNamespaceRule>();
      types ??= Array.Empty<UdonApiStubTypeRule>();
      members ??= Array.Empty<UdonApiStubMemberRule>();
      foreach (var member in members)
      {
        if (member == null)
          continue;
        member.parameter_types ??= Array.Empty<string>();
        member.@out ??= Array.Empty<UdonApiStubOutRule>();
      }
    }
  }

  [Serializable]
  internal sealed class UdonApiStubGenerationDefaults
  {
    public string @namespace = "external";
    public string reference_return = "raw";
    public string reference_out = "raw";
    public string static_class_placement = "top_level";
    public bool predicate_naming = true;
  }

  [Serializable]
  internal sealed class UdonApiStubNamespaceRule
  {
    public string clr_namespace;
    public string @namespace;
    public bool preserve_subnamespaces;

    [NonSerialized]
    internal int MatchCount;
  }

  [Serializable]
  internal sealed class UdonApiStubTypeRule
  {
    public string type;
    public string @namespace;
    public string placement;
    public string name;

    [NonSerialized]
    internal int MatchCount;
  }

  [Serializable]
  internal sealed class UdonApiStubMemberRule
  {
    public string declaring_type;
    public string member_kind;
    public string member;
    public string[] parameter_types = Array.Empty<string>();
    public string @return;
    public UdonApiStubOutRule[] @out = Array.Empty<UdonApiStubOutRule>();
    public string name;
    public bool exclude;

    [NonSerialized]
    internal int MatchCount;
  }

  [Serializable]
  internal sealed class UdonApiStubOutRule
  {
    public string parameter;
    public string projection;
  }

  internal sealed class UdonApiStubConfigurationException : Exception
  {
    public UdonApiStubConfigurationException(string message)
        : base(message)
    {
    }

    public UdonApiStubConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
  }

  internal static class UdonApiStubJsonShapeValidator
  {
    private enum ObjectKind
    {
      Root,
      Defaults,
      NamespaceRule,
      TypeRule,
      MemberRule,
      OutRule
    }

    private sealed class Parser
    {
      private readonly string _text;
      private int _position;

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
        if (TryConsume('}'))
          return;

        var properties = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
          SkipWhitespace();
          var property = ParseString();
          if (!properties.Add(property))
            Fail($"Property '{property}' is declared more than once.");
          SkipWhitespace();
          Expect(':');
          SkipWhitespace();
          ParseProperty(kind, property);
          SkipWhitespace();
          if (TryConsume('}'))
            return;
          Expect(',');
        }
      }

      private void ParseProperty(ObjectKind kind, string property)
      {
        switch (kind)
        {
          case ObjectKind.Root:
            switch (property)
            {
              case "version": ParseStringValue(); return;
              case "defaults": ParseObject(ObjectKind.Defaults); return;
              case "namespaces": ParseObjectArray(ObjectKind.NamespaceRule); return;
              case "types": ParseObjectArray(ObjectKind.TypeRule); return;
              case "members": ParseObjectArray(ObjectKind.MemberRule); return;
            }
            break;
          case ObjectKind.Defaults:
            switch (property)
            {
              case "namespace":
              case "reference_return":
              case "reference_out":
              case "static_class_placement":
                ParseStringValue();
                return;
              case "predicate_naming":
                ParseBoolean();
                return;
            }
            break;
          case ObjectKind.NamespaceRule:
            switch (property)
            {
              case "clr_namespace":
              case "namespace":
                ParseStringValue();
                return;
              case "preserve_subnamespaces":
                ParseBoolean();
                return;
            }
            break;
          case ObjectKind.TypeRule:
            switch (property)
            {
              case "type":
              case "namespace":
              case "placement":
              case "name":
                ParseStringValue();
                return;
            }
            break;
          case ObjectKind.MemberRule:
            switch (property)
            {
              case "declaring_type":
              case "member_kind":
              case "member":
              case "return":
              case "name":
                ParseStringValue();
                return;
              case "parameter_types":
                ParseStringArray();
                return;
              case "out":
                ParseObjectArray(ObjectKind.OutRule);
                return;
              case "exclude":
                ParseBoolean();
                return;
            }
            break;
          case ObjectKind.OutRule:
            switch (property)
            {
              case "parameter":
              case "projection":
                ParseStringValue();
                return;
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
          SkipWhitespace();
          ParseObject(itemKind);
          SkipWhitespace();
          if (TryConsume(']'))
            return;
          Expect(',');
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

      private void ParseBoolean()
      {
        if (TryConsume("true") || TryConsume("false"))
          return;
        Fail("Expected a JSON boolean.");
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
        throw new UdonApiStubConfigurationException(
            $"Invalid Udon API stub configuration at line {line}, column {column}: {message}");
      }

      private static string GetObjectName(ObjectKind kind)
      {
        return kind switch
        {
          ObjectKind.Root => "the root object",
          ObjectKind.Defaults => "defaults",
          ObjectKind.NamespaceRule => "a namespace rule",
          ObjectKind.TypeRule => "a type rule",
          ObjectKind.MemberRule => "a member rule",
          ObjectKind.OutRule => "an out projection rule",
          _ => "the configuration"
        };
      }
    }

    public static void Validate(string json)
    {
      new Parser(json).Parse();
    }
  }
}
