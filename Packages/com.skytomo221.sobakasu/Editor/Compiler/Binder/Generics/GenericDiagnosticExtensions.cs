using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class GenericDiagnosticExtensions
    {
        public static void ReportCannotInferGenericParameter(this DiagnosticBag diagnostics, TextSpan span,
            string parameter,
            string type)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2122", span,
                $"Cannot infer type parameter '{parameter}' for '{type}'.",
                "Specify explicit type arguments or provide a payload/expected type that determines this parameter."));
        }

        public static void ReportConflictingGenericInference(this DiagnosticBag diagnostics, TextSpan span,
            string parameter,
            string first,
            string second)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2123", span,
                $"Type parameter '{parameter}' cannot be inferred as both '{first}' and '{second}'.",
                "Use values that infer one identical concrete type argument."));
        }
    }
}
