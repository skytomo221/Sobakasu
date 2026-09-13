using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class ExternDiagnosticExtensions
    {
        public static void ReportUnknownExternalMember(this DiagnosticBag diagnostics, TextSpan span,
            string typeName,
            string memberName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2083",
                span,
                $"Unknown external member '{typeName}.{memberName}'.",
                "Check the runtime member name and the installed SDK version."
            ));
        }

        public static void ReportExternalMemberNotExposed(this DiagnosticBag diagnostics, TextSpan span,
            string memberName,
            string details)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2084",
                span,
                $"External member '{memberName}' is not exposed to Udon. {details}",
                "Use an API exposed by the installed VRChat SDK."
            ));
        }

        public static void ReportNoApplicableExternalOverload(this DiagnosticBag diagnostics, TextSpan span,
            string memberName,
            string argumentTypes)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2085",
                span,
                $"No applicable external overload for '{memberName}' with argument types {argumentTypes}.",
                "Check the external member signature and argument types."
            ));
        }

        public static void ReportAmbiguousExternalOverload(this DiagnosticBag diagnostics, TextSpan span,
            string memberName,
            string candidates)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2086",
                span,
                $"Ambiguous external overload for '{memberName}'. Candidates: {candidates}.",
                "Use argument types that select one Udon extern signature."
            ));
        }

        public static void ReportUnsupportedExternalExpression(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2087",
                span,
                "Unsupported external expression.",
                "Use extern with a method, getter, setter, constructor, or unary/binary operator access."
            ));
        }

        public static void ReportAggregateExternBoundary(this DiagnosticBag diagnostics, TextSpan span, string type)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2119", span,
                $"Aggregate type '{type}' cannot cross the Udon extern boundary directly.",
                "Pass the aggregate's Udon-representable leaf values explicitly."));
        }

        public static void ReportGenericExternConstraintViolation(this DiagnosticBag diagnostics, TextSpan span,
            string method,
            string detail)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2126", span,
                $"Type arguments for generic extern method '{method}' do not satisfy its CLR constraints: {detail}",
                "Use concrete type arguments satisfying the CLR generic parameter constraints."));
        }

        public static void ReportWrongGenericMethodArity(this DiagnosticBag diagnostics, TextSpan span,
            string method,
            int actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2127", span,
                $"Generic method '{method}' has no overload accepting {actual} type argument(s).",
                "Supply the explicit type argument count declared by one of the generic overloads."));
        }
    }
}
