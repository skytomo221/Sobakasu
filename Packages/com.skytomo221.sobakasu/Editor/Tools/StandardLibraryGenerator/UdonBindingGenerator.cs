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
      MarkAmbiguousExternCalls(generatedModel);
      if (_validateGeneratedBindings)
        RejectUnbindableDeclarations(generatedModel);
      PlanOutputPaths(generatedModel);
      RejectDuplicateDeclarations(generatedModel);
      var preludeReExports = ResolvePrelude(generatedModel);

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
      if (preludeReExports.Count > 0)
      {
        if (files.ContainsKey("prelude.sobakasu"))
        {
          throw new UdonBindingConfigurationException(
              "Generated prelude re-exports collide with another generated prelude.sobakasu file.");
        }
        files.Add("prelude.sobakasu", _renderer.RenderPrelude(preludeReExports));
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
      if (member.RequiresExplicitAbiSignature)
        return true;
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

    private static void MarkAmbiguousExternCalls(UdonApiGeneratedModel model)
    {
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;
        var groups = new Dictionary<
            string,
            List<UdonApiGeneratedMemberModel>>(StringComparer.Ordinal);
        foreach (var member in type.Members)
        {
          if (!member.IsGenerated || member.Physical.Callable == null)
            continue;
          var key = GetExternInputKey(member.Physical);
          if (!groups.TryGetValue(key, out var group))
          {
            group = new List<UdonApiGeneratedMemberModel>();
            groups.Add(key, group);
          }
          group.Add(member);
        }
        foreach (var group in groups.Values)
        {
          if (group.Count < 2)
            continue;
          foreach (var member in group)
            member.RequiresExplicitAbiSignature = true;
        }
      }
    }

    private static string GetExternInputKey(UdonApiMemberModel member)
    {
      var callable = member.Callable;
      var inputTypes = new List<string>();
      foreach (var parameter in callable.GetParameters())
      {
        if (parameter.IsOut)
          continue;
        var type = parameter.ParameterType;
        if (type.IsByRef)
          type = type.GetElementType();
        inputTypes.Add(ClrMemberId.GetClrTypeName(type));
      }
      var callableName = callable.IsConstructor
          ? ".ctor"
          : callable.Name;
      var staticKind = callable.IsStatic ? "static" : "instance";
      return $"{staticKind}|{callableName}|{string.Join(",", inputTypes)}";
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
        WrapperName = type.WrapperName,
        LanguageItem = type.LanguageItem
      };
      validationType.AddMember(member);

      var source =
          "lang \"maybe\"\nenum Maybe<T> {\n  Nothing,\n  Just(T),\n}\n\n" +
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

    private static IReadOnlyList<string> ResolvePrelude(
        UdonApiGeneratedModel model)
    {
      var namespaceSymbols = new Dictionary<
          string,
          Dictionary<string, string>>(StringComparer.Ordinal);
      var typePaths = new HashSet<string>(StringComparer.Ordinal);
      var memberPaths = new HashSet<string>(StringComparer.Ordinal);
      var errors = new SortedSet<string>(StringComparer.Ordinal);

      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;
        AddNamespaceHierarchy(
            namespaceSymbols,
            type.GeneratedNamespace,
            errors);
        if (type.Placement == UdonApiGeneratedPlacement.Impl)
        {
          var implModulePath = JoinPath(type.GeneratedNamespace, type.ModuleName);
          var typePath = JoinPath(implModulePath, type.WrapperName);
          typePaths.Add(typePath);
          AddGeneratedSymbol(
              namespaceSymbols,
              type.GeneratedNamespace,
              type.ModuleName,
              implModulePath,
              errors);
          AddGeneratedSymbol(
              namespaceSymbols,
              type.GeneratedNamespace,
              type.WrapperName,
              typePath,
              errors);
          continue;
        }

        var modulePath = JoinPath(type.GeneratedNamespace, type.ModuleName);
        AddGeneratedSymbol(
            namespaceSymbols,
            type.GeneratedNamespace,
            type.ModuleName,
            modulePath,
            errors);
        foreach (var member in type.Members)
        {
          if (member.IsGenerated)
            memberPaths.Add(JoinPath(modulePath, member.FunctionName));
        }
      }

      var exports = new List<string>();
      var publicSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var path in model.Configuration.prelude.namespaces)
      {
        var identity = UdonBindingGenerationPolicy.PreludeNamespaceIdentity(path);
        if (!namespaceSymbols.TryGetValue(path, out var symbols))
        {
          errors.Add($"Prelude namespace target '{path}' does not exist.");
          continue;
        }
        model.Configuration.MarkRuleMatched(identity);
        foreach (var symbol in symbols)
          AddPreludeSymbol(publicSymbols, symbol.Key, symbol.Value, errors);
        exports.Add(path + ".*");
      }
      foreach (var path in model.Configuration.prelude.types)
      {
        var identity = UdonBindingGenerationPolicy.PreludeTypeIdentity(path);
        if (!typePaths.Contains(path))
        {
          errors.Add($"Prelude type target '{path}' does not exist.");
          continue;
        }
        model.Configuration.MarkRuleMatched(identity);
        AddPreludeSymbol(publicSymbols, GetLeafName(path), path, errors);
        exports.Add(path);
      }
      foreach (var path in model.Configuration.prelude.members)
      {
        var identity = UdonBindingGenerationPolicy.PreludeMemberIdentity(path);
        if (!memberPaths.Contains(path))
        {
          errors.Add($"Prelude member target '{path}' does not exist.");
          continue;
        }
        model.Configuration.MarkRuleMatched(identity);
        AddPreludeSymbol(publicSymbols, GetLeafName(path), path, errors);
        exports.Add(path);
      }

      foreach (var identity in UdonBindingGenerationPolicy.GetConfiguredRuleIdentities(
          model.Configuration))
      {
        if (identity.StartsWith("prelude.", StringComparison.Ordinal) &&
            model.Configuration.GetRuleMatchCount(identity) == 0)
        {
          errors.Add($"Configuration rule '{identity}' did not match a generated Sobakasu path.");
        }
      }
      ThrowGenerationErrors(errors);
      return exports;
    }

    private static void AddNamespaceHierarchy(
        IDictionary<string, Dictionary<string, string>> namespaces,
        string generatedNamespace,
        ISet<string> errors)
    {
      if (string.IsNullOrEmpty(generatedNamespace))
        return;
      var current = string.Empty;
      foreach (var segment in generatedNamespace.Split('.'))
      {
        var parent = current;
        current = JoinPath(current, segment);
        if (!namespaces.ContainsKey(current))
        {
          namespaces.Add(
              current,
              new Dictionary<string, string>(StringComparer.Ordinal));
        }
        if (!string.IsNullOrEmpty(parent))
          AddGeneratedSymbol(namespaces, parent, segment, current, errors);
      }
    }

    private static void AddGeneratedSymbol(
        IDictionary<string, Dictionary<string, string>> namespaces,
        string generatedNamespace,
        string symbol,
        string source,
        ISet<string> errors)
    {
      if (string.IsNullOrEmpty(generatedNamespace))
        return;
      if (!namespaces.TryGetValue(generatedNamespace, out var symbols))
      {
        symbols = new Dictionary<string, string>(StringComparer.Ordinal);
        namespaces.Add(generatedNamespace, symbols);
      }
      if (symbols.TryGetValue(symbol, out var existing) &&
          !string.Equals(existing, source, StringComparison.Ordinal))
      {
        errors.Add(
            $"Generated Sobakasu symbol '{generatedNamespace}.{symbol}' " +
            $"collides between '{existing}' and '{source}'.");
        return;
      }
      symbols[symbol] = source;
    }

    private static void AddPreludeSymbol(
        IDictionary<string, string> symbols,
        string symbol,
        string source,
        ISet<string> errors)
    {
      if (symbols.TryGetValue(symbol, out var existing))
      {
        errors.Add(
            $"Prelude symbol '{symbol}' collides between '{existing}' and '{source}'.");
        return;
      }
      symbols[symbol] = source;
    }

    private static string JoinPath(string prefix, string leaf)
    {
      return string.IsNullOrEmpty(prefix) ? leaf : $"{prefix}.{leaf}";
    }

    private static string GetLeafName(string path)
    {
      var separator = path.LastIndexOf('.');
      return separator < 0 ? path : path.Substring(separator + 1);
    }

    private static void ThrowGenerationErrors(IReadOnlyCollection<string> errors)
    {
      if (errors.Count == 0)
        return;
      throw new UdonBindingConfigurationException(
          "Udon binding generation validation failed:\n- " +
          string.Join("\n- ", errors));
    }

    private void RejectDuplicateDeclarations(UdonApiGeneratedModel model)
    {
      var errors = new SortedSet<string>(StringComparer.Ordinal);
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
        errors.Add(
            $"Multiple CLR types map to the same Sobakasu impl declaration '{pair.Key}': " +
            string.Join(", ", pair.Value.ConvertAll(
                value => value.Physical.QualifiedName)) + ".");
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
              ? $"{type.GeneratedNamespace}|module|{type.ModuleName}"
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
        var sources = new List<string>();
        foreach (var member in pair.Value)
          sources.Add(ClrMemberId.Format(member.Physical));
        errors.Add(
            $"Multiple CLR members map to the same Sobakasu declaration '{pair.Key}': " +
            string.Join(", ", sources) + ".");
      }
      ThrowGenerationErrors(errors);
    }

    private static void PlanOutputPaths(UdonApiGeneratedModel model)
    {
      var errors = new SortedSet<string>(StringComparer.Ordinal);
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
        var sources = new List<string>();
        foreach (var type in pair.Value)
          sources.Add(type.Physical.QualifiedName);
        errors.Add(
            $"Multiple CLR types require generated module path '{pair.Key}': " +
            string.Join(", ", sources) + ".");
      }

      var namespacePaths = CollectNamespacePaths(model.Types);
      foreach (var type in model.Types)
      {
        if (!type.IsGenerated)
          continue;

        var relativePath = GetTypeRelativePath(type);
        if (namespacePaths.TryGetValue(relativePath, out var namespacePath))
        {
          errors.Add(string.Equals(
                  relativePath,
                  namespacePath,
                  StringComparison.Ordinal)
              ? $"Generated type module path '{relativePath}' collides with a namespace facade path."
              : $"Generated type module path '{relativePath}' collides by case with " +
                $"namespace facade path '{namespacePath}'.");
          continue;
        }

        type.RelativePath = relativePath;
      }
      ThrowGenerationErrors(errors);
    }

    private static void RejectNamespacePathCollisions(UdonApiGeneratedModel model)
    {
      var errors = new SortedSet<string>(StringComparer.Ordinal);
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

        var namespaces = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var use in pair.Value)
          namespaces.Add(use.Namespace);
        errors.Add(
            $"Generated namespace path '{pair.Key}' collides by case between: " +
            string.Join(", ", namespaces) + ".");
      }
      ThrowGenerationErrors(errors);
    }

    private static Dictionary<string, string> CollectNamespacePaths(
        IReadOnlyList<UdonApiGeneratedTypeModel> types)
    {
      var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
          var path = current.Replace('.', '/') + ".sobakasu";
          if (!paths.ContainsKey(path))
            paths.Add(path, path);
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
        namespace_rules_configured = model.Configuration.renames.namespaces.Length
      };
      report.rules_configured =
          UdonBindingGenerationPolicy.GetConfiguredRuleIdentities(
              model.Configuration).Count;
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
      foreach (var identity in UdonBindingGenerationPolicy.GetConfiguredRuleIdentities(
          configuration))
      {
        var matched = configuration.GetRuleMatchCount(identity) > 0;
        if (matched)
        {
          report.rules_matched++;
        }
        else
        {
          report.unmatched_rules.Add(identity);
        }
        if (!identity.StartsWith("rename.namespace:", StringComparison.Ordinal))
          continue;
        if (matched)
          report.namespace_rules_matched++;
        else
          report.unmatched_namespace_rules.Add(identity);
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
