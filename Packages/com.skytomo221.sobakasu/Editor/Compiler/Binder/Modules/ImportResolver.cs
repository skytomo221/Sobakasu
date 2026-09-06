using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ImportResolver : BinderComponent
    {
        internal ImportResolver(BindingSession session) : base(session)
        {
        }

        internal void BuildModuleImports(StandardLibraryModuleGraph graph, bool includeFunctions)
        {
            foreach (var module in graph.Modules)
            {
                Session.Modules.Imports[module].Clear();
                Session.Modules.Aliases[module].Clear();
                Session.Modules.GlobImportNames[module].Clear();
            }

            var unresolved = new List<ModuleImportWorkItem>();
            var globs = new List<ModuleImportWorkItem>();
            foreach (var module in graph.Modules)
            {
                foreach (var import in module.Imports)
                {
                    var workItem = new ModuleImportWorkItem(module, import);
                    if (import.IsGlob)
                        globs.Add(workItem);
                    else
                        unresolved.Add(workItem);
                }
            }

            var expandedGlobNames = new Dictionary<ResolvedUseDirective, HashSet<string>>();
            foreach (var workItem in globs)
                expandedGlobNames[workItem.Import] = new HashSet<string>(StringComparer.Ordinal);
            var passLimit = graph.Modules.Count + unresolved.Count + globs.Count + 1;
            for (var pass = 0; pass <= passLimit; pass++)
            {
                var progress = false;
                for (var index = unresolved.Count - 1; index >= 0; index--)
                {
                    var workItem = unresolved[index];
                    if (!Session.ImportResolver.TryResolveModuleImport(workItem.Module, workItem.Import, includeFunctions, reportDiagnostics: false, out var symbol))
                    {
                        continue;
                    }

                    Session.ImportResolver.AddModuleImport(workItem.Module, workItem.Import, symbol, includeFunctions);
                    unresolved.RemoveAt(index);
                    progress = true;
                }

                foreach (var workItem in globs)
                {
                    var hasUnresolvedExplicitImport = false;
                    foreach (var pending in unresolved)
                    {
                        if (ReferenceEquals(pending.Module, workItem.Module))
                        {
                            hasUnresolvedExplicitImport = true;
                            break;
                        }
                    }

                    if (hasUnresolvedExplicitImport)
                        continue;
                    if (!Session.ImportResolver.TryResolveModuleImport(workItem.Module, workItem.Import, includeFunctions, reportDiagnostics: false, out var container))
                    {
                        continue;
                    }

                    foreach (var pair in Session.ImportResolver.GetGlobExports(container, includeFunctions))
                    {
                        if (!expandedGlobNames[workItem.Import].Add(pair.Key))
                            continue;
                        Session.ImportResolver.AddModuleImport(workItem.Module, workItem.Import, pair.Value, includeFunctions, pair.Key);
                        progress = true;
                    }
                }

                if (!progress)
                    break;
            }

            if (includeFunctions)
            {
                foreach (var workItem in unresolved)
                {
                    Session.ImportResolver.TryResolveModuleImport(workItem.Module, workItem.Import, includeFunctions: true, reportDiagnostics: true, out _);
                }

                foreach (var workItem in globs)
                {
                    Session.ImportResolver.TryResolveModuleImport(workItem.Module, workItem.Import, includeFunctions: true, reportDiagnostics: true, out _);
                }
            }

            foreach (var module in graph.Modules)
            {
                if (!module.DependenciesResolved)
                    continue;
                var resolvedSyntax = new HashSet<UseDirectiveSyntax>();
                foreach (var import in module.Imports)
                    resolvedSyntax.Add(import.Syntax);
                if (!includeFunctions)
                    continue;
                foreach (var member in module.Syntax.Members)
                {
                    if (member is not UseDirectiveSyntax use || use.IsMalformed || resolvedSyntax.Contains(use))
                    {
                        continue;
                    }
                    if (module.HasPendingReExportSyntax(use))
                        continue;

                    Session.Diagnostics.SourcePath = module.SourcePath;
                    var path = use.Path?.GetText() ?? string.Empty;
                    if (Session.ImportResolver.LooksLikeExternalUse(path))
                    {
                        Session.Diagnostics.ReportExternalApiCannotBeImportedWithUse(Session.BinderSyntaxFacts.GetUseDirectiveSpan(use), path);
                    }
                    else
                    {
                        Session.Diagnostics.ReportLogicalModuleDoesNotExist(Session.BinderSyntaxFacts.GetUseDirectiveSpan(use), path);
                    }
                }
            }
        }

        internal bool TryResolveModuleImport(StandardLibraryModule sourceModule, ResolvedUseDirective import, bool includeFunctions, bool reportDiagnostics, out Symbol symbol)
        {
            symbol = null;
            var targetSymbol = Session.Modules.Symbols[import.TargetModule];
            if (!Session.VisibilityResolver.CanAccessModule(sourceModule, import))
            {
                if (reportDiagnostics)
                {
                    Session.Diagnostics.SourcePath = sourceModule.SourcePath;
                    if (!import.TargetModule.IsConnected)
                    {
                        Session.Diagnostics.ReportModuleNotConnected(import.Tree.GetSpan(), import.TargetModule.LogicalName);
                    }
                    else
                    {
                        Session.Diagnostics.ReportModuleNotPublic(import.Tree.GetSpan(), import.TargetModule.LogicalName);
                    }
                }

                return false;
            }

            Symbol current = targetSymbol;
            foreach (var segment in import.DeclarationPath)
            {
                if (current is ModuleSymbol moduleSymbol)
                {
                    var exported = moduleSymbol.LookupExport(segment);
                    if (exported == null)
                    {
                        if (!includeFunctions)
                            return false;
                        if (reportDiagnostics)
                        {
                            Session.Diagnostics.SourcePath = sourceModule.SourcePath;
                            if (moduleSymbol.LookupDeclared(segment) != null)
                                Session.Diagnostics.ReportDeclarationNotPublic(import.Tree.GetSpan(), segment);
                            else
                                Session.Diagnostics.ReportLogicalDeclarationNotFound(import.Tree.GetSpan(), import.Path);
                        }

                        return false;
                    }

                    current = exported;
                    continue;
                }

                if (current is TypeSymbol enumType && enumType.AggregateKind == UserAggregateKind.Enum && enumType.TryGetEnumVariant(segment, out var variant))
                {
                    current = variant;
                    continue;
                }

                if (!includeFunctions)
                    return false;
                if (reportDiagnostics)
                {
                    Session.Diagnostics.SourcePath = sourceModule.SourcePath;
                    Session.Diagnostics.ReportLogicalDeclarationNotFound(import.Tree.GetSpan(), import.Path);
                }

                return false;
            }

            if (!includeFunctions && current is FunctionGroupSymbol)
                return false;
            if (import.IsGlob && current is not ModuleSymbol && current is not TypeSymbol { AggregateKind: UserAggregateKind.Enum })
            {
                if (reportDiagnostics)
                {
                    Session.Diagnostics.SourcePath = sourceModule.SourcePath;
                    Session.Diagnostics.ReportLogicalDeclarationNotFound(import.Tree.GetSpan(), import.Path);
                }

                return false;
            }

            symbol = current;
            return true;
        }

        internal IEnumerable<KeyValuePair<string, Symbol>> GetGlobExports(Symbol container, bool includeFunctions)
        {
            if (container is ModuleSymbol module)
            {
                foreach (var pair in module.Exports)
                {
                    if (!includeFunctions && pair.Value is FunctionGroupSymbol)
                        continue;
                    yield return pair;
                }

                yield break;
            }

            if (container is TypeSymbol { AggregateKind: UserAggregateKind.Enum } enumType)
            {
                foreach (var variant in enumType.EnumVariants)
                {
                    yield return new KeyValuePair<string, Symbol>(variant.Name, variant);
                }
            }
        }

        internal bool AddModuleImport(StandardLibraryModule module, ResolvedUseDirective import, Symbol symbol, bool reportDiagnostics, string introducedName = null)
        {
            introducedName ??= import.IntroducedName;
            var imports = import.HasAlias ? Session.Modules.Aliases[module] : Session.Modules.Imports[module];
            if (import.IsGlob && (Session.Modules.Aliases[module].ContainsKey(introducedName) || Session.Modules.Imports[module].TryGetValue(introducedName, out var explicitOrGlob) && ReferenceEquals(explicitOrGlob, symbol)))
            {
                return false;
            }

            if (imports.TryGetValue(introducedName, out var existing))
            {
                if (ReferenceEquals(existing, symbol))
                    return false;
                if (existing is FunctionGroupSymbol existingFunctions && symbol is FunctionGroupSymbol importedFunctions)
                {
                    if (import.IsGlob && !Session.Modules.GlobImportNames[module].Contains(introducedName))
                    {
                        return false;
                    }

                    if (existingFunctions.TryMerge(importedFunctions))
                    {
                        if (import.IsReExport && Session.Modules.Symbols.TryGetValue(module, out var exportingModule))
                        {
                            foreach (var publicPath in exportingModule.PublicPaths)
                            {
                                Session.ImportResolver.RegisterCanonicalPublicPath(existingFunctions, $"{publicPath}.{introducedName}");
                            }
                        }

                        return true;
                    }
                }

                if (import.IsGlob && !Session.Modules.GlobImportNames[module].Contains(introducedName))
                    return false;
                if (reportDiagnostics)
                {
                    Session.Diagnostics.SourcePath = module.SourcePath;
                    if (import.IsReExport)
                    {
                        Session.Diagnostics.ReportAmbiguousReExport(import.Tree.GetSpan(), introducedName, Session.NameResolver.GetSymbolDisplayName(existing), Session.NameResolver.GetSymbolDisplayName(symbol));
                    }
                    else if (import.HasAlias)
                    {
                        Session.Diagnostics.ReportDuplicateModuleAlias(import.Tree.Alias.Span, introducedName);
                    }
                    else
                    {
                        Session.Diagnostics.ReportAmbiguousModuleImport(import.Tree.GetSpan(), introducedName, Session.NameResolver.GetSymbolDisplayName(existing), Session.NameResolver.GetSymbolDisplayName(symbol));
                    }
                }

                return false;
            }

            if (import.IsGlob && Session.Modules.Aliases[module].ContainsKey(introducedName))
                return false;
            var importedSymbol = symbol is FunctionGroupSymbol functions ? functions.Clone() : symbol;
            imports.Add(introducedName, importedSymbol);
            if (import.IsGlob)
                Session.Modules.GlobImportNames[module].Add(introducedName);
            if (!import.IsReExport)
                return true;
            var moduleSymbol = Session.Modules.Symbols[module];
            if (!moduleSymbol.TryExport(introducedName, importedSymbol, out var exportConflict))
            {
                if (reportDiagnostics && !ReferenceEquals(exportConflict, importedSymbol))
                {
                    Session.Diagnostics.SourcePath = module.SourcePath;
                    Session.Diagnostics.ReportAmbiguousReExport(import.Tree.GetSpan(), introducedName, Session.NameResolver.GetSymbolDisplayName(exportConflict), Session.NameResolver.GetSymbolDisplayName(importedSymbol));
                }

                return false;
            }

            foreach (var publicPath in new List<string>(moduleSymbol.PublicPaths))
            {
                Session.ImportResolver.RegisterCanonicalPublicPath(importedSymbol, $"{publicPath}.{introducedName}");
            }

            return true;
        }

        internal void RegisterCanonicalPublicPath(Symbol symbol, string path)
        {
            if (symbol is ModuleSymbol moduleSymbol)
                moduleSymbol.RegisterPublicPath(path);
            else if (symbol is TypeSymbol typeSymbol)
                typeSymbol.RegisterPublicPath(path);
            else if (symbol is FunctionGroupSymbol functionGroup)
            {
                foreach (var function in functionGroup.Functions)
                    function.RegisterPublicPath(path);
            }
            else if (symbol is ConstantSymbol constantSymbol)
                constantSymbol.RegisterPublicPath(path);
            else if (symbol is EnumVariantSymbol enumVariantSymbol)
                enumVariantSymbol.RegisterPublicPath(path);
        }

        internal bool LooksLikeExternalUse(string path)
        {
            return path == "System" || path.StartsWith("System.", StringComparison.Ordinal) || path == "UnityEngine" || path.StartsWith("UnityEngine.", StringComparison.Ordinal) || path == "VRC" || path.StartsWith("VRC.", StringComparison.Ordinal) || path == "TMPro" || path.StartsWith("TMPro.", StringComparison.Ordinal);
        }
    }
}
