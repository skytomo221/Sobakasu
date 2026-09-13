using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Lexer
{
    public static class LexerDiagnosticExtensions
    {
        public static void ReportBadCharacter(this DiagnosticBag diagnostics, TextSpan span, char c)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK0001",
                span,
                $"Unexpected character '{c}'.",
                "Remove the character or replace it with supported syntax."
            ));
        }

        public static void ReportUnterminatedString(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK0002",
                span,
                "Unterminated string literal.",
                "Add a closing '\"' to terminate the string."
            ));
        }

        public static void ReportUnterminatedBlockComment(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK0008",
                span,
                "Unterminated block comment.",
                "Add a closing '*/' to terminate the block comment."
            ));
        }

        public static void ReportInvalidEscapeSequence(this DiagnosticBag diagnostics, TextSpan span, string escapeText)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK0003",
                span,
                $"Invalid escape sequence '{escapeText}'.",
                "Use a supported escape sequence for the current literal kind."
            ));
        }

        public static void ReportInvalidNumericLiteral(this DiagnosticBag diagnostics, TextSpan span, string literalText)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK0004",
                span,
                $"Invalid numeric literal '{literalText}'.",
                "Check the base prefix, suffix, underscore placement, and numeric range."
            ));
        }

        public static void ReportUnterminatedCharacterLiteral(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK0005",
                span,
                "Unterminated character literal.",
                "Add a closing '\\'' to terminate the character literal."
            ));
        }

        public static void ReportMalformedCharacterLiteral(this DiagnosticBag diagnostics, TextSpan span, string literalText)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK0006",
                span,
                $"Malformed character literal '{literalText}'.",
                "Use exactly one UTF-16 code unit or a supported escape sequence inside single quotes."
            ));
        }

        public static void ReportRemovedNullLiteral(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK0007",
                span,
                "The 'null' literal is not part of the Sobakasu source language.",
                "Represent an optional value explicitly with Maybe<T>, Maybe.Nothing, and Maybe.Just(value)."
            ));
        }
    }
}

