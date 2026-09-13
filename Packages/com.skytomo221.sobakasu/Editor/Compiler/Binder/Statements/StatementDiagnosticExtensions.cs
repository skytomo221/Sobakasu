using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class StatementDiagnosticExtensions
    {
        public static void ReportUnsupportedStatement(this DiagnosticBag diagnostics, TextSpan span, string statementKind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2006",
                span,
                $"Unsupported statement '{statementKind}'.",
                "Use a statement form that the compiler currently supports."
            ));
        }

        public static void ReportMissingVariableInitializer(this DiagnosticBag diagnostics, TextSpan span, string variableName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2014",
                span,
                $"Local variable '{variableName}' requires an initializer.",
                "Add '= <expr>' to the declaration."
            ));
        }

        public static void ReportCannotInferVariableType(this DiagnosticBag diagnostics, TextSpan span, string variableName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2018",
                span,
                $"Cannot infer the type of local variable '{variableName}'.",
                "Provide a concrete initializer type or add an explicit type annotation."
            ));
        }

        public static void ReportReturnValueRequired(this DiagnosticBag diagnostics, TextSpan span, string eventName, string returnType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2038",
                span,
                $"Declaration '{eventName}' must return a value of type '{returnType}'.",
                "Add a return statement with a value."
            ));
        }

        public static void ReportReturnValueNotAllowed(this DiagnosticBag diagnostics, TextSpan span, string eventName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2039",
                span,
                $"Declaration '{eventName}' does not return a value.",
                "Use 'return;' or remove the returned expression."
            ));
        }

        public static void ReportReturnTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string expectedType,
            string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2040",
                span,
                $"Return expression type '{actualType}' does not match '{expectedType}'.",
                "Return an expression with the declared return type."
            ));
        }

        public static void ReportBreakValueTargetsWhile(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2050",
                span,
                "A value-producing break cannot target a while expression.",
                "Use 'break;' for while, or target a loop expression when returning a value."
            ));
        }

        public static void ReportMixedLoopBreakValues(this DiagnosticBag diagnostics, TextSpan span, string label)
        {
            var target = string.IsNullOrEmpty(label)
                ? "this loop"
                : $"loop '{label}";
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2051",
                span,
                $"Value-less and value-producing break statements are mixed for {target}.",
                "Use either 'break;' everywhere or give every reachable break a value."
            ));
        }

        public static void ReportLoopBreakTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string expectedType,
            string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2052",
                span,
                $"Loop break value type '{actualType}' does not match '{expectedType}'.",
                "Use exactly the same value type for every break targeting this loop."
            ));
        }

        public static void ReportJumpOutsideLoop(this DiagnosticBag diagnostics, TextSpan span, string statementName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2053",
                span,
                $"'{statementName}' can only be used inside a while or loop expression.",
                "Move the statement into a loop or specify a lexically enclosing loop label."
            ));
        }

        public static void ReportUnknownLoopLabel(this DiagnosticBag diagnostics, TextSpan span, string label)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2054",
                span,
                $"Unknown or non-enclosing loop label '{label}'.",
                "Target a label declared on a lexically enclosing while or loop expression."
            ));
        }

        public static void ReportDuplicateLoopLabel(this DiagnosticBag diagnostics, TextSpan span, string label)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2055",
                span,
                $"Loop label '{label}' overlaps an active label with the same name.",
                "Rename one label so all simultaneously active loop labels are unique."
            ));
        }

        public static void ReportMissingLanguageItem(this DiagnosticBag diagnostics, TextSpan span, string item)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2168",
                span,
                $"Required language item '{item}' is not defined.",
                "Add the matching lang metadata to the standard-library type declaration."
            ));
        }

        public static void ReportUnknownNetworkReceiver(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2141", span,
                $"No network receiver named '{name}' is declared.",
                "Declare it with 'receive' before using 'send'."));
        }

        public static void ReportFunctionIsNotNetworkReceiver(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2142", span,
                $"Function '{name}' cannot be sent as a network event.",
                "Declare a distinct 'receive' entry point and send to that name."));
        }

        public static void ReportNetworkArgumentCountMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string receiver,
            int expected,
            int actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2143", span,
                $"Network receiver '{receiver}' expects {expected} argument(s), but got {actual}.",
                "Pass exactly the logical parameters declared by the receiver."));
        }

        public static void ReportNetworkArgumentTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string receiver,
            int index,
            string expected,
            string actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2144", span,
                $"Argument {index} sent to '{receiver}' expects '{expected}', but got '{actual}'.",
                "Use a value compatible with the receiver parameter type."));
        }

        public static void ReportNetworkTargetTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string expected,
            string actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2145", span,
                $"Network send target must have type '{expected}', but got '{actual}'.",
                "Use all, others, owner, self, or a NetworkEventTarget expression."));
        }

        public static void ReportTuplePatternRequiresTuple(this DiagnosticBag diagnostics, TextSpan span, string actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2162", span,
                $"Tuple destructuring requires a tuple value, but found '{actual}'.",
                "Use a tuple initializer or bind the value to a single name."));
        }

        public static void ReportTuplePatternArity(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            int expected,
            int actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2163", span,
                $"Tuple pattern for '{type}' requires {expected} element(s), but has {actual}.",
                "Use exactly one binding or '_' discard for each tuple element."));
        }
    }
}
