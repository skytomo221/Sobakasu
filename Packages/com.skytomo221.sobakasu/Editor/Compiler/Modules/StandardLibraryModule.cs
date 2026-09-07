using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
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
        public IReadOnlyDictionary<string, PendingChildModule> PendingChildren => _pendingChildren;
        public IReadOnlyList<PendingReExport> PendingGlobReExports => _pendingGlobReExports;
        public IEnumerable<string> PendingReExportNames => _pendingReExports.Keys;

        public StandardLibraryModule(string logicalName, string sourcePath, SourceText sourceText,
            CompilationUnitSyntax syntax, bool isEntry, bool isRoot = false)
        {
            LogicalName = logicalName ?? string.Empty;
            var lastDot = LogicalName.LastIndexOf('.');
            SimpleName = lastDot < 0 ? LogicalName : LogicalName[(lastDot + 1)..];
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

        public bool TryGetPendingReExports(string name, out IReadOnlyList<PendingReExport> reExports)
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

        public bool TryAttachChild(StandardLibraryModule child, ModDeclarationSyntax declaration)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            if (child.Parent != null && !ReferenceEquals(child.Parent, this))
                return false;

            foreach (var existing in _children)
            {
                if (ReferenceEquals(existing.ChildModule, child))
                    return true;
            }

            child.Parent = this;
            child.ParentDeclaration = declaration ?? throw new ArgumentNullException(nameof(declaration));
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
}
