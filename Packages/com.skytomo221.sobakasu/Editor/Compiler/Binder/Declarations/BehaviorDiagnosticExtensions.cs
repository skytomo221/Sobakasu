using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class BehaviorDiagnosticExtensions
    {
        public static void ReportBehaviorFunctionCannotBePublic(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2309", span,
                "Behavior functions cannot be exported from a module.",
                "Remove `public` from this function."));

        public static void ReportBehaviorFunctionCannotBeExternal(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2310", span,
                "Behavior functions cannot have an external binding.",
                "Use a module function for the external binding."));

        public static void ReportStateCapabilityOutsideBehavior(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2311", span,
                "The `state` capability can only be declared in a behavior callable.",
                "Move this callable into a `behavior` block."));

        public static void ReportDuplicateStateBlock(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2300", span,
                "Only one `state` block is allowed in an entry script.",
                "Combine the state members into one block."));

        public static void ReportFieldRequiresPublic(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2301", span,
                "A `field` initializer requires `public`.",
                "Add `public` before this state member."));

        public static void ReportFieldRequiresExplicitType(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2302", span,
                "A `field` initializer requires an explicit type.",
                "Add `: Type` before `= field`."));

        public static void ReportStateCapabilityRequired(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2303", span,
                "This operation requires the `state` capability.",
                "Declare `state` first in the behavior callable's parameter list."));

        public static void ReportUnknownStateMember(this DiagnosticBag diagnostics, TextSpan span, string name) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2304", span,
                $"Unknown state member '{name}'.",
                "Declare it in the `state` block."));

        public static void ReportStateMemberIsNotCallable(this DiagnosticBag diagnostics, TextSpan span, string name) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2317", span,
                $"State member `{name}` is not callable.",
                "Call a state receiver function or use the member as a value."));

        public static void ReportStateReceiverIsNotRuntimeValue(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2312", span,
                "`state` is a behavior receiver, not a runtime value.",
                "Use `state.member`, `state.method(...)`, or `behavior::method(state, ...)`."));

        public static void ReportInteractRequiresStateReceiver(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2313", span,
                "The `interact` event requires the `state` receiver.",
                "Declare it as `on interact(state)`."));

        public static void ReportBehaviorFunctionRequiresStateReceiver(this DiagnosticBag diagnostics, TextSpan span, string name) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2314", span,
                $"Behavior function `{name}` requires a `state` receiver.",
                $"Use `state.{name}(...)` or `behavior::{name}(state, ...)`."));

        public static void ReportBehaviorFunctionHasNoStateReceiver(this DiagnosticBag diagnostics, TextSpan span, string name) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2315", span,
                $"Behavior function `{name}` has no `state` receiver.",
                $"Use `behavior::{name}(...)`."));

        public static void ReportStateMemberMethodNameConflict(this DiagnosticBag diagnostics, TextSpan span, string name) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2316", span,
                $"State member `{name}` conflicts with a zero-argument state receiver function.",
                "Rename the state member or give the state receiver function a runtime parameter."));

        public static void ReportSendOutsideBehavior(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2306", span,
                "`send` is only allowed in a behavior function, event, or network receiver.",
                "Move the send statement into a `behavior` block."));

        public static void ReportStateBlockOutsideEntry(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2307", span,
                "A `state` block is only allowed in the entry script.",
                "Move the state block into the entry script."));

        public static void ReportBehaviorOutsideEntry(this DiagnosticBag diagnostics, TextSpan span) =>
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2308", span,
                "A `behavior` block is only allowed in the entry script.",
                "Move the behavior block into the entry script."));
    }
}
