using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    public static class ParserDiagnosticExtensions
    {
        public static void ReportLegacyStateDeclaration(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK1052", span,
                "State declarations must be inside a `state` block.",
                "Move this declaration into `state { ... }` and remove its `state` keyword."));

        public static void ReportLegacyEventDeclaration(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK1053", span,
                "Event declarations must be inside a `behavior` block.",
                "Move this event into `behavior { ... }`."));

        public static void ReportLegacyReceiveDeclaration(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK1054", span,
                "Network receive declarations must be inside a `behavior` block.",
                "Move this receiver into `behavior { ... }`."));

        public static void ReportUnexpectedBehaviorMember(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK1055", span,
                "Only functions, events, and network receivers are allowed in a `behavior` block.",
                "Use `function`, `on`, or `receive`."));

        public static void ReportMisplacedStateCapability(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK1056", span,
                "The `state` capability must be the first item in a parameter list.",
                "Move `state` before the runtime parameters."));

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

        public static void ReportOrphanDocumentationComment(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1051",
                span,
                "Documentation comment is not attached to a documentable declaration.",
                "Place it immediately before a documentable declaration without a blank line or ordinary comment."
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
                "Functions without 'self' in an implementation are associated functions."
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
                "Write 'self' as the first parameter of an implementation function."
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

        public static void ReportInvalidModuleDeclaration(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1025",
                span,
                "Invalid module declaration.",
                "Use 'module <child>;' or 'public module <child>;' with one child module name."
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

        public static void ReportModuleMustBeTopLevel(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1026",
                span,
                "module declarations are only allowed at the top level.",
                "Move the module declaration outside the function or block."
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
                "State member modifiers are in the wrong position.",
                "Use the canonical state-member modifier order: `public sync(...) name`."
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

        public static void ReportPublicModifierOnlyOnStateMember(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1014",
                span,
                "`public` cannot be used on a local declaration.",
                "Declare a public state member inside a `state` block, or remove `public`."
            ));
        }

        public static void ReportSynchronizationOnlyOnStateMember(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1015",
                span,
                "`sync` can only be used on a state member.",
                "Move the declaration into a `state` block, or remove `sync`."
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
                "Use sync only on state members; use public only on supported state, function, external binding, or implementation-method declarations."
            ));
        }

        public static void ReportMissingStateMemberInitializer(this DiagnosticBag diagnostics, TextSpan span, string stateName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1017",
                span,
                $"State member '{stateName}' requires an initializer.",
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
                "Apply `language item` only to a struct, enum, or external type binding implementation declaration."
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
                "A state member is always mutable and cannot use 'mutable'.",
                "Remove 'mutable' from the state member declaration."
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
                    : "Move the declaration to the top level or use a local 'let mutable'."
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

        public static void ReportUnexpectedImplementationMember(this DiagnosticBag diagnostics, TextSpan span, SyntaxKind actualKind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK1023",
                span,
                $"Unexpected token '{actualKind}' in implementation block.",
                "Only function declarations are allowed in an implementation block."
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
