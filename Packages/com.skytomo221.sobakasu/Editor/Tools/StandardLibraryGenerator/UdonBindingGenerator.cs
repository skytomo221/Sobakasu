using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEngine;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
  internal sealed class UdonBindingGenerationResult
  {
    public IReadOnlyDictionary<string, string> Files { get; }
    public IReadOnlyDictionary<string, string> Diagnostics { get; }
    public UdonApiGenerationReport Report { get; }

    public UdonBindingGenerationResult(
        IReadOnlyDictionary<string, string> files,
        IReadOnlyDictionary<string, string> diagnostics,
        UdonApiGenerationReport report)
    {
      Files = files ?? throw new ArgumentNullException(nameof(files));
      Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
      Report = report ?? throw new ArgumentNullException(nameof(report));
    }
  }

  internal sealed class UdonBindingGenerator
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
    private readonly UdonApiDiscovery _discovery;
    private readonly SobakasuBindingRenderer _renderer;
    private readonly UdonBindingGenerationPolicy _policy;
    private readonly UdonBindingGenerationConfig _configuration;
    private readonly string _configurationPath;
    private readonly bool _validateGeneratedBindings;

    public UdonBindingGenerator(
        UdonApiDiscovery discovery,
        SobakasuBindingRenderer renderer,
        UdonBindingGenerationConfig configuration = null,
        string configurationPath = null,
        bool validateGeneratedBindings = false)
    {
      _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
      _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
      _policy = new UdonBindingGenerationPolicy();
      _configuration = configuration ?? UdonBindingGenerationConfig.CreateDefault();
      _configurationPath = string.IsNullOrWhiteSpace(configurationPath)
          ? string.Empty
          : Path.GetFullPath(configurationPath);
      _validateGeneratedBindings = validateGeneratedBindings;
    }

    public static UdonBindingGenerator CreateDefault(string configurationPath = null)
    {
      var cache = UdonExposedNodeCache.Default;
      var typeFormatter = new UdonBindingTypeFormatter(
          SobakasuBuiltInEnvironment.Default.ExternCatalog);
      var configuration = UdonBindingGenerationConfig.Load(configurationPath);
      return new UdonBindingGenerator(
          new UdonApiDiscovery(
              new InstalledUdonApiExposure(cache),
              typeFormatter),
          new SobakasuBindingRenderer(typeFormatter),
          configuration,
          configurationPath,
          validateGeneratedBindings: true);
    }

    public UdonBindingGenerationResult Generate()
    {
      return Generate(_discovery.Discover());
    }

    internal UdonBindingGenerationResult Generate(IReadOnlyList<Type> candidateTypes)
    {
      return Generate(_discovery.Discover(candidateTypes));
    }

    private UdonBindingGenerationResult Generate(UdonApiModel model)
    {
      var generatedModel = _policy.Apply(
          model,
          _configuration,
          _configurationPath);
      if (_validateGeneratedBindings)
        RejectUnbindableDeclarations(generatedModel);
      RejectDuplicateDeclarations(generatedModel);
      PlanOutputPaths(generatedModel);

      var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
      var modules = new SortedDictionary<string, ModulePlan>(StringComparer.Ordinal);
      foreach (var type in generatedModel.Types)
      {
        if (!type.IsGenerated)
          continue;
        if (!string.IsNullOrEmpty(type.GeneratedNamespace))
        {
          EnsureModuleAndAncestors(modules, type.GeneratedNamespace);
          modules[type.GeneratedNamespace].TypeModules.Add(type.ModuleName, type);
        }
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
      var diagnostics = new SortedDictionary<string, string>(StringComparer.Ordinal)
      {
        [ReportFileName] = RenderReportJson(report),
        [SkippedMembersFileName] = RenderSkippedMembers(report)
      };
      return new UdonBindingGenerationResult(files, diagnostics, report);
    }

    private void RejectUnbindableDeclarations(UdonApiGeneratedModel model)
    {
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;

        foreach (var member in type.Members)
        {
          if (!member.IsGenerated)
            continue;
          if (TryGetUnsupportedDeclarationReason(member, out var reason))
          {
            member.SkipReason = reason;
            continue;
          }
          if (!RequiresCompilerValidation(member))
            continue;
          if (!TryValidateDeclaration(type, member, out reason))
            member.SkipReason = reason;
        }
      }
    }

    private static bool TryGetUnsupportedDeclarationReason(
        UdonApiGeneratedMemberModel member,
        out string reason)
    {
      if (member.Physical.Callable is System.Reflection.MethodInfo method &&
          method.IsSpecialName &&
          method.Name.StartsWith("op_", StringComparison.Ordinal))
      {
        reason =
            "Operator members cannot be represented as named declarative extern " +
            "bindings by the current Sobakasu compiler.";
        return true;
      }

      if ((member.Physical.Kind == UdonApiMemberKind.PropertyGetter ||
           member.Physical.Kind == UdonApiMemberKind.PropertySetter ||
           member.Physical.Kind == UdonApiMemberKind.FieldGetter ||
           member.Physical.Kind == UdonApiMemberKind.FieldSetter) &&
          !SobakasuNameUtility.IsIdentifier(member.Physical.MemberName))
      {
        reason =
            $"External member name '{member.Physical.MemberName}' cannot be " +
            "represented by the current Sobakasu member-access syntax.";
        return true;
      }

      reason = string.Empty;
      return false;
    }

    private static bool RequiresCompilerValidation(
        UdonApiGeneratedMemberModel member)
    {
      if (member.Physical.Kind == UdonApiMemberKind.FieldGetter ||
          member.Physical.Kind == UdonApiMemberKind.FieldSetter)
      {
        return true;
      }

      var callable = member.Physical.Callable;
      if (callable == null)
        return false;
      foreach (var parameter in callable.GetParameters())
      {
        if (parameter.ParameterType.IsByRef ||
            ContainsArrayType(parameter.ParameterType))
        {
          return true;
        }
      }

      return callable is System.Reflection.MethodInfo method &&
          ContainsArrayType(method.ReturnType);
    }

    private static bool ContainsArrayType(Type type)
    {
      while (type != null && type.IsByRef)
        type = type.GetElementType();
      return type?.IsArray == true;
    }

    private bool TryValidateDeclaration(
        UdonApiGeneratedTypeModel type,
        UdonApiGeneratedMemberModel member,
        out string reason)
    {
      var validationType = new UdonApiGeneratedTypeModel(type.Physical)
      {
        GeneratedNamespace = type.GeneratedNamespace,
        Placement = type.Placement,
        WrapperName = type.WrapperName
      };
      validationType.AddMember(member);

      var source =
          "enum Maybe<T> {\n  Nothing,\n  Just(T),\n}\n\n" +
          _renderer.RenderType(validationType, includeMaybeImport: false);
      var parser = new SobakasuParser(SourceText.From(source));
      var syntax = parser.ParseCompilationUnit();
      if (TryGetFirstError(parser.Diagnostics.Diagnostics, out var diagnostic))
      {
        reason = FormatValidationFailure("parser", diagnostic);
        return false;
      }

      var binder = new SobakasuBinder();
      binder.BindProgram(syntax);
      if (TryGetFirstError(binder.Diagnostics.Diagnostics, out diagnostic))
      {
        reason = FormatValidationFailure("binder", diagnostic);
        return false;
      }

      reason = string.Empty;
      return true;
    }

    private static bool TryGetFirstError(
        IReadOnlyList<Diagnostic> diagnostics,
        out Diagnostic error)
    {
      foreach (var diagnostic in diagnostics)
      {
        if (diagnostic.Severity != DiagnosticSeverity.Error)
          continue;
        error = diagnostic;
        return true;
      }

      error = default;
      return false;
    }

    private static string FormatValidationFailure(
        string phase,
        Diagnostic diagnostic)
    {
      return
          $"Generated declaration is not supported by the current Sobakasu {phase}: " +
          $"{diagnostic.Code}: {diagnostic.Message}";
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
        if (type.Placement == UdonApiGeneratedPlacement.Impl &&
            string.Equals(
                type.ModuleName,
                type.WrapperName,
                StringComparison.OrdinalIgnoreCase))
        {
          type.ModuleName += "_binding";
        }
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
        if (string.IsNullOrEmpty(type.GeneratedNamespace))
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
        if (string.IsNullOrEmpty(type.GeneratedNamespace))
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
      var fileName = type.ModuleName + ".sobakasu";
      return string.IsNullOrEmpty(type.GeneratedNamespace)
          ? fileName
          : type.GeneratedNamespace.Replace('.', '/') + "/" + fileName;
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
      var physicalApis = new SortedDictionary<string, UdonApiPhysicalRecord>(
          StringComparer.Ordinal);
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
            surface_type = type.Physical.QualifiedName,
            clr_declaring_type = type.Physical.QualifiedName,
            member_kind = "type",
            signature = type.Physical.QualifiedName,
            extern_signature = string.Empty,
            reason = type.SkipReason,
            reasons = new List<string> { type.SkipReason }
          });
        }

        foreach (var member in type.Members)
        {
          var isGenerated = type.IsGenerated && member.IsGenerated;
          var failureReason = isGenerated
              ? null
              : member.SkipReason ??
                  $"Declaring type was skipped: {type.SkipReason}";
          report.members_discovered++;
          if (isGenerated)
          {
            report.members_generated++;
            CountMemberPolicy(member, report);
          }
          else
          {
            report.members_skipped++;
          }

          if (string.IsNullOrEmpty(member.Physical.ExternSignature))
          {
            if (!isGenerated)
            {
              report.skipped_members.Add(CreateSurfaceOnlySkipRecord(
                  member.Physical,
                  failureReason));
            }
          }
          else
          {
            AddPhysicalApiSurface(
                physicalApis,
                member.Physical,
                isGenerated,
                failureReason);
          }
          if (member.IsExplicitlyExcluded)
            report.explicit_exclusions++;
          if (member.HasDeclarationCollision)
            report.declaration_collisions++;
        }
      }
      report.namespaces_generated = CountGeneratedNamespaces(generatedNamespaces);
      PopulatePhysicalApiReport(model, physicalApis, report);
      report.member_surfaces_discovered = report.members_discovered;
      report.member_surfaces_generated = report.members_generated;
      report.member_surfaces_skipped = report.members_skipped;

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

    private static void AddPhysicalApiSurface(
        IDictionary<string, UdonApiPhysicalRecord> physicalApis,
        UdonApiMemberModel member,
        bool isGenerated,
        string failureReason)
    {
      if (!physicalApis.TryGetValue(member.ExternSignature, out var physical))
      {
        physical = new UdonApiPhysicalRecord
        {
          extern_signature = member.ExternSignature,
          physical_full_name = member.PhysicalFullName,
          clr_declaring_type = member.ClrDeclaringTypeName,
          member_kind = ToSnakeCase(member.Kind.ToString()),
          signature = member.DisplaySignature
        };
        physicalApis.Add(member.ExternSignature, physical);
      }

      AddUnique(physical.surface_types, member.SurfaceTypeName);
      physical.is_udon_exposed |= member.IsUdonExposed;
      if (isGenerated)
      {
        AddUnique(physical.generated_surface_types, member.SurfaceTypeName);
        return;
      }

      failureReason ??= string.Empty;
      AddUnique(physical.reasons, failureReason);
      AddUniqueSurfaceFailure(
          physical.surface_failures,
          member.SurfaceTypeName,
          failureReason);
    }

    private static UdonApiSkipRecord CreateSurfaceOnlySkipRecord(
        UdonApiMemberModel member,
        string reason)
    {
      reason ??= string.Empty;
      return new UdonApiSkipRecord
      {
        full_name = member.SurfaceFullName,
        declaring_type = member.ClrDeclaringTypeName,
        surface_type = member.SurfaceTypeName,
        clr_declaring_type = member.ClrDeclaringTypeName,
        member_kind = ToSnakeCase(member.Kind.ToString()),
        signature = member.DisplaySignature,
        extern_signature = string.Empty,
        reason = reason,
        is_udon_exposed = false,
        surface_types = new List<string> { member.SurfaceTypeName },
        reasons = new List<string> { reason },
        surface_failures = new List<UdonApiSurfaceFailureRecord>
        {
          new()
          {
            surface_type = member.SurfaceTypeName,
            reason = reason
          }
        }
      };
    }

    private static void PopulatePhysicalApiReport(
        UdonApiGeneratedModel model,
        SortedDictionary<string, UdonApiPhysicalRecord> physicalApis,
        UdonApiGenerationReport report)
    {
      report.udon_signatures_discovered = physicalApis.Count;
      foreach (var physical in physicalApis.Values)
      {
        physical.surface_types.Sort(StringComparer.Ordinal);
        physical.generated_surface_types.Sort(StringComparer.Ordinal);
        physical.reasons.Sort(StringComparer.Ordinal);
        physical.surface_failures.Sort(CompareSurfaceFailures);
        physical.is_covered =
            physical.is_udon_exposed && physical.generated_surface_types.Count > 0;
        report.udon_api.Add(physical);

        if (physical.is_udon_exposed)
        {
          report.udon_signatures_exposed++;
          if (physical.is_covered)
            report.udon_signatures_covered++;
          else
            report.udon_signatures_unsupported++;
        }

        if (physical.surface_failures.Count > 0)
          report.skipped_members.Add(CreatePhysicalSkipRecord(physical));
      }

      var unmatched = new SortedSet<string>(StringComparer.Ordinal);
      foreach (var signature in model.UdonExposedSignatures)
      {
        if (!string.IsNullOrEmpty(signature) && !physicalApis.ContainsKey(signature))
          unmatched.Add(signature);
      }
      report.udon_exposed_unmatched_signatures.AddRange(unmatched);
      report.udon_exposed_unmatched_signatures_count = unmatched.Count;

      if (report.udon_signatures_exposed !=
          report.udon_signatures_covered + report.udon_signatures_unsupported)
      {
        throw new InvalidOperationException(
            "Udon physical API coverage invariant was violated.");
      }

      report.udon_api_coverage_percent = report.udon_signatures_exposed == 0
          ? 0.0
          : report.udon_signatures_covered * 100.0 /
              report.udon_signatures_exposed;
    }

    private static UdonApiSkipRecord CreatePhysicalSkipRecord(
        UdonApiPhysicalRecord physical)
    {
      return new UdonApiSkipRecord
      {
        full_name = physical.physical_full_name,
        declaring_type = physical.clr_declaring_type,
        surface_type = string.Empty,
        clr_declaring_type = physical.clr_declaring_type,
        member_kind = physical.member_kind,
        signature = physical.signature,
        extern_signature = physical.extern_signature,
        reason = string.Join(" | ", physical.reasons),
        is_udon_exposed = physical.is_udon_exposed,
        surface_types = new List<string>(physical.surface_types),
        generated_surface_types = new List<string>(
            physical.generated_surface_types),
        reasons = new List<string>(physical.reasons),
        surface_failures = new List<UdonApiSurfaceFailureRecord>(
            physical.surface_failures)
      };
    }

    private static void AddUniqueSurfaceFailure(
        ICollection<UdonApiSurfaceFailureRecord> failures,
        string surfaceType,
        string reason)
    {
      foreach (var failure in failures)
      {
        if (string.Equals(failure.surface_type, surfaceType, StringComparison.Ordinal) &&
            string.Equals(failure.reason, reason, StringComparison.Ordinal))
        {
          return;
        }
      }

      failures.Add(new UdonApiSurfaceFailureRecord
      {
        surface_type = surfaceType,
        reason = reason
      });
    }

    private static int CompareSurfaceFailures(
        UdonApiSurfaceFailureRecord left,
        UdonApiSurfaceFailureRecord right)
    {
      var surfaceComparison = string.CompareOrdinal(
          left.surface_type,
          right.surface_type);
      return surfaceComparison != 0
          ? surfaceComparison
          : string.CompareOrdinal(left.reason, right.reason);
    }

    private static void AddUnique(ICollection<string> values, string value)
    {
      if (!values.Contains(value))
        values.Add(value);
    }

    private static int CountGeneratedNamespaces(IEnumerable<string> namespaces)
    {
      var generated = new HashSet<string>(StringComparer.Ordinal);
      foreach (var generatedNamespace in namespaces)
      {
        if (string.IsNullOrEmpty(generatedNamespace))
          continue;
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
        UdonBindingGenerationConfig configuration,
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
      var returnType = UdonBindingGenerationPolicy.GetNormalReturnType(member.Physical);
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
      var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
      foreach (var skippedType in report.skipped_types)
        Increment(typeCounts, skippedType.reason);

      var surfaceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
      foreach (var skippedMember in report.skipped_members)
      {
        foreach (var failure in skippedMember.surface_failures)
          Increment(surfaceCounts, failure.reason);
      }

      var unsupportedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
      foreach (var physical in report.udon_api)
      {
        if (!physical.is_udon_exposed || physical.is_covered)
          continue;
        if (physical.reasons.Count == 0)
        {
          Increment(unsupportedCounts, string.Empty);
          continue;
        }
        foreach (var reason in physical.reasons)
          Increment(unsupportedCounts, reason);
      }

      report.type_skip_reasons.AddRange(CreateReasonCounts(typeCounts));
      report.surface_skip_reasons.AddRange(CreateReasonCounts(surfaceCounts));
      report.udon_unsupported_reasons.AddRange(
          CreateReasonCounts(unsupportedCounts));

      var legacyCounts = new Dictionary<string, int>(typeCounts, StringComparer.Ordinal);
      foreach (var pair in surfaceCounts)
      {
        legacyCounts.TryGetValue(pair.Key, out var count);
        legacyCounts[pair.Key] = count + pair.Value;
      }
      report.skip_reasons.AddRange(CreateReasonCounts(legacyCounts));
    }

    private static List<UdonApiSkipReasonCount> CreateReasonCounts(
        IReadOnlyDictionary<string, int> counts)
    {
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
      return reasons;
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
        text.Append(string.Join(",", member.surface_types));
        text.Append("\t");
        text.Append(member.signature);
        text.Append("\t");
        text.Append(member.extern_signature);
        text.Append("\t");
        text.AppendLine(string.Join(" | ", member.reasons));
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

}
