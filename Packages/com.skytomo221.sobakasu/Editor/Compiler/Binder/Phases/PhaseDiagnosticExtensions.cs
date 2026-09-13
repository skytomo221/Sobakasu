using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class PhaseDiagnosticExtensions
    {
        public static void ReportUnsupportedMember(this DiagnosticBag diagnostics, TextSpan span, string memberText)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2009",
                span,
                $"Unsupported top-level member '{memberText}'.",
                "Only supported top-level members can appear here."
            ));
        }

        public static void ReportUnknownLanguageItem(this DiagnosticBag diagnostics, TextSpan span, string item)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2165",
                span,
                $"Unknown language item '{item}'.",
                "Use a compiler-supported language item name and check for spelling mistakes."
            ));
        }

        public static void ReportDuplicateLanguageItem(this DiagnosticBag diagnostics, TextSpan span,
            string item,
            string existingType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2166",
                span,
                $"Language item '{item}' is already assigned to type '{existingType}'.",
                "Keep exactly one type declaration for each language item in the compilation graph."
            ));
        }

        public static void ReportInvalidLanguageItemDeclaration(this DiagnosticBag diagnostics, TextSpan span, string item)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2167",
                span,
                $"Language item '{item}' is not attached to a type declaration.",
                "Apply lang only to a struct, enum, or external type binding impl declaration."
            ));
        }

        public static void ReportStateNotAllowedInStandardLibrary(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4012",
                span,
                "State declarations are not allowed in standard library modules.",
                "Move persistent state to the entry program."
            ));
        }

        public static void ReportEventNotAllowedInStandardLibrary(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4013",
                span,
                "Event declarations are not allowed in standard library modules.",
                "Declare Udon event entry points only in the entry program."
            ));
        }

        public static void ReportReceiveNotAllowedInStandardLibrary(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4014",
                span,
                "Network receiver declarations are not allowed in standard library modules.",
                "Declare network entry points only in the entry program."
            ));
        }
    }
}
