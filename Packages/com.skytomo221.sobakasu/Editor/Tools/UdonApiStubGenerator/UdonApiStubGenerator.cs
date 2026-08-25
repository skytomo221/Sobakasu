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
    private sealed class ModulePlan
    {
      public SortedDictionary<string, UdonApiGeneratedTypeModel> TypeModules { get; } =
          new(StringComparer.Ordinal);
      public SortedSet<string> Children { get; } = new(StringComparer.Ordinal);
    }

    private sealed class ModulePathUse
    {
      public string Namespace { get; }
      public UdonApiGeneratedTypeModel Type { get; }

      public ModulePathUse(string generatedNamespace, UdonApiGeneratedTypeModel type)
      {
        Namespace = generatedNamespace;
        Type = type;
      }
    }

    public const string ReportFileName = "generation_report.json";
    public const string SkippedMembersFileName = "skipped_members.txt";
    public const string DefaultRelativeOutputDirectory =
        "Packages/com.skytomo221.sobakasu/GeneratedUdonApiStubs~";

    private readonly UdonApiDiscovery _discovery;
    private readonly SobakasuStubRenderer _renderer;
    private readonly UdonApiStubGenerationPolicy _policy;
    private readonly UdonApiStubGenerationConfig _configuration;
    private readonly string _configurationPath;

    public static string DefaultOutputDirectory => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), DefaultRelativeOutputDirectory));

    public UdonApiStubGenerator(
        UdonApiDiscovery discovery,
        SobakasuStubRenderer renderer,
        UdonApiStubGenerationConfig configuration = null,
        string configurationPath = null)
    {
      _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
      _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
      _policy = new UdonApiStubGenerationPolicy();
      _configuration = configuration ?? UdonApiStubGenerationConfig.CreateDefault();
      _configurationPath = string.IsNullOrWhiteSpace(configurationPath)
          ? string.Empty
          : Path.GetFullPath(configurationPath);
    }

    public static UdonApiStubGenerator CreateDefault(string configurationPath = null)
    {
      var cache = UdonExposedNodeCache.Default;
      var typeFormatter = new UdonApiStubTypeFormatter(
          SobakasuBuiltInEnvironment.Default.ExternCatalog);
      var configuration = UdonApiStubGenerationConfig.Load(configurationPath);
      return new UdonApiStubGenerator(
          new UdonApiDiscovery(
              new InstalledUdonApiExposure(cache),
              typeFormatter),
          new SobakasuStubRenderer(typeFormatter),
          configuration,
          configurationPath);
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
      var generatedModel = _policy.Apply(
          model,
          _configuration,
          _configurationPath);
      RejectDuplicateDeclarations(generatedModel);
      PlanOutputPaths(generatedModel);

      var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
      var modules = new SortedDictionary<string, ModulePlan>(StringComparer.Ordinal);
      foreach (var type in generatedModel.Types)
      {
        if (!type.IsGenerated)
          continue;
        EnsureModuleAndAncestors(modules, type.GeneratedNamespace);
        modules[type.GeneratedNamespace].TypeModules.Add(type.ModuleName, type);
        files.Add(type.RelativePath, _renderer.RenderType(type));
      }
      var rootModuleNames = new HashSet<string>(StringComparer.Ordinal);
      foreach (var moduleName in modules.Keys)
      {
        if (moduleName.IndexOf('.') < 0)
          rootModuleNames.Add(moduleName);
      }
      foreach (var module in modules)
      {
        var relativePath = module.Key.Replace('.', '/') + ".sobakasu";
        var typeModules = new List<UdonApiGeneratedTypeModel>(
            module.Value.TypeModules.Values);
        files.Add(
            relativePath,
            _renderer.RenderNamespaceModule(
                new List<string>(module.Value.Children),
                typeModules,
                rootModuleNames));
      }

      var report = CreateReport(generatedModel);
      files.Add(ReportFileName, RenderReportJson(report));
      files.Add(SkippedMembersFileName, RenderSkippedMembers(report));
      return new UdonApiGenerationResult(files, report);
    }

    private static void EnsureModuleAndAncestors(
        IDictionary<string, ModulePlan> modules,
        string generatedNamespace)
    {
      var segments = generatedNamespace.Split('.');
      var current = string.Empty;
      for (var index = 0; index < segments.Length; index++)
      {
        var parent = current;
        current = string.IsNullOrEmpty(current)
            ? segments[index]
            : $"{current}.{segments[index]}";
        if (!modules.ContainsKey(current))
          modules.Add(current, new ModulePlan());
        if (!string.IsNullOrEmpty(parent))
          modules[parent].Children.Add(segments[index]);
      }
    }

    private void RejectDuplicateDeclarations(UdonApiGeneratedModel model)
    {
      var implTypes = new Dictionary<string, List<UdonApiGeneratedTypeModel>>(
          StringComparer.Ordinal);
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated || type.Placement != UdonApiGeneratedPlacement.Impl)
          continue;
        var typeKey = $"{type.GeneratedNamespace}|{type.WrapperName}";
        if (!implTypes.TryGetValue(typeKey, out var typeGroup))
        {
          typeGroup = new List<UdonApiGeneratedTypeModel>();
          implTypes.Add(typeKey, typeGroup);
        }
        typeGroup.Add(type);
      }
      foreach (var pair in implTypes)
      {
        if (pair.Value.Count < 2)
          continue;
        foreach (var type in pair.Value)
        {
          type.SkipReason =
              $"Multiple CLR types map to the same Sobakasu impl declaration '{pair.Key}'.";
          type.SkipGeneratedMembers($"Declaring type was skipped: {type.SkipReason}");
        }
      }

      var declarations = new Dictionary<
          string,
          List<UdonApiGeneratedMemberModel>>(StringComparer.Ordinal);
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;
        foreach (var member in type.Members)
        {
          if (!member.IsGenerated)
            continue;

          var scope = type.Placement == UdonApiGeneratedPlacement.TopLevel
              ? $"{type.GeneratedNamespace}|top_level"
              : $"{type.GeneratedNamespace}|impl|{type.WrapperName}";
          var key = $"{scope}|{_renderer.GetDeclarationKey(type, member)}";
          if (!declarations.TryGetValue(key, out var group))
          {
            group = new List<UdonApiGeneratedMemberModel>();
            declarations.Add(key, group);
          }
          group.Add(member);
        }
      }

      foreach (var pair in declarations)
      {
        if (pair.Value.Count < 2)
          continue;

        foreach (var member in pair.Value)
        {
          member.SkipReason =
              $"Multiple CLR members map to the same Sobakasu declaration '{pair.Key}'.";
          member.HasDeclarationCollision = true;
        }
      }
    }

    private static void PlanOutputPaths(UdonApiGeneratedModel model)
    {
      foreach (var type in model.Types)
      {
        type.ModuleName = null;
        type.RelativePath = null;
      }

      RejectNamespacePathCollisions(model);

      var usesByTypePath = new Dictionary<
          string,
          List<UdonApiGeneratedTypeModel>>(StringComparer.OrdinalIgnoreCase);
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;

        type.ModuleName = SobakasuNameUtility.ToSnakeCase(type.WrapperName);
        if (string.IsNullOrEmpty(type.ModuleName) ||
            !SobakasuNameUtility.IsIdentifier(type.ModuleName))
        {
          SkipTypeForPathCollision(
              type,
              $"The generated type name '{type.WrapperName}' does not produce a valid module name.");
          continue;
        }

        var relativePath = GetTypeRelativePath(type);
        if (!usesByTypePath.TryGetValue(relativePath, out var uses))
        {
          uses = new List<UdonApiGeneratedTypeModel>();
          usesByTypePath.Add(relativePath, uses);
        }
        uses.Add(type);
      }

      foreach (var pair in usesByTypePath)
      {
        if (pair.Value.Count < 2)
          continue;
        foreach (var type in pair.Value)
        {
          SkipTypeForPathCollision(
              type,
              $"Multiple CLR types require the same generated type module path '{pair.Key}'.");
        }
      }

      var namespacePaths = CollectNamespacePaths(model.Types);
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;

        var relativePath = GetTypeRelativePath(type);
        if (namespacePaths.Contains(relativePath))
        {
          SkipTypeForPathCollision(
              type,
              $"The generated type module path collides with a namespace facade path: '{relativePath}'.");
          continue;
        }

        type.RelativePath = relativePath;
      }
    }

    private static void RejectNamespacePathCollisions(UdonApiGeneratedModel model)
    {
      var usesByPath = new Dictionary<string, List<ModulePathUse>>(
          StringComparer.OrdinalIgnoreCase);
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;

        var segments = type.GeneratedNamespace.Split('.');
        var moduleNamespace = string.Empty;
        for (var index = 0; index < segments.Length; index++)
        {
          moduleNamespace = string.IsNullOrEmpty(moduleNamespace)
              ? segments[index]
              : $"{moduleNamespace}.{segments[index]}";
          var modulePath = moduleNamespace.Replace('.', '/') + ".sobakasu";
          if (!usesByPath.TryGetValue(modulePath, out var uses))
          {
            uses = new List<ModulePathUse>();
            usesByPath.Add(modulePath, uses);
          }
          uses.Add(new ModulePathUse(moduleNamespace, type));
        }
      }

      foreach (var pair in usesByPath)
      {
        var firstNamespace = pair.Value[0].Namespace;
        var hasDifferentNamespace = false;
        foreach (var use in pair.Value)
        {
          if (!string.Equals(
                  use.Namespace,
                  firstNamespace,
                  StringComparison.Ordinal))
          {
            hasDifferentNamespace = true;
            break;
          }
        }
        if (!hasDifferentNamespace)
          continue;

        var skippedTypes = new HashSet<UdonApiGeneratedTypeModel>();
        foreach (var use in pair.Value)
        {
          if (!use.Type.IsGenerated || !skippedTypes.Add(use.Type))
            continue;
          SkipTypeForPathCollision(
              use.Type,
              $"The generated namespace path collides by case with another namespace: '{pair.Key}'.");
        }
      }
    }

    private static HashSet<string> CollectNamespacePaths(
        IReadOnlyList<UdonApiGeneratedTypeModel> types)
    {
      var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var type in types)
      {
        if (!type.IsGenerated)
          continue;
        var segments = type.GeneratedNamespace.Split('.');
        var current = string.Empty;
        foreach (var segment in segments)
        {
          current = string.IsNullOrEmpty(current)
              ? segment
              : $"{current}.{segment}";
          paths.Add(current.Replace('.', '/') + ".sobakasu");
        }
      }
      return paths;
    }

    private static string GetTypeRelativePath(UdonApiGeneratedTypeModel type)
    {
      return type.GeneratedNamespace.Replace('.', '/') + "/" +
          type.ModuleName + ".sobakasu";
    }

    private static void SkipTypeForPathCollision(
        UdonApiGeneratedTypeModel type,
        string reason)
    {
      type.RelativePath = null;
      type.SkipReason = reason;
      type.SkipGeneratedMembers($"Declaring type was skipped: {reason}");
    }

    private static UdonApiGenerationReport CreateReport(UdonApiGeneratedModel model)
    {
      var report = new UdonApiGenerationReport
      {
        configuration_path = model.ConfigurationPath,
        configuration_version = model.Configuration.version,
        namespace_rules_configured = model.Configuration.namespaces.Length
      };
      report.rules_configured =
          model.Configuration.namespaces.Length +
          model.Configuration.types.Length +
          model.Configuration.members.Length;
      CountConfiguredRules(model.Configuration, report);
      var generatedNamespaces = new HashSet<string>(StringComparer.Ordinal);
      foreach (var type in model.Types)
      {
        report.generated_types.Add(new UdonApiGeneratedTypeRecord
        {
          clr_declaring_type = type.Physical.QualifiedName,
          sobakasu_namespace = type.GeneratedNamespace,
          placement = type.Placement == UdonApiGeneratedPlacement.TopLevel
              ? "top_level"
              : "impl",
          generated_file = type.RelativePath ?? string.Empty
        });
        report.types_discovered++;
        if (type.IsGenerated)
        {
          report.types_generated++;
          generatedNamespaces.Add(type.GeneratedNamespace);
          if (type.Placement == UdonApiGeneratedPlacement.TopLevel)
            report.top_level_static_type_count++;
          else
            report.impl_type_count++;
        }
        else
        {
          report.types_skipped++;
          report.skipped_types.Add(new UdonApiSkipRecord
          {
            full_name = type.Physical.QualifiedName,
            declaring_type = type.Physical.QualifiedName,
            member_kind = "type",
            signature = type.Physical.QualifiedName,
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
            CountMemberPolicy(member, report);
          }
          else
          {
            report.members_skipped++;
            report.skipped_members.Add(new UdonApiSkipRecord
            {
              full_name = member.Physical.FullName,
              declaring_type = member.Physical.DeclaringTypeName,
              member_kind = ToSnakeCase(member.Physical.Kind.ToString()),
              signature = member.Physical.DisplaySignature,
              extern_signature = member.Physical.ExternSignature,
              reason = member.SkipReason ??
                  $"Declaring type was skipped: {type.SkipReason}"
            });
          }
          if (member.IsExplicitlyExcluded)
            report.explicit_exclusions++;
          if (member.HasDeclarationCollision)
            report.declaration_collisions++;
        }
      }
      report.namespaces_generated = CountGeneratedNamespaces(generatedNamespaces);

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

    private static int CountGeneratedNamespaces(IEnumerable<string> namespaces)
    {
      var generated = new HashSet<string>(StringComparer.Ordinal);
      foreach (var generatedNamespace in namespaces)
      {
        var segments = generatedNamespace.Split('.');
        var current = string.Empty;
        foreach (var segment in segments)
        {
          current = string.IsNullOrEmpty(current)
              ? segment
              : $"{current}.{segment}";
          generated.Add(current);
        }
      }
      return generated.Count;
    }

    private static void CountConfiguredRules(
        UdonApiStubGenerationConfig configuration,
        UdonApiGenerationReport report)
    {
      foreach (var rule in configuration.namespaces)
      {
        if (rule.MatchCount > 0)
        {
          report.rules_matched++;
          report.namespace_rules_matched++;
        }
        else
        {
          var identity = $"namespace:{rule.clr_namespace}";
          report.unmatched_rules.Add(identity);
          report.unmatched_namespace_rules.Add(identity);
        }
      }
      foreach (var rule in configuration.types)
      {
        if (rule.MatchCount > 0)
          report.rules_matched++;
        else
          report.unmatched_rules.Add($"type:{rule.type}");
      }
      foreach (var rule in configuration.members)
      {
        var identity =
            $"member:{rule.declaring_type}|{rule.member_kind}|{rule.member}(" +
            $"{string.Join(",", rule.parameter_types)})";
        if (rule.MatchCount > 0)
          report.rules_matched++;
        else
          report.unmatched_rules.Add(identity);
      }
    }

    private static void CountMemberPolicy(
        UdonApiGeneratedMemberModel member,
        UdonApiGenerationReport report)
    {
      var returnType = UdonApiStubGenerationPolicy.GetNormalReturnType(member.Physical);
      if (returnType != typeof(void))
      {
        if (member.ReturnProjection == UdonApiGeneratedProjection.Maybe)
          report.maybe_return_count++;
        else
          report.raw_return_count++;
      }

      var parameters = member.Physical.Callable?.GetParameters();
      if (parameters == null)
        return;
      for (var index = 0; index < parameters.Length; index++)
      {
        if (!parameters[index].IsOut)
          continue;
        if (member.GetOutProjection(index) == UdonApiGeneratedProjection.Maybe)
          report.maybe_out_count++;
        else
          report.raw_out_count++;
      }
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
      return NormalizeNewLines(JsonUtility.ToJson(report, true)) + "\n";
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
      var configurationPath = GetArgument("-udonApiStubConfig");
      var result = UdonApiStubGenerator.CreateDefault(configurationPath)
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
