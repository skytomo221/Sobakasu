using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Modules;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ModuleSymbol : Symbol
    {
        private readonly Dictionary<string, ModuleSymbol> _children =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Symbol> _declarations =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Symbol> _exports =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _publicPaths = new(StringComparer.Ordinal);

        public override SymbolKind Kind => SymbolKind.Module;
        public StandardLibraryModule SourceModule { get; }
        public string QualifiedName => SourceModule.LogicalName;
        public ModuleSymbol Parent { get; private set; }
        public bool IsPublic => SourceModule.IsPublic;
        public bool IsConnected => SourceModule.IsConnected;
        public bool IsPrelude => SourceModule.IsPrelude;
        public string CanonicalPublicPath { get; private set; }
        public string ExternalMemberName { get; }
        public IReadOnlyCollection<string> PublicPaths => _publicPaths;
        public IReadOnlyDictionary<string, ModuleSymbol> Children => _children;
        public IReadOnlyDictionary<string, Symbol> Exports => _exports;

        public ModuleSymbol(StandardLibraryModule sourceModule)
            : base(sourceModule?.SimpleName ?? string.Empty)
        {
            SourceModule = sourceModule ?? throw new ArgumentNullException(nameof(sourceModule));
            if (sourceModule.IsRoot)
                RegisterPublicPath(sourceModule.LogicalName);
        }

        public void AttachChild(ModuleSymbol child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            child.Parent = this;
            _children[child.Name] = child;
            if (child.IsPublic)
            {
                _exports[child.Name] = child;
                foreach (var publicPath in new List<string>(_publicPaths))
                    child.RegisterPublicPath($"{publicPath}.{child.Name}");
            }
        }

        public bool TryDeclare(string name, Symbol symbol)
        {
            if (_declarations.TryGetValue(name, out var existing))
            {
                if (existing is FunctionGroupSymbol existingFunctions &&
                    symbol is FunctionGroupSymbol newFunctions)
                {
                    return existingFunctions.TryMerge(newFunctions);
                }
                return false;
            }
            _declarations.Add(name, symbol);
            return true;
        }

        public bool TryExport(string name, Symbol symbol, out Symbol existing)
        {
            if (_exports.TryGetValue(name, out existing))
            {
                if (existing is FunctionGroupSymbol existingFunctions &&
                    symbol is FunctionGroupSymbol newFunctions)
                {
                    return existingFunctions.TryMerge(newFunctions);
                }
                return ReferenceEquals(existing, symbol);
            }

            _exports.Add(name, symbol);
            return true;
        }

        public Symbol LookupDeclared(string name)
        {
            if (_declarations.TryGetValue(name, out var declaration))
                return declaration;
            if (_children.TryGetValue(name, out var child))
                return child;
            return null;
        }

        public Symbol LookupExport(string name)
        {
            return _exports.TryGetValue(name, out var symbol) ? symbol : null;
        }

        public void RegisterPublicPath(string path)
        {
            RegisterPublicPath(path, new HashSet<ModuleSymbol>());
        }

        private void RegisterPublicPath(string path, ISet<ModuleSymbol> visited)
        {
            if (string.IsNullOrEmpty(path) || !visited.Add(this))
                return;

            _publicPaths.Add(path);

            if (string.IsNullOrEmpty(CanonicalPublicPath) ||
                IsBetterPublicPath(path, CanonicalPublicPath))
            {
                CanonicalPublicPath = path;
            }

            foreach (var pair in _exports)
            {
                if (pair.Value is ModuleSymbol exportedModule)
                    exportedModule.RegisterPublicPath($"{path}.{pair.Key}", visited);
            }
        }

        public bool HasPublicPath(string path)
        {
            return !string.IsNullOrEmpty(path) && _publicPaths.Contains(path);
        }

        private static bool IsBetterPublicPath(string candidate, string current)
        {
            var candidateSegments = candidate.Split('.').Length;
            var currentSegments = current.Split('.').Length;
            return candidateSegments < currentSegments ||
                candidateSegments == currentSegments &&
                string.CompareOrdinal(candidate, current) < 0;
        }
    }
}
