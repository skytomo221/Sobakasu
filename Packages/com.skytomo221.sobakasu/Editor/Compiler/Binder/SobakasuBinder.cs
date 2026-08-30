using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class SobakasuBinder
  {
    private readonly SobakasuCompilationEnvironment _environment;
    private IReadOnlyDictionary<StandardLibraryModule, ModuleSymbol> _moduleSymbols =
        new Dictionary<StandardLibraryModule, ModuleSymbol>();
    private IReadOnlyDictionary<string, TypeSymbol> _languageItems =
        new Dictionary<string, TypeSymbol>();

    public SobakasuBinder()
        : this(SobakasuBuiltInEnvironment.Default)
    {
    }

    internal SobakasuBinder(SobakasuCompilationEnvironment environment)
    {
      _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public DiagnosticBag Diagnostics { get; } = new();

    internal IReadOnlyDictionary<StandardLibraryModule, ModuleSymbol> ModuleSymbols =>
        _moduleSymbols;
    internal IReadOnlyDictionary<string, TypeSymbol> LanguageItems => _languageItems;

    public BoundProgram BindProgram(CompilationUnitSyntax syntax)
    {
      return BindProgram(StandardLibraryModuleGraph.CreateSingle(syntax));
    }

    internal BoundProgram BindProgram(StandardLibraryModuleGraph graph)
    {
      var session = new BindingSession(_environment, Diagnostics);
      var program = session.Pipeline.BindProgram(graph);
      _moduleSymbols = session.Modules.Symbols;
      _languageItems = session.LanguageItems.Types;
      return program;
    }
  }
}
