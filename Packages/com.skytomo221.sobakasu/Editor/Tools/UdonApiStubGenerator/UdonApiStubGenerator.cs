using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Binder;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tools.UdonApiStubGenerator
{
  internal sealed class UdonApiGenerationResult
  {
    public IReadOnlyDictionary<string, string> Files { get; }
    public UdonApiGenerationReport Report { get; }

    public UdonApiGenerationResult(
        IReadOnlyDictionary<string, string> files,
        UdonApiGenerationReport report)
    {
      Files = files ?? throw new ArgumentNullException(nameof(files));
      Report = report ?? throw new ArgumentNullException(nameof(report));
    }
  }

  internal sealed class UdonApiStubGenerator
  {
    public const string ReportFileName = "generation_report.json";
    public const string SkippedMembersFileName = "skipped_members.txt";
    public const string DefaultRelativeOutputDirectory =
        "Packages/com.skytomo221.sobakasu/GeneratedUdonApiStubs~";

    private readonly UdonApiDiscovery _discovery;
    private readonly SobakasuStubRenderer _renderer;

    public static string DefaultOutputDirectory => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), DefaultRelativeOutputDirectory));

    public UdonApiStubGenerator(
        UdonApiDiscovery discovery,
        SobakasuStubRenderer renderer)
    {
      _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
      _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public static UdonApiStubGenerator CreateDefault()
    {
      var cache = UdonExposedNodeCache.Default;
      var typeFormatter = new UdonApiStubTypeFormatter(
          SobakasuBuiltInEnvironment.Default.ExternCatalog);
      return new UdonApiStubGenerator(
          new UdonApiDiscovery(
              new InstalledUdonApiExposure(cache),
              typeFormatter),
          new SobakasuStubRenderer(typeFormatter));
    }

    public UdonApiGenerationResult Generate()
    {
      return Generate(_discovery.Discover());
    }

    internal UdonApiGenerationResult Generate(IReadOnlyList<Type> candidateTypes)
    {
      return Generate(_discovery.Discover(candidateTypes));
    }

    public UdonApiGenerationResult GenerateToDirectory(string outputDirectory)
    {
      var result = Generate();
      new UdonApiStubOutputWriter().Write(outputDirectory, result);
      return result;
    }

    private UdonApiGenerationResult Generate(UdonApiModel model)
    {
      RejectDuplicateDeclarations(model);
      PlanOutputPaths(model);

      var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
      foreach (var type in model.Types)
      {
        if (type.IsGenerated)
          files.Add(type.RelativePath, _renderer.Render(type));
      }

      var report = CreateReport(model);
      files.Add(ReportFileName, RenderReportJson(report));
      files.Add(SkippedMembersFileName, RenderSkippedMembers(report));
      return new UdonApiGenerationResult(files, report);
    }

    private void RejectDuplicateDeclarations(UdonApiModel model)
    {
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;

        var declarations = new Dictionary<string, List<UdonApiMemberModel>>(
            StringComparer.Ordinal);
        foreach (var member in type.Members)
        {
          if (!member.IsGenerated)
            continue;

          var key = _renderer.GetDeclarationKey(type, member);
          if (!declarations.TryGetValue(key, out var group))
          {
            group = new List<UdonApiMemberModel>();
            declarations.Add(key, group);
          }
          group.Add(member);
        }

        foreach (var pair in declarations)
        {
          if (pair.Value.Count < 2)
            continue;

          foreach (var member in pair.Value)
          {
            member.SkipReason =
                $"Multiple CLR members map to the same Sobakasu declaration '{pair.Key}'.";
          }
        }
      }
    }

    private static void PlanOutputPaths(UdonApiModel model)
    {
      var typesByPath = new Dictionary<string, List<UdonApiTypeModel>>(
          StringComparer.OrdinalIgnoreCase);
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;

        var namespacePath = type.ClrType.Namespace.Replace('.', '/');
        var fileName =
            $"{SobakasuNameUtility.ToSnakeCase(type.WrapperName)}.sobakasu";
        type.RelativePath = $"{namespacePath}/{fileName}";
        if (!typesByPath.TryGetValue(type.RelativePath, out var group))
        {
          group = new List<UdonApiTypeModel>();
          typesByPath.Add(type.RelativePath, group);
        }
        group.Add(type);
      }

      foreach (var pair in typesByPath)
      {
        if (pair.Value.Count < 2)
          continue;

        foreach (var type in pair.Value)
        {
          type.SkipReason =
              $"The generated output path collides with another type: '{pair.Key}'.";
          type.SkipGeneratedMembers(
              $"Declaring type was skipped: {type.SkipReason}");
        }
      }
    }

    private static UdonApiGenerationReport CreateReport(UdonApiModel model)
    {
      var report = new UdonApiGenerationReport();
      foreach (var type in model.Types)
      {
        report.types_discovered++;
        if (type.IsGenerated)
        {
          report.types_generated++;
        }
        else
        {
          report.types_skipped++;
          report.skipped_types.Add(new UdonApiSkipRecord
          {
            full_name = type.QualifiedName,
            declaring_type = type.QualifiedName,
            member_kind = "type",
            signature = type.QualifiedName,
            extern_signature = string.Empty,
            reason = type.SkipReason
          });
        }

        foreach (var member in type.Members)
        {
          report.members_discovered++;
          if (type.IsGenerated && member.IsGenerated)
          {
            report.members_generated++;
          }
          else
          {
            report.members_skipped++;
            report.skipped_members.Add(new UdonApiSkipRecord
            {
              full_name = member.FullName,
              declaring_type = member.DeclaringTypeName,
              member_kind = ToSnakeCase(member.Kind.ToString()),
              signature = member.DisplaySignature,
              extern_signature = member.ExternSignature,
              reason = member.SkipReason ??
                  $"Declaring type was skipped: {type.SkipReason}"
            });
          }
        }
      }

      if (report.types_discovered !=
          report.types_generated + report.types_skipped)
      {
        throw new InvalidOperationException(
            "Type completeness invariant was violated.");
      }
      if (report.members_discovered !=
          report.members_generated + report.members_skipped)
      {
        throw new InvalidOperationException(
            "Member completeness invariant was violated.");
      }

      PopulateSkipReasonCounts(report);
      return report;
    }

    private static void PopulateSkipReasonCounts(UdonApiGenerationReport report)
    {
      var counts = new Dictionary<string, int>(StringComparer.Ordinal);
      foreach (var skippedType in report.skipped_types)
        Increment(counts, skippedType.reason);
      foreach (var skippedMember in report.skipped_members)
        Increment(counts, skippedMember.reason);

      var reasons = new List<UdonApiSkipReasonCount>();
      foreach (var pair in counts)
      {
        reasons.Add(new UdonApiSkipReasonCount
        {
          reason = pair.Key,
          count = pair.Value
        });
      }
      reasons.Sort((left, right) =>
      {
        var countComparison = right.count.CompareTo(left.count);
        return countComparison != 0
            ? countComparison
            : string.CompareOrdinal(left.reason, right.reason);
      });
      report.skip_reasons.AddRange(reasons);
    }

    private static string RenderSkippedMembers(UdonApiGenerationReport report)
    {
      var text = new StringBuilder();
      text.AppendLine("Skipped types");
      text.AppendLine("=============");
      foreach (var skippedType in report.skipped_types)
      {
        text.Append(skippedType.full_name);
        text.Append("\t");
        text.AppendLine(skippedType.reason);
      }

      text.AppendLine();
      text.AppendLine("Skipped members");
      text.AppendLine("===============");
      foreach (var member in report.skipped_members)
      {
        text.Append(member.member_kind);
        text.Append("\t");
        text.Append(member.full_name);
        text.Append("\t");
        text.Append(member.signature);
        text.Append("\t");
        text.Append(member.extern_signature);
        text.Append("\t");
        text.AppendLine(member.reason);
      }

      return NormalizeNewLines(text.ToString());
    }

    private static string RenderReportJson(UdonApiGenerationReport report)
    {
      var json = new StringBuilder();
      json.AppendLine("{");
      AppendJsonNumber(json, "types_discovered", report.types_discovered);
      AppendJsonNumber(json, "types_generated", report.types_generated);
      AppendJsonNumber(json, "types_skipped", report.types_skipped);
      AppendJsonNumber(json, "members_discovered", report.members_discovered);
      AppendJsonNumber(json, "members_generated", report.members_generated);
      AppendJsonNumber(json, "members_skipped", report.members_skipped);
      AppendSkipRecords(json, "skipped_types", report.skipped_types);
      json.AppendLine(",");
      AppendSkipRecords(json, "skipped_members", report.skipped_members);
      json.AppendLine(",");
      json.AppendLine("  \"skip_reasons\": [");
      for (var index = 0; index < report.skip_reasons.Count; index++)
      {
        var reason = report.skip_reasons[index];
        json.AppendLine("    {");
        json.Append("      \"reason\": ");
        AppendJsonString(json, reason.reason);
        json.AppendLine(",");
        json.Append("      \"count\": ");
        json.AppendLine(reason.count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        json.Append("    }");
        if (index + 1 < report.skip_reasons.Count)
          json.Append(',');
        json.AppendLine();
      }
      json.AppendLine("  ]");
      json.AppendLine("}");
      return NormalizeNewLines(json.ToString());
    }

    private static void AppendJsonNumber(
        StringBuilder json,
        string name,
        int value)
    {
      json.Append("  \"");
      json.Append(name);
      json.Append("\": ");
      json.Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
      json.AppendLine(",");
    }

    private static void AppendSkipRecords(
        StringBuilder json,
        string name,
        IReadOnlyList<UdonApiSkipRecord> records)
    {
      json.Append("  \"");
      json.Append(name);
      json.AppendLine("\": [");
      for (var index = 0; index < records.Count; index++)
      {
        var record = records[index];
        json.AppendLine("    {");
        AppendJsonProperty(json, "full_name", record.full_name);
        AppendJsonProperty(json, "declaring_type", record.declaring_type);
        AppendJsonProperty(json, "member_kind", record.member_kind);
        AppendJsonProperty(json, "signature", record.signature);
        AppendJsonProperty(json, "extern_signature", record.extern_signature);
        json.Append("      \"reason\": ");
        AppendJsonString(json, record.reason);
        json.AppendLine();
        json.Append("    }");
        if (index + 1 < records.Count)
          json.Append(',');
        json.AppendLine();
      }
      json.Append("  ]");
    }

    private static void AppendJsonProperty(
        StringBuilder json,
        string name,
        string value)
    {
      json.Append("      \"");
      json.Append(name);
      json.Append("\": ");
      AppendJsonString(json, value);
      json.AppendLine(",");
    }

    private static void AppendJsonString(StringBuilder json, string value)
    {
      json.Append('"');
      foreach (var character in value ?? string.Empty)
      {
        switch (character)
        {
          case '"':
            json.Append("\\\"");
            break;
          case '\\':
            json.Append("\\\\");
            break;
          case '\b':
            json.Append("\\b");
            break;
          case '\f':
            json.Append("\\f");
            break;
          case '\n':
            json.Append("\\n");
            break;
          case '\r':
            json.Append("\\r");
            break;
          case '\t':
            json.Append("\\t");
            break;
          default:
            if (character < ' ')
            {
              json.Append("\\u");
              json.Append(((int)character).ToString("x4"));
            }
            else
            {
              json.Append(character);
            }
            break;
        }
      }
      json.Append('"');
    }

    private static void Increment(IDictionary<string, int> counts, string reason)
    {
      reason ??= string.Empty;
      counts.TryGetValue(reason, out var count);
      counts[reason] = count + 1;
    }

    private static string ToSnakeCase(string value)
    {
      return SobakasuNameUtility.ToSnakeCase(value);
    }

    private static string NormalizeNewLines(string value)
    {
      return (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
    }
  }

  internal sealed class UdonApiStubOutputWriter
  {
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public void Write(string outputDirectory, UdonApiGenerationResult result)
    {
      if (result == null)
        throw new ArgumentNullException(nameof(result));

      var outputPath = ValidateOutputDirectory(outputDirectory);
      Directory.CreateDirectory(outputPath);
      foreach (var pair in result.Files)
      {
        var relativePath = pair.Key.Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.GetFullPath(Path.Combine(outputPath, relativePath));
        EnsurePathIsInsideOutput(outputPath, filePath);

        var parentDirectory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(parentDirectory))
          Directory.CreateDirectory(parentDirectory);

        using var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var writer = new StreamWriter(stream, Utf8WithoutBom)
        {
          NewLine = "\n"
        };
        writer.Write(pair.Value);
      }
    }

    internal static string ValidateOutputDirectory(string outputDirectory)
    {
      if (string.IsNullOrWhiteSpace(outputDirectory))
        throw new ArgumentException("An output directory is required.", nameof(outputDirectory));

      var outputPath = Path.GetFullPath(outputDirectory);
      if (File.Exists(outputPath))
        throw new IOException($"The output path is a file: '{outputPath}'.");

      var standardLibraryPath = Path.GetFullPath(Path.Combine(
          Directory.GetCurrentDirectory(),
          "Packages/com.skytomo221.sobakasu/StandardLibrary~"));
      if (IsSameOrDescendant(standardLibraryPath, outputPath))
      {
        throw new InvalidOperationException(
            "The generator cannot write to StandardLibrary~ or one of its descendants.");
      }

      if (Directory.Exists(outputPath))
      {
        using var entries = Directory.EnumerateFileSystemEntries(outputPath).GetEnumerator();
        if (entries.MoveNext())
        {
          throw new IOException(
              $"The output directory must be new or empty: '{outputPath}'.");
        }
      }

      return outputPath;
    }

    private static void EnsurePathIsInsideOutput(
        string outputDirectory,
        string filePath)
    {
      if (!IsSameOrDescendant(outputDirectory, filePath) ||
          string.Equals(
              Path.GetFullPath(outputDirectory),
              Path.GetFullPath(filePath),
              StringComparison.OrdinalIgnoreCase))
      {
        throw new InvalidOperationException(
            $"Generated file escaped the output directory: '{filePath}'.");
      }
    }

    private static bool IsSameOrDescendant(string parent, string candidate)
    {
      var parentPath = Path.GetFullPath(parent)
          .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      var candidatePath = Path.GetFullPath(candidate)
          .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      if (string.Equals(
              parentPath,
              candidatePath,
              StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }

      return candidatePath.StartsWith(
          parentPath + Path.DirectorySeparatorChar,
          StringComparison.OrdinalIgnoreCase);
    }
  }

  public static class UdonApiStubGeneratorCommandLine
  {
    public static void Generate()
    {
      var outputDirectory = GetArgument("-udonApiStubOutput") ??
          UdonApiStubGenerator.DefaultOutputDirectory;
      var result = UdonApiStubGenerator.CreateDefault()
          .GenerateToDirectory(outputDirectory);
      Debug.Log(
          $"Sobakasu Udon API stubs generated at '{Path.GetFullPath(outputDirectory)}'.\n" +
          $"Types: {result.Report.types_generated}/{result.Report.types_discovered}; " +
          $"Members: {result.Report.members_generated}/{result.Report.members_discovered}.");
    }

    private static string GetArgument(string name)
    {
      var arguments = Environment.GetCommandLineArgs();
      for (var index = 0; index + 1 < arguments.Length; index++)
      {
        if (string.Equals(arguments[index], name, StringComparison.Ordinal))
          return arguments[index + 1];
      }

      return null;
    }
  }
}
