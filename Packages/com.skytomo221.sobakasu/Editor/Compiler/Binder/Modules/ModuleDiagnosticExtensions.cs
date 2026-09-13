using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class ModuleDiagnosticExtensions
    {
        public static void ReportUnresolvedUsePath(this DiagnosticBag diagnostics, TextSpan span, string importedPath)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2019",
                span,
                $"Could not resolve use path '{importedPath}'.",
                "Import a supported namespace, type, or static method group."
            ));
        }

        public static void ReportImportConflict(this DiagnosticBag diagnostics, TextSpan span,
            string introducedName,
            string existingTarget,
            string newTarget)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2020",
                span,
                $"Import name '{introducedName}' conflicts between '{existingTarget}' and '{newTarget}'.",
                "Rename one import with 'as' or remove the conflicting import."
            ));
        }

        public static void ReportAmbiguousImportedReference(this DiagnosticBag diagnostics, TextSpan span,
            string referenceName,
            string candidates)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2021",
                span,
                $"Imported reference '{referenceName}' is ambiguous. Candidates: {candidates}.",
                "Use a more specific path or remove the conflicting imports."
            ));
        }

        public static void ReportUnsupportedUseTarget(this DiagnosticBag diagnostics, TextSpan span,
            string importedPath,
            string reason)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2025",
                span,
                $"Unsupported use target '{importedPath}'. {reason}",
                "Import only namespaces, types, or static method groups supported by v1."
            ));
        }

        public static void ReportLogicalModuleDoesNotExist(this DiagnosticBag diagnostics, TextSpan span, string path)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4004",
                span,
                $"Logical module does not exist for use path '{path}'.",
                "Create the convention-based .sobakasu module below StandardLibrary~. use does not fall back to external APIs."
            ));
        }

        public static void ReportDeclarationNotPublic(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4007",
                span,
                $"Declaration '{name}' is not public.",
                "Add pub to the declaration or import a public wrapper."
            ));
        }

        public static void ReportDuplicateModuleAlias(this DiagnosticBag diagnostics, TextSpan span, string alias)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4008",
                span,
                $"Duplicate module import alias '{alias}'.",
                "Choose a unique alias in this module."
            ));
        }

        public static void ReportAmbiguousModuleImport(this DiagnosticBag diagnostics, TextSpan span,
            string name,
            string first,
            string second)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4009",
                span,
                $"Ambiguous imported name '{name}': {first}, {second}.",
                "Use an as alias to give each imported declaration a unique name."
            ));
        }

        public static void ReportLogicalDeclarationNotFound(this DiagnosticBag diagnostics, TextSpan span, string path)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4010",
                span,
                $"Sobakasu declaration was not found for use path '{path}'.",
                "Check the declaration name and convention-based module path."
            ));
        }

        public static void ReportExternalApiCannotBeImportedWithUse(this DiagnosticBag diagnostics, TextSpan span, string path)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4011",
                span,
                $"External APIs cannot be imported with use: '{path}'.",
                "Wrap the API with extern, or import a Sobakasu library module that provides a wrapper."
            ));
        }

        public static void ReportModuleNotPublic(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4021",
                span,
                $"Module '{name}' is private from this location.",
                "Use a public parent re-export or change the parent declaration to pub mod."
            ));
        }

        public static void ReportModuleNotConnected(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4022",
                span,
                $"Module '{name}' exists but is not connected by its parent.",
                "Add mod or pub mod for this direct child in the parent module."
            ));
        }

        public static void ReportAmbiguousReExport(this DiagnosticBag diagnostics, TextSpan span,
            string name,
            string first,
            string second)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4024",
                span,
                $"Ambiguous re-exported name '{name}': {first}, {second}.",
                "Use an as alias or remove one re-export."
            ));
        }
    }
}
