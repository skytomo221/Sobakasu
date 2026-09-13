using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class ResolutionDiagnosticExtensions
    {
        public static void ReportUndefinedMember(this DiagnosticBag diagnostics, TextSpan span, string receiverType, string memberName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2003",
                span,
                $"'{receiverType}' does not contain a member named '{memberName}'.",
                "Use a supported member for the receiver type."
            ));
        }

        public static void ReportUnknownType(this DiagnosticBag diagnostics, TextSpan span, string typeName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2015",
                span,
                $"Unknown type '{typeName}'.",
                "Use a supported built-in type name."
            ));
        }

        public static void ReportUnsupportedUnaryOperator(this DiagnosticBag diagnostics, TextSpan span,
            string operatorText,
            string operandType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2026",
                span,
                $"Operator '{operatorText}' is not defined for operand type '{operandType}'.",
                "Use a supported unary operator for the operand type."
            ));
        }

        public static void ReportUnsupportedBinaryOperator(this DiagnosticBag diagnostics, TextSpan span,
            string operatorText,
            string leftType,
            string rightType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2027",
                span,
                $"Operator '{operatorText}' is not defined for operand types '{leftType}' and '{rightType}'.",
                "Use a supported binary operator with exact operand types."
            ));
        }

        public static void ReportAmbiguousOperator(this DiagnosticBag diagnostics, TextSpan span,
            string operatorText,
            string operandTypes,
            string candidates)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2028",
                span,
                $"Operator '{operatorText}' for operand type(s) {operandTypes} is ambiguous. Candidates: {candidates}.",
                "Make the operand types unambiguous or use a different operator."
            ));
        }

        public static void ReportShortCircuitRequiresBoolOperands(this DiagnosticBag diagnostics, TextSpan span,
            string operatorText,
            string leftType,
            string rightType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2030",
                span,
                $"Operator '{operatorText}' requires bool operands, but got '{leftType}' and '{rightType}'.",
                "Use bool expressions on both sides of the short-circuit operator."
            ));
        }

        public static void ReportSelfTypeOutsideImpl(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2074",
                span,
                "Self is only available inside an impl block.",
                "Use a concrete type name outside impl."
            ));
        }

        public static void ReportWrongGenericArity(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            int expected,
            int actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2121", span,
                $"Generic type '{type}' expects {expected} type argument(s), but got {actual}.",
                "Supply exactly the declared number of concrete type arguments."));
        }

        public static void ReportModuleMemberNotPublic(this DiagnosticBag diagnostics, TextSpan span, string module, string member)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK4025",
                span,
                $"Member '{member}' is private in module '{module}'.",
                "Make the declaration public or use a public re-export."
            ));
        }
    }
}
