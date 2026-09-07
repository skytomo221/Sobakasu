using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Modules
{
    internal sealed class StandardLibraryModuleGraph
    {
        private readonly Dictionary<string, StandardLibraryModule> _modulesByName;

        public StandardLibraryModule EntryModule { get; }
        public StandardLibraryModule PreludeModule { get; }
        public IReadOnlyList<StandardLibraryModule> Modules { get; }

        public StandardLibraryModuleGraph(StandardLibraryModule entryModule,
            IReadOnlyList<StandardLibraryModule> modules, StandardLibraryModule preludeModule = null)
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
                ? module : null;
        }

        public static StandardLibraryModuleGraph CreateSingle(CompilationUnitSyntax syntax,
            SourceText sourceText = null, string sourcePath = "<entry>")
        {
            var entry = new StandardLibraryModule(string.Empty, sourcePath,
                sourceText ?? SourceText.From(string.Empty), syntax, isEntry: true);
            return new StandardLibraryModuleGraph(entry, new[] { entry });
        }
    }
}
