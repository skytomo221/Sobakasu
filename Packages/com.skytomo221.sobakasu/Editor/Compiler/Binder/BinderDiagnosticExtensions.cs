using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class BinderDiagnosticExtensions
    {
        public static void ReportTypeMismatch(this DiagnosticBag diagnostics, TextSpan span, string expectedType, string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2005",
                span,
                $"Cannot convert type '{actualType}' to '{expectedType}'.",
                "Make the expression type match the expected type."
            ));
        }
    }
}
