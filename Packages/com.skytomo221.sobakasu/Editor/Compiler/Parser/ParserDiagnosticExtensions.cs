using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    public static class ParserDiagnosticExtensions
    {
        public static void ReportUnexpectedToken(this DiagnosticBag diagnostics, TextSpan span,
            SyntaxKind actualKind,
            SyntaxKind expectedKind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1001",
                span,
                $"Unexpected token <{actualKind}>, expected <{expectedKind}>.",
                "Fix the token order so the parser can continue."
            ));
        }

        public static void ReportUnexpectedMember(this DiagnosticBag diagnostics, TextSpan span, SyntaxKind kind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1002",
                span,
                $"Unexpected member start <{kind}>.",
                "Only supported top-level declarations can appear here."
            ));
        }

        public static void ReportUnexpectedExpression(this DiagnosticBag diagnostics, TextSpan span, SyntaxKind kind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1003",
                span,
                $"Unexpected token <{kind}> in expression.",
                "Replace it with a valid expression."
            ));
        }

        public static void ReportInvalidUseDirective(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1004",
                span,
                "Invalid use directive.",
                "Use a '::'-separated path, leaf alias, grouped use tree, self leaf, or glob followed by ';'."
            ));
        }

        public static void ReportStaticKeywordRemoved(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1024",
                span,
                "'static' is no longer supported.",
                "Functions without 'self' in an impl are associated functions."
            ));
        }

        public static void ReportDotPathSeparator(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1048",
                span,
                "Module and type paths use '::', not '.'.",
                "Use 'core::string' instead of 'core.string'."
            ));
        }

        public static void ReportPathSeparatorInExternalIdentity(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1050",
                span,
                "CLR/Udon external identities use '.', not '::'.",
                "Write 'External.Namespace.Type' instead of 'External::Namespace::Type'."
            ));
        }

        public static void ReportSelfParameterCannotHaveType(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1047",
                span,
                "The receiver parameter 'self' cannot have a type annotation.",
                "Write 'self' as the first parameter of an impl function."
            ));
        }

        public static void ReportSelfParameterMustBeFirst(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1049",
                span,
                "The receiver parameter 'self' must be the first parameter.",
                "Move 'self' to the first parameter position."
            ));
        }

        public static void ReportInvalidModDeclaration(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1025",
                span,
                "Invalid mod declaration.",
                "Use 'mod <child>;' or 'pub mod <child>;' with one child module name."
            ));
        }

        public static void ReportUnsupportedPatternForm(this DiagnosticBag diagnostics, TextSpan span, string patternText)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1027",
                span,
                $"Unsupported pattern form '{patternText}'.",
                "Use '_', a supported literal, or a qualified enum variant pattern."
            ));
        }

        public static void ReportModMustBeTopLevel(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1026",
                span,
                "mod declarations are only allowed at the top level.",
                "Move the mod declaration outside the function or block."
            ));
        }

        public static void ReportMissingLoopLabelColon(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1005",
                span,
                "Loop label declaration is missing ':'.",
                "Write the label as \"'label: while ...\" or \"'label: loop ...\"."
            ));
        }

        public static void ReportInvalidLoopLabelTarget(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1006",
                span,
                "A loop label can only be attached to 'while' or 'loop'.",
                "Place the label immediately before a while or loop expression."
            ));
        }

        public static void ReportControlBodyRequiresBlock(this DiagnosticBag diagnostics, TextSpan span, string keyword)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1007",
                span,
                $"The body following '{keyword}' must be enclosed in braces.",
                $"Write '{keyword} ... {{ ... }}'; single-statement bodies cannot omit braces."
            ));
        }

        public static void ReportJumpDoesNotAcceptValue(this DiagnosticBag diagnostics, TextSpan span, string keyword)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1008",
                span,
                $"'{keyword}' does not accept a value.",
                $"Write '{keyword};' or '{keyword} 'label;'."
            ));
        }

        public static void ReportInvalidJumpSyntax(this DiagnosticBag diagnostics, TextSpan span, string keyword)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1009",
                span,
                $"Invalid token sequence in '{keyword}' statement.",
                "End the jump statement with ';' and place an optional label before any break value."
            ));
        }

        public static void ReportUnknownSynchronizationMode(this DiagnosticBag diagnostics, TextSpan span, string mode)
        {
            var displayMode = string.IsNullOrEmpty(mode) ? "<empty>" : mode;
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1010",
                span,
                $"Unknown synchronization mode '{displayMode}'.",
                "Use one of the allowed modes: none, linear, smooth."
            ));
        }

        public static void ReportSynchronizationModeArgumentCount(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1011",
                span,
                "A synchronization modifier accepts exactly one mode.",
                "Write sync(none), sync(linear), or sync(smooth)."
            ));
        }

        public static void ReportStateModifierOrder(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1012",
                span,
                "State declaration modifiers are in the wrong position.",
                "Use the canonical order: pub, sync(...), state, name."
            ));
        }

        public static void ReportDuplicateStateModifier(this DiagnosticBag diagnostics, TextSpan span, string modifier)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1013",
                span,
                $"State declaration modifier '{modifier}' is duplicated.",
                $"Remove the duplicate '{modifier}' modifier."
            ));
        }

        public static void ReportPublicModifierOnlyOnTopLevelState(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1014",
                span,
                "pub can only be used on top-level state declarations.",
                "Move the declaration to the top level or remove 'pub'."
            ));
        }

        public static void ReportSynchronizedStateMustBeTopLevel(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1015",
                span,
                "Synchronized state must be declared at top level.",
                "Move the declaration to the top level and write 'sync state name = value;'."
            ));
        }

        public static void ReportUnsupportedTopLevelModifier(this DiagnosticBag diagnostics, TextSpan span,
            string modifier,
            SyntaxKind declarationKind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1016",
                span,
                $"Modifier '{modifier}' is not supported on <{declarationKind}> declarations.",
                "Use sync only on top-level state; use pub only on supported state, function, external binding, or impl-method declarations."
            ));
        }

        public static void ReportMissingTopLevelStateInitializer(this DiagnosticBag diagnostics, TextSpan span, string stateName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1017",
                span,
                $"Top-level state '{stateName}' requires an initializer.",
                "Add '= <compile-time constant>' before the terminating semicolon."
            ));
        }

        public static void ReportInvalidLanguageItemTarget(this DiagnosticBag diagnostics, TextSpan span, SyntaxKind kind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1042",
                span,
                $"Language items cannot be applied to <{kind}> declarations.",
                "Apply lang only to a struct, enum, or external type binding impl declaration."
            ));
        }

        public static void ReportPublicStateCannotHaveSourceInitializer(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1040",
                span,
                "Public state cannot have a source initializer because its value is provided by the UdonBehaviour Inspector.",
                "Remove the initializer and keep an explicit type annotation."
            ));
        }

        public static void ReportPublicStateRequiresExplicitType(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1041",
                span,
                "Public state requires an explicit type because public state cannot have a source initializer.",
                "Add an explicit type annotation before the terminating semicolon."
            ));
        }

        public static void ReportTopLevelLetNoLongerSupported(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1033",
                span,
                "Top-level 'let' is no longer supported.",
                "Use 'const' for compile-time values or 'state' for persistent mutable state."
            ));
        }

        public static void ReportInvalidExternalFunctionBinding(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1038",
                span,
                "Invalid declarative extern binding.",
                "Use '= extern <external access>' or '= maybe extern <external access>'; general expression-bodied functions are not supported."
            ));
        }

        public static void ReportInvalidMaybeExternalAbiParameter(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1039",
                span,
                "Invalid 'maybe' modifier in an extern ABI parameter.",
                "Use 'maybe out <type> <name>'; 'maybe ref' and normal 'maybe' parameters are not supported."
            ));
        }

        public static void ReportStateCannotUseMut(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1034",
                span,
                "A 'state' declaration is always mutable and cannot use 'mut'.",
                "Remove 'mut' and write 'state name = value;'."
            ));
        }

        public static void ReportSynchronizationOnlyOnState(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1035",
                span,
                "The 'sync' modifier can only be used on a 'state' declaration.",
                "Remove 'sync' or change the declaration to 'sync state name = value;'."
            ));
        }

        public static void ReportDeclarationMustBeTopLevel(this DiagnosticBag diagnostics, TextSpan span, string keyword)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1036",
                span,
                $"'{keyword}' declarations are only allowed at the top level.",
                keyword == "const"
                    ? "Move the declaration to the top level or use a local 'let'."
                    : "Move the declaration to the top level or use a local 'let mut'."
            ));
        }

        public static void ReportMissingConstantInitializer(this DiagnosticBag diagnostics, TextSpan span, string constantName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1037",
                span,
                $"Constant '{constantName}' requires an initializer.",
                "Add '= <compile-time constant>' before the terminating semicolon."
            ));
        }

        public static void ReportQuestionMarkNotAllowedInName(this DiagnosticBag diagnostics, TextSpan span, string nameKind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1018",
                span,
                $"'?' can only be used at the end of a callable name; it is not allowed in a {nameKind} name.",
                "Remove '?' or use it once at the end of a function name."
            ));
        }

        public static void ReportMultipleCallableQuestionMarks(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1019",
                span,
                "A callable name can end with at most one '?'.",
                "Remove the extra '?' suffix."
            ));
        }

        public static void ReportQuestionMarkMustEndCallableName(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1022",
                span,
                "'?' can only be used at the end of a callable name.",
                "Move '?' to the end of the name or remove it."
            ));
        }

        public static void ReportBangCallableNameSuffix(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1020",
                span,
                "'!' cannot be used as a callable-name suffix.",
                "'!' is reserved for future macro syntax."
            ));
        }

        public static void ReportCallableParametersRequireParentheses(this DiagnosticBag diagnostics, TextSpan span,
            string declarationKind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1021",
                span,
                $"Parameters in a {declarationKind} declaration must be enclosed in parentheses.",
                "Use '(name: Type)' for one or more parameters; only an empty parameter list may omit parentheses."
            ));
        }

        public static void ReportUnexpectedImplMember(this DiagnosticBag diagnostics, TextSpan span, SyntaxKind actualKind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1023",
                span,
                $"Unexpected token '{actualKind}' in impl block.",
                "Only fn declarations are allowed in an impl block."
            ));
        }

        public static void ReportReceiveReturnTypeNotAllowed(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1028",
                span,
                "Network receiver declarations always return '()' and cannot declare a return type.",
                "Remove the '-> Type' annotation from the receive declaration."
            ));
        }
    }
}
