using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class ValidationDiagnosticExtensions
    {
        public static void ReportRecursiveFunction(this DiagnosticBag diagnostics, TextSpan span, string functionName, string cycle)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2045",
                span,
                $"Function '{functionName}' is recursive in v1. Cycle: {cycle}.",
                "Rewrite the function without recursion or wait for runtime call-frame support."
            ));
        }

        public static void ReportRecursiveAggregate(this DiagnosticBag diagnostics, TextSpan span, string cycle)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2105", span,
                $"Recursive aggregate type is not supported: {cycle}.",
                "Remove the struct/enum dependency cycle so storage can be flattened."));
        }

        public static void ReportUnsupportedAggregateLeafAbi(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string path,
            string leafType)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2116", span,
                $"Aggregate '{type}' field path '{path}' has unsupported Udon leaf type '{leafType}'.",
                "Use a field type that has concrete Udon storage."));
        }

        public static void ReportInvalidAggregateArrayLeafAbi(this DiagnosticBag diagnostics, TextSpan span,
            string arrayType,
            string path,
            string leafType,
            string reason)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2117", span,
                $"Aggregate array '{arrayType}' field path '{path}' cannot use leaf array '{leafType}'. {reason}",
                "Use aggregate fields whose typed leaf arrays are exposed by the installed SDK."));
        }

        public static void ReportOpenGenericType(this DiagnosticBag diagnostics, TextSpan span, string type)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2124", span,
                $"Open generic type '{type}' cannot be used where concrete storage is required.",
                "Resolve every type parameter before lowering or runtime storage."));
        }
    }
}
