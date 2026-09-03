using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
  internal class PendingModuleImport
  {
    public UseDirectiveSyntax Syntax { get; }
    public UseTreeSyntax Tree { get; }
    public string TargetModuleName { get; }
    public IReadOnlyList<string> DeclarationPath { get; }
    public string Path { get; }
    public string IntroducedName { get; }
    public bool IsGlob { get; }
    public bool HasAlias => Tree.Alias != null;
    public bool IsMaterialized { get; private set; }
    public StandardLibraryModule ResolvedModule { get; private set; }

    public PendingModuleImport(
        UseDirectiveSyntax syntax,
        UseTreeSyntax tree,
        string targetModuleName,
        IReadOnlyList<string> declarationPath,
        string path,
        string introducedName,
        bool isGlob)
    {
      Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
      Tree = tree ?? throw new ArgumentNullException(nameof(tree));
      TargetModuleName = targetModuleName ?? string.Empty;
      DeclarationPath = declarationPath ?? Array.Empty<string>();
      Path = path ?? string.Empty;
      IntroducedName = introducedName ?? string.Empty;
      IsGlob = isGlob;
    }

    public ResolvedUseDirective Materialize(
        StandardLibraryModule targetModule,
        StandardLibraryModule resolvedModule = null)
    {
      IsMaterialized = true;
      ResolvedModule = resolvedModule;
      return new ResolvedUseDirective(
          Syntax,
          Tree,
          targetModule,
          DeclarationPath,
          Path,
          IntroducedName,
          IsGlob);
    }
  }

  internal sealed class PendingReExport : PendingModuleImport
  {
    public string ExportedName { get; }

    public PendingReExport(
        UseDirectiveSyntax syntax,
        UseTreeSyntax tree,
        string targetModuleName,
        IReadOnlyList<string> declarationPath,
        string path,
        string exportedName,
        bool isGlob)
        : base(
            syntax,
            tree,
            targetModuleName,
            declarationPath,
            path,
            exportedName,
            isGlob)
    {
      ExportedName = exportedName ?? string.Empty;
    }
  }

  internal sealed class PendingChildModule
  {
    public string Name { get; }
    public string LogicalName { get; }
    public ModDeclarationSyntax Syntax { get; }
    public bool IsPublic => Syntax.IsPublic;

    public PendingChildModule(
        string name,
        string logicalName,
        ModDeclarationSyntax syntax)
    {
      Name = name ?? string.Empty;
      LogicalName = logicalName ?? string.Empty;
      Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
    }
  }

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
    private readonly List<PendingModuleImport> _pendingImports = new();
    private readonly Dictionary<string, PendingChildModule> _pendingChildren =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<PendingReExport>> _pendingReExports =
        new(StringComparer.Ordinal);
    private readonly List<PendingReExport> _pendingGlobReExports = new();
    private readonly HashSet<UseDirectiveSyntax> _pendingReExportSyntax = new();

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
    public bool DependenciesResolved { get; private set; }
    public bool IsConnected => IsEntry || IsRoot || Parent != null;
    public StandardLibraryModule Parent { get; private set; }
    public ModDeclarationSyntax ParentDeclaration { get; private set; }
    public IReadOnlyList<ResolvedUseDirective> Imports => _imports;
    public IReadOnlyList<ResolvedModDeclaration> Children => _children;
    public IReadOnlyList<PendingModuleImport> PendingImports => _pendingImports;
    public IReadOnlyDictionary<string, PendingChildModule> PendingChildren =>
        _pendingChildren;
    public IReadOnlyList<PendingReExport> PendingGlobReExports =>
        _pendingGlobReExports;
    public IEnumerable<string> PendingReExportNames => _pendingReExports.Keys;

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
      DependenciesResolved = isEntry;
    }

    public void AddImport(ResolvedUseDirective import)
    {
      _imports.Add(import ?? throw new ArgumentNullException(nameof(import)));
    }

    public void AddPendingImport(PendingModuleImport import)
    {
      _pendingImports.Add(import ?? throw new ArgumentNullException(nameof(import)));
    }

    public bool TryAddPendingChild(PendingChildModule child)
    {
      if (child == null)
        throw new ArgumentNullException(nameof(child));
      if (_pendingChildren.ContainsKey(child.Name))
        return false;
      _pendingChildren.Add(child.Name, child);
      return true;
    }

    public bool TryGetPendingChild(string name, out PendingChildModule child)
    {
      return _pendingChildren.TryGetValue(name, out child);
    }

    public void AddPendingReExport(PendingReExport reExport)
    {
      if (reExport == null)
        throw new ArgumentNullException(nameof(reExport));
      if (reExport.IsGlob)
      {
        _pendingGlobReExports.Add(reExport);
      }
      else
      {
        if (!_pendingReExports.TryGetValue(reExport.ExportedName, out var exports))
        {
          exports = new List<PendingReExport>();
          _pendingReExports.Add(reExport.ExportedName, exports);
        }
        exports.Add(reExport);
      }
      _pendingReExportSyntax.Add(reExport.Syntax);
    }

    public bool TryGetPendingReExports(
        string name,
        out IReadOnlyList<PendingReExport> reExports)
    {
      if (_pendingReExports.TryGetValue(name, out var exports))
      {
        reExports = exports;
        return true;
      }
      reExports = Array.Empty<PendingReExport>();
      return false;
    }

    public bool HasPendingReExportSyntax(UseDirectiveSyntax syntax)
    {
      return syntax != null && _pendingReExportSyntax.Contains(syntax);
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

      foreach (var existing in _children)
      {
        if (ReferenceEquals(existing.ChildModule, child))
          return true;
      }

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

    internal void MarkAsPublic()
    {
      IsPublic = true;
    }

    internal void MarkDependenciesResolved()
    {
      DependenciesResolved = true;
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
    private readonly HashSet<StandardLibraryModule> _indexedModules =
        new();
    private readonly HashSet<StandardLibraryModule> _processedImports =
        new();
    private readonly Dictionary<StandardLibraryModule, HashSet<StandardLibraryModule>> _dependencyEdges =
        new();
    private readonly HashSet<string> _resolvingExports =
        new(StringComparer.Ordinal);
    private readonly List<string> _exportStack = new();
    private readonly HashSet<string> _reportedCycles = new(StringComparer.Ordinal);
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

      IndexModule(entryModule);

      if (TryGetModuleLocation(PreludeLogicalName, out var preludeLocation))
      {
        _preludeModule = LoadModule(
            preludeLocation,
            preludeLocation.SourcePath,
            new TextSpan(0, 0));
        _preludeModule?.MarkAsPrelude();
      }

      MaterializeRequiredClosure();

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
      _indexedModules.Clear();
      _processedImports.Clear();
      _dependencyEdges.Clear();
      _resolvingExports.Clear();
      _exportStack.Clear();
      _reportedCycles.Clear();
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

    private void IndexModule(StandardLibraryModule module)
    {
      if (module == null || !_indexedModules.Add(module))
        return;
      IndexModuleChildren(module);
      IndexModuleImports(module, GetUseDirectives(module.Syntax));
    }

    private void IndexModuleChildren(StandardLibraryModule sourceModule)
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

        sourceModule.TryAddPendingChild(new PendingChildModule(
            childName,
            logicalName,
            declaration));
      }
    }

    private void IndexModuleImports(
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

          var introducedName = leaf.Tree.Alias?.Text;
          if (string.IsNullOrEmpty(introducedName) && !leaf.IsGlob)
          {
            introducedName = declarationPath.Count == 0
                ? GetSimpleName(location.Name)
                : declarationPath[^1];
          }
          if (use.IsReExport)
          {
            sourceModule.AddPendingReExport(new PendingReExport(
                use,
                leaf.Tree,
                location.Name,
                declarationPath,
                path,
                introducedName,
                leaf.IsGlob));
          }
          else
          {
            sourceModule.AddPendingImport(new PendingModuleImport(
                use,
                leaf.Tree,
                location.Name,
                declarationPath,
                path,
                introducedName,
                leaf.IsGlob));
          }
        }
      }
    }

    private void MaterializeRequiredClosure()
    {
      for (var index = 0; index < _moduleOrder.Count; index++)
      {
        var module = _moduleOrder[index];
        MaterializeImports(module);
        MaterializeSyntaxReferences(module);
      }

      foreach (var module in _moduleOrder)
        module.MarkDependenciesResolved();
    }

    private void MaterializeImports(StandardLibraryModule sourceModule)
    {
      if (!_processedImports.Add(sourceModule))
        return;

      foreach (var import in sourceModule.PendingImports)
        TryMaterializeImport(sourceModule, import);
    }

    private bool TryMaterializeImport(
        StandardLibraryModule sourceModule,
        PendingModuleImport import)
    {
      if (import.IsMaterialized)
        return true;
      if (!TryGetModuleLocation(import.TargetModuleName, out var location))
        return false;

      LoadModuleAncestors(location, sourceModule.SourcePath, import.Tree.GetSpan());
      var targetModule = LoadModule(
          location,
          sourceModule.SourcePath,
          import.Tree.GetSpan());
      if (targetModule == null)
        return false;

      AddDependency(sourceModule, targetModule, import.Tree.GetSpan());
      StandardLibraryModule resolvedModule = targetModule;
      if (import.DeclarationPath.Count > 0 &&
          !TryResolveDeclarationPath(
              targetModule,
              import.DeclarationPath,
              sourceModule.SourcePath,
              import.Tree.GetSpan(),
              out resolvedModule))
      {
        return false;
      }

      if (import.IsGlob && resolvedModule != null)
        MaterializeAllExports(resolvedModule, sourceModule.SourcePath, import.Tree.GetSpan());

      sourceModule.AddImport(import.Materialize(targetModule, resolvedModule));
      return true;
    }

    private bool TryResolveDeclarationPath(
        StandardLibraryModule module,
        IReadOnlyList<string> declarationPath,
        string requestingPath,
        TextSpan span,
        out StandardLibraryModule resolvedModule)
    {
      resolvedModule = module;
      foreach (var segment in declarationPath)
      {
        if (resolvedModule == null)
          return true;
        if (!TryMaterializeModuleMember(
                resolvedModule,
                segment,
                requestingPath,
                span,
                out resolvedModule))
        {
          return false;
        }
      }
      return true;
    }

    private bool TryMaterializeModuleMember(
        StandardLibraryModule module,
        string memberName,
        string requestingPath,
        TextSpan span,
        out StandardLibraryModule resolvedModule)
    {
      resolvedModule = null;
      var foundCandidate = false;
      if (module.TryGetPendingReExports(memberName, out var reExports))
      {
        foundCandidate = true;
        if (!TryMaterializeNamedReExports(
                module,
                memberName,
                reExports,
                requestingPath,
                span,
                out resolvedModule))
        {
          return false;
        }
      }

      if (module.TryGetPendingChild(memberName, out var child))
      {
        foundCandidate = true;
        var childModule = EnsureChildLoaded(module, child, requestingPath, span);
        resolvedModule ??= childModule;
      }

      foreach (var glob in module.PendingGlobReExports)
      {
        foundCandidate = true;
        if (!TryMaterializeGlobReExport(
                module,
                glob,
                memberName,
                requestingPath,
                span,
                out var globModule))
        {
          return false;
        }
        resolvedModule ??= globModule;
      }

      // No lazy module metadata means this segment is a declaration (or an
      // error that the Binder will diagnose). Either way no more modules are
      // needed for the remaining member/type path.
      if (!foundCandidate)
        resolvedModule = null;
      return true;
    }

    private bool TryMaterializeNamedReExports(
        StandardLibraryModule module,
        string exportedName,
        IReadOnlyList<PendingReExport> reExports,
        string requestingPath,
        TextSpan span,
        out StandardLibraryModule resolvedModule)
    {
      resolvedModule = null;
      var key = $"{module.LogicalName}.{exportedName}";
      if (!_resolvingExports.Add(key))
      {
        ReportExportCycle(key, requestingPath, span);
        return false;
      }

      _exportStack.Add(key);
      try
      {
        foreach (var reExport in reExports)
        {
          if (reExport.IsMaterialized)
          {
            resolvedModule ??= reExport.ResolvedModule;
            continue;
          }

          if (!TryMaterializeReExport(
                  module,
                  reExport,
                  requestingPath,
                  span,
                  out var exportedModule))
          {
            return false;
          }
          resolvedModule ??= exportedModule;
        }
        return true;
      }
      finally
      {
        _exportStack.RemoveAt(_exportStack.Count - 1);
        _resolvingExports.Remove(key);
      }
    }

    private bool TryMaterializeReExport(
        StandardLibraryModule sourceModule,
        PendingReExport reExport,
        string requestingPath,
        TextSpan span,
        out StandardLibraryModule resolvedModule)
    {
      resolvedModule = reExport.ResolvedModule;
      if (reExport.IsMaterialized)
        return true;
      if (!TryGetModuleLocation(reExport.TargetModuleName, out var location))
        return false;

      LoadModuleAncestors(location, requestingPath, span);
      var targetModule = LoadModule(location, requestingPath, span);
      if (targetModule == null || !AddDependency(sourceModule, targetModule, span))
        return false;

      resolvedModule = targetModule;
      if (reExport.DeclarationPath.Count > 0 &&
          !TryResolveDeclarationPath(
              targetModule,
              reExport.DeclarationPath,
              requestingPath,
              span,
              out resolvedModule))
      {
        return false;
      }

      sourceModule.AddImport(reExport.Materialize(targetModule, resolvedModule));
      return true;
    }

    private bool TryMaterializeGlobReExport(
        StandardLibraryModule sourceModule,
        PendingReExport reExport,
        string requestedName,
        string requestingPath,
        TextSpan span,
        out StandardLibraryModule resolvedModule)
    {
      resolvedModule = reExport.ResolvedModule;
      if (!reExport.IsMaterialized)
      {
        if (!TryMaterializeReExport(
                sourceModule,
                reExport,
                requestingPath,
                span,
                out resolvedModule))
        {
          return false;
        }
      }

      if (resolvedModule == null)
        return true;
      var containerModule = resolvedModule;
      return TryMaterializeModuleMember(
          containerModule,
          requestedName,
          requestingPath,
          span,
          out resolvedModule);
    }

    private void MaterializeAllExports(
        StandardLibraryModule module,
        string requestingPath,
        TextSpan span)
    {
      var key = $"{module.LogicalName}.*";
      if (!_resolvingExports.Add(key))
      {
        ReportExportCycle(key, requestingPath, span);
        return;
      }

      _exportStack.Add(key);
      try
      {
        foreach (var child in module.PendingChildren.Values)
        {
          if (child.IsPublic)
            EnsureChildLoaded(module, child, requestingPath, span);
        }

        var names = new List<string>(module.PendingReExportNames);
        foreach (var name in names)
        {
          if (module.TryGetPendingReExports(name, out var reExports))
          {
            TryMaterializeNamedReExports(
                module,
                name,
                reExports,
                requestingPath,
                span,
                out _);
          }
        }

        foreach (var glob in module.PendingGlobReExports)
        {
          if (!glob.IsMaterialized)
          {
            TryMaterializeReExport(
                module,
                glob,
                requestingPath,
                span,
                out _);
          }
          if (glob.ResolvedModule != null)
            MaterializeAllExports(glob.ResolvedModule, requestingPath, span);
        }
      }
      finally
      {
        _exportStack.RemoveAt(_exportStack.Count - 1);
        _resolvingExports.Remove(key);
      }
    }

    private StandardLibraryModule EnsureChildLoaded(
        StandardLibraryModule parent,
        PendingChildModule child,
        string requestingPath,
        TextSpan span)
    {
      if (!TryGetModuleLocation(child.LogicalName, out var location))
        return null;
      var childModule = LoadModule(location, requestingPath, span);
      if (childModule == null)
        return null;
      if (!parent.TryAttachChild(childModule, child.Syntax))
      {
        Report(
            "SBK4020",
            GetModSpan(child.Syntax),
            $"Module '{child.LogicalName}' is already attached to another parent.",
            "Each child module must have exactly one parent.",
            parent.SourcePath);
      }
      return childModule;
    }

    private void MaterializeSyntaxReferences(StandardLibraryModule sourceModule)
    {
      var qualifiedPaths = new Dictionary<string, SyntaxReference>(StringComparer.Ordinal);
      var simpleNames = new Dictionary<string, TextSpan>(StringComparer.Ordinal);
      CollectSyntaxReferences(sourceModule.Syntax, qualifiedPaths, simpleNames);

      foreach (var reference in qualifiedPaths.Values)
      {
        if (!TryResolveVisibleModule(
                sourceModule,
                reference.Path[0],
                sourceModule.SourcePath,
                reference.Span,
                out var module))
        {
          continue;
        }

        var remaining = new string[reference.Path.Count - 1];
        for (var index = 1; index < reference.Path.Count; index++)
          remaining[index - 1] = reference.Path[index];
        TryResolveDeclarationPath(
            module,
            remaining,
            sourceModule.SourcePath,
            reference.Span,
            out _);
      }

      foreach (var pair in simpleNames)
      {
        MaterializeVisibleSimpleName(
            sourceModule,
            pair.Key,
            sourceModule.SourcePath,
            pair.Value);
      }
    }

    private bool TryResolveVisibleModule(
        StandardLibraryModule sourceModule,
        string name,
        string requestingPath,
        TextSpan span,
        out StandardLibraryModule module)
    {
      module = null;
      if (sourceModule.TryGetPendingChild(name, out var child))
      {
        module = EnsureChildLoaded(sourceModule, child, requestingPath, span);
        if (module != null)
          return true;
      }

      if (TryResolveImportedModule(sourceModule, name, aliasesOnly: true, out module) ||
          TryResolveImportedModule(sourceModule, name, aliasesOnly: false, out module))
      {
        return true;
      }

      if (HasLazyExport(sourceModule, name) &&
          TryMaterializeModuleMember(
              sourceModule,
              name,
              requestingPath,
              span,
              out module) &&
          module != null)
      {
        return true;
      }

      if (sourceModule.IsEntry &&
          _preludeModule != null &&
          HasLazyExport(_preludeModule, name) &&
          TryMaterializeModuleMember(
              _preludeModule,
              name,
              requestingPath,
              span,
              out module) &&
          module != null)
      {
        return true;
      }

      return false;
    }

    private static bool TryResolveImportedModule(
        StandardLibraryModule sourceModule,
        string name,
        bool aliasesOnly,
        out StandardLibraryModule module)
    {
      module = null;
      foreach (var import in sourceModule.PendingImports)
      {
        if (import.IsMaterialized &&
            import.HasAlias == aliasesOnly &&
            string.Equals(import.IntroducedName, name, StringComparison.Ordinal) &&
            import.ResolvedModule != null)
        {
          module = import.ResolvedModule;
          return true;
        }
      }
      return false;
    }

    private void MaterializeVisibleSimpleName(
        StandardLibraryModule sourceModule,
        string name,
        string requestingPath,
        TextSpan span)
    {
      if (sourceModule.TryGetPendingChild(name, out var child))
        EnsureChildLoaded(sourceModule, child, requestingPath, span);

      if (HasLazyExport(sourceModule, name))
      {
        TryMaterializeModuleMember(
            sourceModule,
            name,
            requestingPath,
            span,
            out _);
      }

      if (sourceModule.IsEntry &&
          _preludeModule != null &&
          HasLazyExport(_preludeModule, name))
      {
        TryMaterializeModuleMember(
            _preludeModule,
            name,
            requestingPath,
            span,
            out _);
      }
    }

    private static bool HasLazyExport(StandardLibraryModule module, string name)
    {
      return module.TryGetPendingReExports(name, out _) ||
          module.TryGetPendingChild(name, out var child) && child.IsPublic ||
          module.PendingGlobReExports.Count > 0;
    }

    private bool AddDependency(
        StandardLibraryModule source,
        StandardLibraryModule target,
        TextSpan span)
    {
      if (!_dependencyEdges.TryGetValue(source, out var dependencies))
      {
        dependencies = new HashSet<StandardLibraryModule>();
        _dependencyEdges.Add(source, dependencies);
      }
      if (!dependencies.Add(target))
        return true;

      var path = new List<StandardLibraryModule>();
      if (!ReferenceEquals(source, target) &&
          !TryFindDependencyPath(target, source, new HashSet<StandardLibraryModule>(), path))
      {
        return true;
      }

      var cycle = new List<string> { GetModuleDisplayName(source) };
      foreach (var item in path)
        cycle.Add(GetModuleDisplayName(item));
      if (ReferenceEquals(source, target))
        cycle.Add(GetModuleDisplayName(source));
      var cycleText = string.Join(" -> ", cycle);
      if (_reportedCycles.Add(cycleText))
      {
        Report(
            "SBK4006",
            span,
            $"Cyclic module dependency: {cycleText}.",
            "Remove one dependency or re-export in the cycle.",
            source.SourcePath);
      }
      return false;
    }

    private bool TryFindDependencyPath(
        StandardLibraryModule current,
        StandardLibraryModule target,
        ISet<StandardLibraryModule> visited,
        IList<StandardLibraryModule> path)
    {
      if (!visited.Add(current))
        return false;
      path.Add(current);
      if (ReferenceEquals(current, target))
        return true;
      if (_dependencyEdges.TryGetValue(current, out var dependencies))
      {
        foreach (var dependency in dependencies)
        {
          if (TryFindDependencyPath(dependency, target, visited, path))
            return true;
        }
      }
      path.RemoveAt(path.Count - 1);
      return false;
    }

    private void ReportExportCycle(string key, string requestingPath, TextSpan span)
    {
      var start = _exportStack.IndexOf(key);
      var cycle = start >= 0
          ? _exportStack.GetRange(start, _exportStack.Count - start)
          : new List<string>(_exportStack);
      cycle.Add(key);
      var cycleText = string.Join(" -> ", cycle);
      if (!_reportedCycles.Add(cycleText))
        return;
      Report(
          "SBK4006",
          span,
          $"Cyclic module dependency: {cycleText}.",
          "Remove one dependency or re-export in the cycle.",
          requestingPath);
    }

    private static string GetModuleDisplayName(StandardLibraryModule module)
    {
      return string.IsNullOrEmpty(module.LogicalName) ? "<entry>" : module.LogicalName;
    }

    private void CollectSyntaxReferences(
        SyntaxNode root,
        IDictionary<string, SyntaxReference> qualifiedPaths,
        IDictionary<string, TextSpan> simpleNames)
    {
      foreach (var node in EnumerateSyntaxNodes(root))
      {
        if (node is MemberAccessExpressionSyntax member &&
            TryGetMemberPath(member, out var memberPath) &&
            memberPath.Count > 1)
        {
          var key = string.Join(".", memberPath);
          if (!qualifiedPaths.ContainsKey(key))
          {
            qualifiedPaths.Add(
                key,
                new SyntaxReference(memberPath, member.Name.Span));
          }
          continue;
        }

        if (node is TypeSyntax type && !type.IsArray && !type.IsTuple)
        {
          if (type.Parts.Count > 1)
          {
            var path = new List<string>();
            foreach (var part in type.Parts)
              path.Add(part.Text ?? string.Empty);
            var key = string.Join(".", path);
            if (!qualifiedPaths.ContainsKey(key))
              qualifiedPaths.Add(key, new SyntaxReference(path, type.GetSpan()));
          }
          else if (type.Parts.Count == 1)
          {
            var name = type.Parts[0].Text ?? string.Empty;
            if (!simpleNames.ContainsKey(name))
              simpleNames.Add(name, type.Parts[0].Span);
          }
          continue;
        }

        if (node is NameExpressionSyntax nameExpression)
        {
          var name = nameExpression.Name;
          if (!simpleNames.ContainsKey(name))
            simpleNames.Add(name, nameExpression.IdentifierToken.Span);
        }
      }
    }

    private IEnumerable<SyntaxNode> EnumerateSyntaxNodes(SyntaxNode root)
    {
      if (root == null)
        yield break;

      var stack = new Stack<SyntaxNode>();
      stack.Push(root);
      while (stack.Count > 0)
      {
        var node = stack.Pop();
        yield return node;
        var properties = node.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var property in properties)
        {
          if (property.GetIndexParameters().Length != 0 ||
              !CanContainSyntaxNode(property.PropertyType))
          {
            continue;
          }

          var value = property.GetValue(node);
          if (value is SyntaxNode child)
          {
            stack.Push(child);
          }
          else if (value is IEnumerable values)
          {
            foreach (var item in values)
            {
              if (item is SyntaxNode itemNode)
                stack.Push(itemNode);
            }
          }
        }
      }
    }

    private static bool CanContainSyntaxNode(Type type)
    {
      if (typeof(SyntaxNode).IsAssignableFrom(type))
        return true;
      if (!typeof(IEnumerable).IsAssignableFrom(type) || type == typeof(string))
        return false;
      if (!type.IsGenericType)
        return true;
      foreach (var argument in type.GetGenericArguments())
      {
        if (typeof(SyntaxNode).IsAssignableFrom(argument))
          return true;
      }
      return false;
    }

    private static bool TryGetMemberPath(
        ExpressionSyntax expression,
        out IReadOnlyList<string> path)
    {
      var segments = new List<string>();
      if (!AppendMemberPath(expression, segments))
      {
        path = Array.Empty<string>();
        return false;
      }
      path = segments;
      return true;
    }

    private static bool AppendMemberPath(
        ExpressionSyntax expression,
        ICollection<string> path)
    {
      if (expression is NameExpressionSyntax name)
      {
        path.Add(name.Name);
        return true;
      }
      if (expression is GenericTypeExpressionSyntax generic)
        return AppendMemberPath(generic.Target, path);
      if (expression is not MemberAccessExpressionSyntax member ||
          !AppendMemberPath(member.Expression, path))
      {
        return false;
      }
      path.Add(member.MemberName);
      return true;
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

      AttachToLoadedParent(location, module);
      IndexModule(module);
      _visitStack.RemoveAt(_visitStack.Count - 1);
      _visitStates[location.Name] = 2;
      return module;
    }

    private void AttachToLoadedParent(
        ModuleLocation location,
        StandardLibraryModule module)
    {
      if (string.IsNullOrEmpty(location.ParentName) ||
          !_loadedModules.TryGetValue(location.ParentName, out var parent))
      {
        return;
      }

      foreach (var member in parent.Syntax.Members)
      {
        if (member is not ModDeclarationSyntax declaration ||
            declaration.IsMalformed ||
            !string.Equals(
                declaration.Identifier.Text,
                module.SimpleName,
                StringComparison.Ordinal))
        {
          continue;
        }

        parent.TryAttachChild(module, declaration);
        if (IsPubliclyReExportedModule(parent, module.SimpleName))
          module.MarkAsPublic();
        return;
      }
    }

    private static bool IsPubliclyReExportedModule(
        StandardLibraryModule parent,
        string childName)
    {
      foreach (var member in parent.Syntax.Members)
      {
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
          if (!leaf.IsGlob &&
              leaf.Tree.Alias == null &&
              leaf.Path.Count == 1 &&
              string.Equals(leaf.Path[0], childName, StringComparison.Ordinal))
          {
            return true;
          }
        }
      }

      return false;
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
      foreach (var member in sourceModule.Syntax.Members)
      {
        if (member is not ModDeclarationSyntax declaration || declaration.IsMalformed)
          continue;
        if (!string.Equals(
                declaration.Identifier.Text,
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

    private static string GetSimpleName(string logicalName)
    {
      var lastDot = logicalName.LastIndexOf('.');
      return lastDot < 0 ? logicalName : logicalName.Substring(lastDot + 1);
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

    private sealed class SyntaxReference
    {
      public IReadOnlyList<string> Path { get; }
      public TextSpan Span { get; }

      public SyntaxReference(IReadOnlyList<string> path, TextSpan span)
      {
        Path = path ?? Array.Empty<string>();
        Span = span;
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
