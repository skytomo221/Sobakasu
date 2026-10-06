using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler
{
    internal static class SobakasuSourceKindValidator
    {
        public static DiagnosticBag Validate(StandardLibraryModuleGraph graph)
        {
            var diagnostics = new DiagnosticBag();
            if (graph == null)
                return diagnostics;

            foreach (var module in graph.Modules)
                ValidateModule(module, diagnostics);

            return diagnostics;
        }

        private static void ValidateModule(
            StandardLibraryModule module,
            DiagnosticBag diagnostics)
        {
            if (module == null)
                return;

            var sourcePath = module.SourcePath ?? string.Empty;
            var sourceKind = SobakasuSourceKinds.FromPath(sourcePath);
            if (sourceKind == SobakasuSourceKind.Editor)
            {
                diagnostics.Report(new DiagnosticItem(
                    DiagnosticSeverity.Error,
                    "SBK5003",
                    new TextSpan(0, 0),
                    "Editor Sources are reserved but are not supported yet.",
                    "Use a .sobakasu Script Source or .library.sobakasu Library Source until Editor Source semantics are defined.",
                    sourcePath));
                return;
            }

            if (sourceKind != SobakasuSourceKind.Library)
                return;

            foreach (var member in module.Syntax.Members)
            {
                if (member is StateBlockDeclarationSyntax stateBlock)
                {
                    ReportLibraryRuntimeDeclaration(
                        diagnostics,
                        "SBK2307",
                        stateBlock.StateKeyword.Span,
                        "state",
                        "Library Sources cannot own runtime state.",
                        sourcePath);
                    continue;
                }

                if (member is StateDeclarationSyntax state)
                {
                    ReportLibraryRuntimeDeclaration(
                        diagnostics,
                        "SBK2307",
                        state.StateKeyword.Span,
                        "state",
                        "Library Sources cannot own runtime state.",
                        sourcePath);
                    continue;
                }

                if (member is BehaviorDeclarationSyntax behavior)
                {
                    ReportLibraryRuntimeDeclaration(
                        diagnostics,
                        "SBK2308",
                        behavior.BehaviorKeyword.Span,
                        "behavior",
                        "Library Sources do not have a runtime UdonBehaviour instance.",
                        sourcePath);
                }
            }
        }

        private static void ReportLibraryRuntimeDeclaration(
            DiagnosticBag diagnostics,
            string code,
            TextSpan span,
            string declarationName,
            string reason,
            string sourcePath)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                code,
                span,
                $"'{declarationName}' is not allowed in a Library Source.",
                $"{reason} Move this declaration to a .sobakasu Script Source.",
                sourcePath));
        }
    }
}
