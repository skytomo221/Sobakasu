using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using UnityEngine;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
  [Serializable]
  internal sealed class StandardLibraryManifest
  {
    public StandardLibraryManifestEntry[] modules;
  }

  [Serializable]
  internal sealed class StandardLibraryManifestEntry
  {
    public string name;
    public string path;
  }

  internal sealed class ResolvedUseDirective
  {
    public UseDirectiveSyntax Syntax { get; }
    public StandardLibraryModule TargetModule { get; }
    public string DeclarationName { get; }
    public string IntroducedName { get; }

    public ResolvedUseDirective(
        UseDirectiveSyntax syntax,
        StandardLibraryModule targetModule,
        string declarationName,
        string introducedName)
    {
      Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
      TargetModule = targetModule ?? throw new ArgumentNullException(nameof(targetModule));
      DeclarationName = declarationName ?? string.Empty;
      IntroducedName = introducedName ?? declarationName ?? string.Empty;
    }
  }

  internal sealed class StandardLibraryModule
  {
    private readonly List<ResolvedUseDirective> _imports = new();

    public string LogicalName { get; }
    public string SourcePath { get; }
    public SourceText SourceText { get; }
    public CompilationUnitSyntax Syntax { get; }
    public bool IsEntry { get; }
    public bool IsStandardLibrary => !IsEntry;
    public IReadOnlyList<ResolvedUseDirective> Imports => _imports;

    public StandardLibraryModule(
        string logicalName,
        string sourcePath,
        SourceText sourceText,
        CompilationUnitSyntax syntax,
        bool isEntry)
    {
      LogicalName = logicalName ?? string.Empty;
      SourcePath = sourcePath ?? string.Empty;
      SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
      Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
      IsEntry = isEntry;
    }

    public void AddImport(ResolvedUseDirective import)
    {
      _imports.Add(import ?? throw new ArgumentNullException(nameof(import)));
    }
  }

  internal sealed class StandardLibraryModuleGraph
  {
    public StandardLibraryModule EntryModule { get; }
    public IReadOnlyList<StandardLibraryModule> Modules { get; }

    public StandardLibraryModuleGraph(
        StandardLibraryModule entryModule,
        IReadOnlyList<StandardLibraryModule> modules)
    {
      EntryModule = entryModule ?? throw new ArgumentNullException(nameof(entryModule));
      Modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    public static StandardLibraryModuleGraph CreateSingle(
        CompilationUnitSyntax syntax,
        SourceText sourceText = null,
        string sourcePath = "<entry>")
    {
      var entry = new StandardLibraryModule(
          string.Empty,
          sourcePath,
          sourceText ?? SourceText.From(string.Empty),
          syntax,
          isEntry: true);
      return new StandardLibraryModuleGraph(entry, new[] { entry });
    }
  }

  internal sealed class StandardLibraryResolution
  {
    public StandardLibraryModuleGraph Graph { get; }
    public DiagnosticBag Diagnostics { get; }

    public StandardLibraryResolution(
        StandardLibraryModuleGraph graph,
        DiagnosticBag diagnostics)
    {
      Graph = graph ?? throw new ArgumentNullException(nameof(graph));
      Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }
  }

  internal sealed class StandardLibraryResolver
  {
    public const string ManifestFileName = "manifest.json";
    public static readonly string DefaultRoot = Path.Combine(
        "Packages",
        "com.skytomo221.sobakasu",
        "StandardLibrary~");

    private static readonly object CacheGate = new();
    private static readonly Dictionary<string, ParsedSourceCacheEntry> ParsedSourceCache =
        new(StringComparer.OrdinalIgnoreCase);

    private DiagnosticBag _diagnostics = new();
    private readonly Dictionary<string, ManifestModule> _manifestModules =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, StandardLibraryModule> _loadedModules =
        new(StringComparer.Ordinal);
    private readonly List<StandardLibraryModule> _moduleOrder = new();
    private readonly Dictionary<string, int> _visitStates =
        new(StringComparer.Ordinal);
    private readonly List<string> _visitStack = new();
    private string _rootPath;

    public StandardLibraryResolution Resolve(
        string entrySource,
        string rootPath = null,
        string entryPath = "<entry>")
    {
      Reset();
      var entryText = SourceText.From(entrySource ?? string.Empty);
      var entrySyntax = ParseSource(entryText, entryPath);
      var entryModule = new StandardLibraryModule(
          string.Empty,
          entryPath,
          entryText,
          entrySyntax,
          isEntry: true);
      _moduleOrder.Add(entryModule);

      var entryUses = GetUseDirectives(entrySyntax);
      if (entryUses.Count == 0)
      {
        return new StandardLibraryResolution(
            new StandardLibraryModuleGraph(entryModule, _moduleOrder.ToArray()),
            _diagnostics);
      }

      try
      {
        _rootPath = Path.GetFullPath(rootPath ?? DefaultRoot);
      }
      catch (Exception ex)
      {
        Report(
            "SBK4001",
            new TextSpan(0, 0),
            $"Standard library root was not found: {ex.Message}",
            "Use a valid path to the StandardLibrary~ directory.",
            entryPath);
        return new StandardLibraryResolution(
            new StandardLibraryModuleGraph(entryModule, _moduleOrder.ToArray()),
            _diagnostics);
      }

      if (!Directory.Exists(_rootPath))
      {
        Report(
            "SBK4001",
            new TextSpan(0, 0),
            "Standard library root was not found.",
            $"Expected standard library root: {_rootPath}",
            entryPath);
        return new StandardLibraryResolution(
            new StandardLibraryModuleGraph(entryModule, _moduleOrder.ToArray()),
            _diagnostics);
      }

      if (!LoadManifest())
      {
        return new StandardLibraryResolution(
            new StandardLibraryModuleGraph(entryModule, _moduleOrder.ToArray()),
            _diagnostics);
      }

      ResolveModuleImports(entryModule, entryUses);
      return new StandardLibraryResolution(
          new StandardLibraryModuleGraph(entryModule, _moduleOrder.ToArray()),
          _diagnostics);
    }

    private void Reset()
    {
      _diagnostics = new DiagnosticBag();
      _manifestModules.Clear();
      _loadedModules.Clear();
      _moduleOrder.Clear();
      _visitStates.Clear();
      _visitStack.Clear();
      _rootPath = null;
    }

    private bool LoadManifest()
    {
      var manifestPath = Path.Combine(_rootPath, ManifestFileName);
      if (!File.Exists(manifestPath))
      {
        Report(
            "SBK4002",
            new TextSpan(0, 0),
            "Standard library manifest could not be read.",
            $"Create '{ManifestFileName}' under the standard library root.",
            manifestPath);
        return false;
      }

      StandardLibraryManifest manifest;
      try
      {
        manifest = JsonUtility.FromJson<StandardLibraryManifest>(
            File.ReadAllText(manifestPath, Encoding.UTF8));
      }
      catch (Exception ex)
      {
        Report(
            "SBK4003",
            new TextSpan(0, 0),
            $"Invalid standard library manifest: {ex.Message}",
            "Use the documented modules array schema.",
            manifestPath);
        return false;
      }

      if (manifest?.modules == null)
      {
        Report(
            "SBK4003",
            new TextSpan(0, 0),
            "Invalid standard library manifest.",
            "The manifest must contain a modules array.",
            manifestPath);
        return false;
      }

      var physicalPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      var valid = true;
      foreach (var entry in manifest.modules)
      {
        if (entry == null ||
            string.IsNullOrWhiteSpace(entry.name) ||
            string.IsNullOrWhiteSpace(entry.path))
        {
          Report(
              "SBK4003",
              new TextSpan(0, 0),
              "Invalid standard library manifest entry.",
              "Every module requires non-empty name and path fields.",
              manifestPath);
          valid = false;
          continue;
        }

        if (_manifestModules.ContainsKey(entry.name))
        {
          Report(
              "SBK4005",
              new TextSpan(0, 0),
              $"Duplicate logical module '{entry.name}'.",
              "Keep one manifest entry per logical module.",
              manifestPath);
          valid = false;
          continue;
        }

        if (Path.IsPathRooted(entry.path))
        {
          Report(
              "SBK4003",
              new TextSpan(0, 0),
              $"Standard library module path must be relative: '{entry.path}'.",
              "Use a source path relative to StandardLibrary~.",
              manifestPath);
          valid = false;
          continue;
        }

        string fullPath;
        try
        {
          fullPath = Path.GetFullPath(Path.Combine(_rootPath, entry.path));
        }
        catch (Exception ex)
        {
          Report(
              "SBK4003",
              new TextSpan(0, 0),
              $"Invalid standard library module path '{entry.path}': {ex.Message}",
              "Use a valid relative source path inside StandardLibrary~.",
              manifestPath);
          valid = false;
          continue;
        }

        if (!IsInsideRoot(fullPath))
        {
          Report(
              "SBK4015",
              new TextSpan(0, 0),
              $"Module path escapes the standard library root: '{entry.path}'.",
              "Use a relative path that remains inside StandardLibrary~.",
              manifestPath);
          valid = false;
          continue;
        }

        if (physicalPaths.TryGetValue(fullPath, out var existingName))
        {
          Report(
              "SBK4005",
              new TextSpan(0, 0),
              $"Duplicate logical modules '{existingName}' and '{entry.name}' map to the same file.",
              "Map each physical source file once.",
              manifestPath);
          valid = false;
          continue;
        }

        physicalPaths.Add(fullPath, entry.name);
        _manifestModules.Add(entry.name, new ManifestModule(entry.name, fullPath));
      }

      return valid;
    }

    private void ResolveModuleImports(
        StandardLibraryModule sourceModule,
        IReadOnlyList<UseDirectiveSyntax> uses)
    {
      foreach (var use in uses)
      {
        if (use.IsMalformed)
          continue;

        var path = use.Path.GetText();
        if (!TryFindManifestModule(path, out var manifestModule, out var declarationName))
        {
          if (LooksLikeExternalApi(path))
          {
            Report(
                "SBK4011",
                GetUseSpan(use),
                $"External APIs cannot be imported with use: '{path}'.",
                "Wrap the API with extern, or import a Sobakasu library module that provides a wrapper.",
                sourceModule.SourcePath);
          }
          else
          {
            Report(
                "SBK4004",
                GetUseSpan(use),
                $"Logical module does not exist for use path '{path}'.",
                "Register the logical module in StandardLibrary~/manifest.json.",
                sourceModule.SourcePath);
          }

          continue;
        }

        var targetModule = LoadModule(manifestModule, sourceModule.SourcePath, GetUseSpan(use));
        if (targetModule == null)
          continue;

        var introducedName = use.Alias?.Text;
        if (string.IsNullOrEmpty(introducedName))
          introducedName = declarationName;
        sourceModule.AddImport(new ResolvedUseDirective(
            use,
            targetModule,
            declarationName,
            introducedName));
      }
    }

    private StandardLibraryModule LoadModule(
        ManifestModule manifestModule,
        string requestingPath,
        TextSpan useSpan)
    {
      if (_visitStates.TryGetValue(manifestModule.Name, out var state))
      {
        if (state == 1)
        {
          var cycle = new List<string>(_visitStack) { manifestModule.Name };
          Report(
              "SBK4006",
              useSpan,
              $"Cyclic module dependency: {string.Join(" -> ", cycle)}.",
              "Remove one of the use directives in the cycle.",
              requestingPath);
          return _loadedModules.TryGetValue(manifestModule.Name, out var cyclicModule)
              ? cyclicModule
              : null;
        }

        return _loadedModules[manifestModule.Name];
      }

      if (!File.Exists(manifestModule.SourcePath))
      {
        Report(
            "SBK4004",
            useSpan,
            $"Logical module '{manifestModule.Name}' does not exist at its manifest path.",
            "Fix the module path in the standard library manifest.",
            requestingPath);
        return null;
      }

      string sourceContent;
      try
      {
        sourceContent = File.ReadAllText(manifestModule.SourcePath, Encoding.UTF8);
      }
      catch (Exception ex)
      {
        Report(
            "SBK4004",
            useSpan,
            $"Logical module '{manifestModule.Name}' could not be read: {ex.Message}",
            "Check the module path and file permissions.",
            requestingPath);
        return null;
      }

      _visitStates[manifestModule.Name] = 1;
      _visitStack.Add(manifestModule.Name);
      var source = SourceText.From(sourceContent);
      var syntax = ParseSource(source, manifestModule.SourcePath);
      var module = new StandardLibraryModule(
          manifestModule.Name,
          manifestModule.SourcePath,
          source,
          syntax,
          isEntry: false);
      _loadedModules.Add(manifestModule.Name, module);
      _moduleOrder.Add(module);

      ResolveModuleImports(module, GetUseDirectives(syntax));
      _visitStack.RemoveAt(_visitStack.Count - 1);
      _visitStates[manifestModule.Name] = 2;
      return module;
    }

    private CompilationUnitSyntax ParseSource(SourceText source, string sourcePath)
    {
      var text = source.Text;
      lock (CacheGate)
      {
        if (ParsedSourceCache.TryGetValue(sourcePath, out var cached) &&
            string.Equals(cached.Source, text, StringComparison.Ordinal))
        {
          foreach (var diagnostic in cached.Diagnostics)
            _diagnostics.Report(diagnostic);
          return cached.Syntax;
        }
      }

      var parser = new SobakasuParser(source, sourcePath);
      var syntax = parser.ParseCompilationUnit();
      foreach (var diagnostic in parser.Diagnostics.Diagnostics)
        _diagnostics.Report(diagnostic);

      var diagnostics = new List<DiagnosticItem>(parser.Diagnostics.Diagnostics).ToArray();
      lock (CacheGate)
      {
        ParsedSourceCache[sourcePath] = new ParsedSourceCacheEntry(
            text,
            syntax,
            diagnostics);
      }

      return syntax;
    }

    private bool TryFindManifestModule(
        string usePath,
        out ManifestModule module,
        out string declarationName)
    {
      module = null;
      declarationName = string.Empty;
      var bestLength = -1;
      foreach (var pair in _manifestModules)
      {
        var prefix = pair.Key + ".";
        if (!usePath.StartsWith(prefix, StringComparison.Ordinal) ||
            pair.Key.Length <= bestLength)
        {
          continue;
        }

        bestLength = pair.Key.Length;
        module = pair.Value;
        declarationName = usePath.Substring(prefix.Length);
      }

      return module != null && declarationName.IndexOf('.') < 0;
    }

    private bool IsInsideRoot(string fullPath)
    {
      var normalizedRoot = _rootPath.TrimEnd(
          Path.DirectorySeparatorChar,
          Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
      return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<UseDirectiveSyntax> GetUseDirectives(
        CompilationUnitSyntax syntax)
    {
      var uses = new List<UseDirectiveSyntax>();
      foreach (var member in syntax.Members)
      {
        if (member is UseDirectiveSyntax use)
          uses.Add(use);
      }

      return uses;
    }

    private static bool LooksLikeExternalApi(string path)
    {
      return path == "System" || path.StartsWith("System.", StringComparison.Ordinal) ||
             path == "UnityEngine" || path.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
             path == "VRC" || path.StartsWith("VRC.", StringComparison.Ordinal) ||
             path == "TMPro" || path.StartsWith("TMPro.", StringComparison.Ordinal);
    }

    private static TextSpan GetUseSpan(UseDirectiveSyntax syntax)
    {
      return TextSpan.FromBounds(
          syntax.UseKeyword.Span.Start,
          syntax.SemicolonToken?.Span.End ?? syntax.Path.Identifiers[^1].Span.End);
    }

    private void Report(
        string code,
        TextSpan span,
        string message,
        string hint,
        string sourcePath)
    {
      _diagnostics.Report(new DiagnosticItem(
          DiagnosticSeverity.Error,
          code,
          span,
          message,
          hint,
          sourcePath));
    }

    private sealed class ManifestModule
    {
      public string Name { get; }
      public string SourcePath { get; }

      public ManifestModule(string name, string sourcePath)
      {
        Name = name;
        SourcePath = sourcePath;
      }
    }

    private sealed class ParsedSourceCacheEntry
    {
      public string Source { get; }
      public CompilationUnitSyntax Syntax { get; }
      public IReadOnlyList<DiagnosticItem> Diagnostics { get; }

      public ParsedSourceCacheEntry(
          string source,
          CompilationUnitSyntax syntax,
          IReadOnlyList<DiagnosticItem> diagnostics)
      {
        Source = source;
        Syntax = syntax;
        Diagnostics = diagnostics;
      }
    }
  }
}
