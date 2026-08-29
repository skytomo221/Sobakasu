using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
  internal sealed class ResolvedUseDirective
  {
    public UseDirectiveSyntax Syntax { get; }
    public UseTreeSyntax Tree { get; }
    public StandardLibraryModule TargetModule { get; }
    public IReadOnlyList<string> DeclarationPath { get; }
    public string DeclarationName => DeclarationPath.Count == 0
        ? string.Empty
        : DeclarationPath[^1];
    public string Path { get; }
    public string IntroducedName { get; }
    public bool ImportsModule => !IsGlob && DeclarationPath.Count == 0;
    public bool IsGlob { get; }
    public bool HasAlias => Tree.Alias != null;
    public bool IsReExport => Syntax.IsReExport;

    public ResolvedUseDirective(
        UseDirectiveSyntax syntax,
        UseTreeSyntax tree,
        StandardLibraryModule targetModule,
        IReadOnlyList<string> declarationPath,
        string path,
        string introducedName,
        bool isGlob)
    {
      Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
      Tree = tree ?? throw new ArgumentNullException(nameof(tree));
      TargetModule = targetModule ?? throw new ArgumentNullException(nameof(targetModule));
      DeclarationPath = declarationPath ?? Array.Empty<string>();
      Path = path ?? string.Empty;
      IntroducedName = introducedName ?? string.Empty;
      IsGlob = isGlob;
    }
  }

  internal sealed class ResolvedModDeclaration
  {
    public ModDeclarationSyntax Syntax { get; }
    public StandardLibraryModule ChildModule { get; }
    public bool IsPublic => Syntax.IsPublic;

    public ResolvedModDeclaration(
        ModDeclarationSyntax syntax,
        StandardLibraryModule childModule)
    {
      Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
      ChildModule = childModule ?? throw new ArgumentNullException(nameof(childModule));
    }
  }

  internal sealed class StandardLibraryModule
  {
    private readonly List<ResolvedUseDirective> _imports = new();
    private readonly List<ResolvedModDeclaration> _children = new();

    public string LogicalName { get; }
    public string SimpleName { get; }
    public string SourcePath { get; }
    public SourceText SourceText { get; }
    public CompilationUnitSyntax Syntax { get; }
    public bool IsEntry { get; }
    public bool IsStandardLibrary => !IsEntry;
    public bool IsRoot { get; }
    public bool IsPrelude { get; private set; }
    public bool IsPublic { get; private set; }
    public bool IsConnected => IsEntry || IsRoot || Parent != null;
    public StandardLibraryModule Parent { get; private set; }
    public ModDeclarationSyntax ParentDeclaration { get; private set; }
    public IReadOnlyList<ResolvedUseDirective> Imports => _imports;
    public IReadOnlyList<ResolvedModDeclaration> Children => _children;

    public StandardLibraryModule(
        string logicalName,
        string sourcePath,
        SourceText sourceText,
        CompilationUnitSyntax syntax,
        bool isEntry,
        bool isRoot = false)
    {
      LogicalName = logicalName ?? string.Empty;
      var lastDot = LogicalName.LastIndexOf('.');
      SimpleName = lastDot < 0 ? LogicalName : LogicalName.Substring(lastDot + 1);
      SourcePath = sourcePath ?? string.Empty;
      SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
      Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
      IsEntry = isEntry;
      IsRoot = isRoot;
      IsPublic = isEntry || isRoot;
    }

    public void AddImport(ResolvedUseDirective import)
    {
      _imports.Add(import ?? throw new ArgumentNullException(nameof(import)));
    }

    public bool TryAttachChild(
        StandardLibraryModule child,
        ModDeclarationSyntax declaration)
    {
      if (child == null)
        throw new ArgumentNullException(nameof(child));
      if (declaration == null)
        throw new ArgumentNullException(nameof(declaration));

      if (child.Parent != null && !ReferenceEquals(child.Parent, this))
        return false;

      child.Parent = this;
      child.ParentDeclaration = declaration;
      child.IsPublic = declaration.IsPublic;
      _children.Add(new ResolvedModDeclaration(declaration, child));
      return true;
    }

    public void MarkAsPrelude()
    {
      IsPrelude = true;
    }
  }

  internal sealed class StandardLibraryModuleGraph
  {
    private readonly Dictionary<string, StandardLibraryModule> _modulesByName;

    public StandardLibraryModule EntryModule { get; }
    public StandardLibraryModule PreludeModule { get; }
    public IReadOnlyList<StandardLibraryModule> Modules { get; }

    public StandardLibraryModuleGraph(
        StandardLibraryModule entryModule,
        IReadOnlyList<StandardLibraryModule> modules,
        StandardLibraryModule preludeModule = null)
    {
      EntryModule = entryModule ?? throw new ArgumentNullException(nameof(entryModule));
      Modules = modules ?? throw new ArgumentNullException(nameof(modules));
      PreludeModule = preludeModule;
      _modulesByName = new Dictionary<string, StandardLibraryModule>(StringComparer.Ordinal);
      foreach (var module in modules)
      {
        if (!module.IsEntry)
          _modulesByName[module.LogicalName] = module;
      }
    }

    public StandardLibraryModule FindModule(string logicalName)
    {
      return logicalName != null && _modulesByName.TryGetValue(logicalName, out var module)
          ? module
          : null;
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
    public const string PreludeLogicalName = "prelude";
    public static readonly string DefaultRoot = Path.Combine(
        "Packages",
        "com.skytomo221.sobakasu",
        "StandardLibrary~");

    private static readonly object CacheGate = new();
    private static readonly Dictionary<string, ParsedSourceCacheEntry> ParsedSourceCache =
        new(StringComparer.OrdinalIgnoreCase);

    private DiagnosticBag _diagnostics = new();
    private readonly Dictionary<string, ModuleLocation> _moduleLocations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, StandardLibraryModule> _loadedModules =
        new(StringComparer.Ordinal);
    private readonly List<StandardLibraryModule> _moduleOrder = new();
    private readonly Dictionary<string, int> _visitStates =
        new(StringComparer.Ordinal);
    private readonly List<string> _visitStack = new();
    private string _rootPath;
    private StandardLibraryModule _preludeModule;

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

      if (!TrySetRoot(rootPath, entryPath))
        return CreateResolution(entryModule);

      ResolveModuleImports(entryModule, GetUseDirectives(entrySyntax));

      if (TryGetModuleLocation(PreludeLogicalName, out var preludeLocation))
      {
        _preludeModule = LoadModule(
            preludeLocation,
            preludeLocation.SourcePath,
            new TextSpan(0, 0));
        _preludeModule?.MarkAsPrelude();
      }

      return CreateResolution(entryModule);
    }

    private StandardLibraryResolution CreateResolution(StandardLibraryModule entryModule)
    {
      return new StandardLibraryResolution(
          new StandardLibraryModuleGraph(
              entryModule,
              _moduleOrder.ToArray(),
              _preludeModule),
          _diagnostics);
    }

    private void Reset()
    {
      _diagnostics = new DiagnosticBag();
      _moduleLocations.Clear();
      _loadedModules.Clear();
      _moduleOrder.Clear();
      _visitStates.Clear();
      _visitStack.Clear();
      _rootPath = null;
      _preludeModule = null;
    }

    private bool TrySetRoot(string rootPath, string entryPath)
    {
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
        return false;
      }

      if (Directory.Exists(_rootPath))
        return true;

      Report(
          "SBK4001",
          new TextSpan(0, 0),
          "Standard library root was not found.",
          $"Expected standard library root: {_rootPath}",
          entryPath);
      return false;
    }

    private void ResolveModuleChildren(StandardLibraryModule sourceModule)
    {
      var declaredChildren = new HashSet<string>(StringComparer.Ordinal);
      foreach (var member in sourceModule.Syntax.Members)
      {
        if (member is not ModDeclarationSyntax declaration || declaration.IsMalformed)
          continue;

        var childName = declaration.Identifier.Text ?? string.Empty;
        if (!declaredChildren.Add(childName))
        {
          Report(
              "SBK4018",
              GetModSpan(declaration),
              $"Child module '{childName}' is declared more than once.",
              "Keep one mod or pub mod declaration for each direct child.",
              sourceModule.SourcePath);
          continue;
        }

        if (sourceModule.IsEntry)
        {
          Report(
              "SBK4017",
              GetModSpan(declaration),
              "Entry sources cannot declare standard-library child modules.",
              "Declare mod in a standard-library module.",
              sourceModule.SourcePath);
          continue;
        }

        var logicalName = $"{sourceModule.LogicalName}.{childName}";
        if (!TryGetModuleLocation(logicalName, out var childLocation) ||
            !string.Equals(
                childLocation.ParentName,
                sourceModule.LogicalName,
                StringComparison.Ordinal))
        {
          Report(
              "SBK4017",
              GetModSpan(declaration),
              $"Direct child module '{logicalName}' does not exist.",
              $"Create '{GetRelativeModulePath(logicalName)}'.",
              sourceModule.SourcePath);
          continue;
        }

        var childModule = LoadModule(
            childLocation,
            sourceModule.SourcePath,
            GetModSpan(declaration));
        if (childModule == null)
          continue;

        if (!sourceModule.TryAttachChild(childModule, declaration))
        {
          Report(
              "SBK4020",
              GetModSpan(declaration),
              $"Module '{logicalName}' is already attached to another parent.",
              "Each child module must have exactly one parent.",
              sourceModule.SourcePath);
        }
      }
    }

    private void ResolveModuleImports(
        StandardLibraryModule sourceModule,
        IReadOnlyList<UseDirectiveSyntax> uses)
    {
      foreach (var use in uses)
      {
        if (use.IsMalformed)
          continue;

        var leaves = new List<FlattenedUseTree>();
        FlattenUseTree(use.UseTree, Array.Empty<string>(), leaves);
        foreach (var leaf in leaves)
        {
          var path = string.Join(".", leaf.Path);
          if (!TryFindModuleTarget(
                  sourceModule,
                  path,
                  out var location,
                  out var declarationPath))
          {
            if (LooksLikeExternalApi(path))
            {
              Report(
                  "SBK4011",
                  leaf.Tree.GetSpan(),
                  $"External APIs cannot be imported with use: '{path}'.",
                  "Wrap the API with extern, or import a Sobakasu library module that provides a wrapper.",
                  sourceModule.SourcePath);
            }
            else
            {
              Report(
                  "SBK4004",
                  leaf.Tree.GetSpan(),
                  $"Logical module does not exist for use path '{path}'.",
                  "Create the convention-based .sobakasu source below StandardLibrary~.",
                  sourceModule.SourcePath);
            }
            continue;
          }

          LoadModuleAncestors(location, sourceModule.SourcePath, leaf.Tree.GetSpan());
          var targetModule = LoadModule(
              location,
              sourceModule.SourcePath,
              leaf.Tree.GetSpan());
          if (targetModule == null)
            continue;

          if (declarationPath.Count > 1 &&
              !CanContainNestedDeclaration(targetModule, declarationPath[0]))
          {
            Report(
                "SBK4004",
                leaf.Tree.GetSpan(),
                $"Logical module does not exist for use path '{path}'.",
                "Create the convention-based .sobakasu source below StandardLibrary~.",
                sourceModule.SourcePath);
            continue;
          }

          var introducedName = leaf.Tree.Alias?.Text;
          if (string.IsNullOrEmpty(introducedName) && !leaf.IsGlob)
          {
            introducedName = declarationPath.Count == 0
                ? targetModule.SimpleName
                : declarationPath[^1];
          }
          sourceModule.AddImport(new ResolvedUseDirective(
              use,
              leaf.Tree,
              targetModule,
              declarationPath,
              path,
              introducedName,
              leaf.IsGlob));
        }
      }
    }

    private static bool CanContainNestedDeclaration(
        StandardLibraryModule module,
        string name)
    {
      foreach (var member in module.Syntax.Members)
      {
        if (member is StructDeclarationSyntax @struct &&
            string.Equals(@struct.Identifier.Text, name, StringComparison.Ordinal))
        {
          return true;
        }

        if (member is EnumDeclarationSyntax @enum &&
            string.Equals(@enum.Identifier.Text, name, StringComparison.Ordinal))
        {
          return true;
        }

        if (member is not UseDirectiveSyntax use ||
            !use.IsReExport ||
            use.IsMalformed)
        {
          continue;
        }

        var leaves = new List<FlattenedUseTree>();
        FlattenUseTree(use.UseTree, Array.Empty<string>(), leaves);
        foreach (var leaf in leaves)
        {
          if (leaf.IsGlob)
            return true;
          var introducedName = leaf.Tree.Alias?.Text;
          if (string.IsNullOrEmpty(introducedName) && leaf.Path.Count > 0)
            introducedName = leaf.Path[^1];
          if (string.Equals(introducedName, name, StringComparison.Ordinal))
            return true;
        }
      }

      return false;
    }

    private void LoadModuleAncestors(
        ModuleLocation module,
        string requestingPath,
        TextSpan useSpan)
    {
      if (string.IsNullOrEmpty(module.ParentName) ||
          !TryGetModuleLocation(module.ParentName, out var parent))
      {
        return;
      }

      LoadModuleAncestors(parent, requestingPath, useSpan);
      if (!_loadedModules.ContainsKey(parent.Name))
        LoadModule(parent, requestingPath, useSpan);
    }

    private StandardLibraryModule LoadModule(
        ModuleLocation location,
        string requestingPath,
        TextSpan dependencySpan)
    {
      if (_visitStates.TryGetValue(location.Name, out var state))
      {
        if (state == 1)
        {
          var cycleStart = _visitStack.IndexOf(location.Name);
          var cycle = cycleStart >= 0
              ? _visitStack.GetRange(cycleStart, _visitStack.Count - cycleStart)
              : new List<string>(_visitStack);
          cycle.Add(location.Name);
          Report(
              "SBK4006",
              dependencySpan,
              $"Cyclic module dependency: {string.Join(" -> ", cycle)}.",
              "Remove one dependency or re-export in the cycle.",
              requestingPath);
          return _loadedModules.TryGetValue(location.Name, out var cyclicModule)
              ? cyclicModule
              : null;
        }

        return _loadedModules[location.Name];
      }

      if (!File.Exists(location.SourcePath))
      {
        Report(
            "SBK4004",
            dependencySpan,
            $"Logical module '{location.Name}' does not exist at '{location.SourcePath}'.",
            $"Create '{GetRelativeModulePath(location.Name)}'.",
            requestingPath);
        return null;
      }

      string sourceContent;
      try
      {
        sourceContent = File.ReadAllText(location.SourcePath, Encoding.UTF8);
      }
      catch (Exception ex)
      {
        Report(
            "SBK4004",
            dependencySpan,
            $"Logical module '{location.Name}' could not be read: {ex.Message}",
            "Check the module file and its permissions.",
            requestingPath);
        return null;
      }

      _visitStates[location.Name] = 1;
      _visitStack.Add(location.Name);
      var source = SourceText.From(sourceContent);
      var syntax = ParseSource(source, location.SourcePath);
      var module = new StandardLibraryModule(
          location.Name,
          location.SourcePath,
          source,
          syntax,
          isEntry: false,
          isRoot: string.IsNullOrEmpty(location.ParentName));
      _loadedModules.Add(location.Name, module);
      _moduleOrder.Add(module);

      ResolveModuleChildren(module);
      ResolveModuleImports(module, GetUseDirectives(syntax));
      _visitStack.RemoveAt(_visitStack.Count - 1);
      _visitStates[location.Name] = 2;
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

    private bool TryFindModuleTarget(
        StandardLibraryModule sourceModule,
        string usePath,
        out ModuleLocation module,
        out IReadOnlyList<string> declarationPath)
    {
      if (TryFindDeclaredChildTarget(
              sourceModule,
              usePath,
              out module,
              out declarationPath))
      {
        return true;
      }

      if (TryFindModuleTarget(usePath, out module, out declarationPath))
        return true;

      if (!sourceModule.IsEntry && !string.IsNullOrEmpty(sourceModule.LogicalName))
      {
        var relativePath = $"{sourceModule.LogicalName}.{usePath}";
        if (TryFindModuleTarget(
                relativePath,
                out module,
                out declarationPath))
        {
          if (!string.Equals(
                  module.Name,
                  sourceModule.LogicalName,
                  StringComparison.Ordinal))
          {
            return true;
          }
        }
      }

      module = null;
      declarationPath = Array.Empty<string>();
      return false;
    }

    private bool TryFindDeclaredChildTarget(
        StandardLibraryModule sourceModule,
        string usePath,
        out ModuleLocation module,
        out IReadOnlyList<string> declarationPath)
    {
      module = null;
      declarationPath = Array.Empty<string>();
      if (sourceModule.IsEntry || string.IsNullOrEmpty(sourceModule.LogicalName))
        return false;

      var separator = usePath.IndexOf('.');
      var firstSegment = separator < 0
          ? usePath
          : usePath.Substring(0, separator);
      foreach (var child in sourceModule.Children)
      {
        if (!string.Equals(
                child.ChildModule.SimpleName,
                firstSegment,
                StringComparison.Ordinal))
        {
          continue;
        }

        return TryFindModuleTarget(
            $"{sourceModule.LogicalName}.{usePath}",
            out module,
            out declarationPath);
      }

      return false;
    }

    private bool TryFindModuleTarget(
        string usePath,
        out ModuleLocation module,
        out IReadOnlyList<string> declarationPath)
    {
      if (TryGetModuleLocation(usePath, out module))
      {
        declarationPath = Array.Empty<string>();
        return true;
      }

      module = null;
      var separator = usePath.LastIndexOf('.');
      while (separator > 0 && separator < usePath.Length - 1)
      {
        var moduleName = usePath.Substring(0, separator);
        if (TryGetModuleLocation(moduleName, out module))
        {
          declarationPath = usePath.Substring(separator + 1).Split('.');
          return true;
        }
        separator = usePath.LastIndexOf('.', separator - 1);
      }

      declarationPath = Array.Empty<string>();
      return false;
    }

    private static void FlattenUseTree(
        UseTreeSyntax tree,
        IReadOnlyList<string> prefix,
        ICollection<FlattenedUseTree> leaves)
    {
      var path = new List<string>(prefix);
      if (tree.Path != null)
      {
        foreach (var identifier in tree.Path.Identifiers)
          path.Add(identifier.Text ?? string.Empty);
      }

      if (tree.Group != null)
      {
        foreach (var item in tree.Group.Items)
          FlattenUseTree(item, path, leaves);
        return;
      }

      if (tree.IsSelf)
      {
        leaves.Add(new FlattenedUseTree(prefix, tree, isGlob: false));
        return;
      }

      leaves.Add(new FlattenedUseTree(path, tree, tree.IsGlob));
    }

    private bool TryGetModuleLocation(string logicalName, out ModuleLocation location)
    {
      if (_moduleLocations.TryGetValue(logicalName, out location))
        return true;

      location = null;
      if (!IsValidLogicalName(logicalName))
        return false;

      string sourcePath;
      try
      {
        sourcePath = GetModuleSourcePath(logicalName);
      }
      catch (Exception)
      {
        return false;
      }
      if (!IsInsideRoot(sourcePath) || !File.Exists(sourcePath))
        return false;

      var parentName = GetLogicalParentName(logicalName);
      if (!string.IsNullOrEmpty(parentName) && !ModuleFileExists(parentName))
      {
        parentName = string.Empty;
      }

      location = new ModuleLocation(logicalName, sourcePath, parentName);
      _moduleLocations.Add(logicalName, location);
      return true;
    }

    private bool ModuleFileExists(string logicalName)
    {
      try
      {
        var sourcePath = GetModuleSourcePath(logicalName);
        return IsInsideRoot(sourcePath) && File.Exists(sourcePath);
      }
      catch (Exception)
      {
        return false;
      }
    }

    private string GetModuleSourcePath(string logicalName)
    {
      var relativePath = GetRelativeModulePath(logicalName)
          .Replace('/', Path.DirectorySeparatorChar);
      return Path.GetFullPath(Path.Combine(_rootPath, relativePath));
    }

    private static string GetRelativeModulePath(string logicalName)
    {
      return logicalName.Replace('.', '/') + ".sobakasu";
    }

    private bool IsInsideRoot(string fullPath)
    {
      var normalizedRoot = _rootPath.TrimEnd(
          Path.DirectorySeparatorChar,
          Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
      return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLogicalParentName(string logicalName)
    {
      var lastDot = logicalName.LastIndexOf('.');
      return lastDot < 0 ? string.Empty : logicalName.Substring(0, lastDot);
    }

    private static bool IsValidLogicalName(string logicalName)
    {
      if (string.IsNullOrEmpty(logicalName))
        return false;

      var segments = logicalName.Split('.');
      foreach (var segment in segments)
      {
        if (segment.Length == 0 || !IsIdentifierStart(segment[0]))
          return false;
        for (var index = 1; index < segment.Length; index++)
        {
          if (!IsIdentifierPart(segment[index]))
            return false;
        }
      }

      return true;
    }

    private static bool IsIdentifierStart(char value)
    {
      return value == '_' || char.IsLetter(value);
    }

    private static bool IsIdentifierPart(char value)
    {
      return value == '_' || char.IsLetterOrDigit(value);
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
      var start = syntax.PubKeyword?.Span.Start ?? syntax.UseKeyword.Span.Start;
      return TextSpan.FromBounds(
          start,
          syntax.SemicolonToken?.Span.End ?? syntax.UseTree.GetSpan().End);
    }

    private static TextSpan GetModSpan(ModDeclarationSyntax syntax)
    {
      var start = syntax.PubKeyword?.Span.Start ?? syntax.ModKeyword.Span.Start;
      return TextSpan.FromBounds(
          start,
          syntax.SemicolonToken?.Span.End ?? syntax.Identifier.Span.End);
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

    private sealed class ModuleLocation
    {
      public string Name { get; }
      public string SourcePath { get; }
      public string ParentName { get; }

      public ModuleLocation(string name, string sourcePath, string parentName)
      {
        Name = name;
        SourcePath = sourcePath;
        ParentName = parentName ?? string.Empty;
      }
    }

    private sealed class FlattenedUseTree
    {
      public IReadOnlyList<string> Path { get; }
      public UseTreeSyntax Tree { get; }
      public bool IsGlob { get; }

      public FlattenedUseTree(
          IReadOnlyList<string> path,
          UseTreeSyntax tree,
          bool isGlob)
      {
        Path = path ?? Array.Empty<string>();
        Tree = tree ?? throw new ArgumentNullException(nameof(tree));
        IsGlob = isGlob;
      }
    }
  }
}
