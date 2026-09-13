using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class ConstantDiagnosticExtensions
    {
        public static void ReportCannotInferConstantType(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2150", span,
                $"Cannot infer the type of constant '{name}'.",
                "Add an explicit type and use a supported compile-time constant initializer."));
        }

        public static void ReportUnsupportedConstantType(this DiagnosticBag diagnostics, TextSpan span,
            string name,
            string type)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2151", span,
                $"Constant '{name}' has unsupported compile-time type '{type}'.",
                "Use a primitive, bool, char, string, or supported external scalar type."));
        }

        public static void ReportConstantInitializerMustBeConstant(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2152", span,
                $"Initializer for constant '{name}' must be fully evaluable at compile time.",
                "Constants may depend on other constants, but not on state, runtime calls, or extern calls."));
        }

        public static void ReportConstantDependencyCycle(this DiagnosticBag diagnostics, TextSpan span, string path)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2153", span,
                $"Constant dependency cycle detected: {path}.",
                "Break the cycle so every constant has an acyclic compile-time value."));
        }
    }
}
