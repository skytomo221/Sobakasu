using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.UasmAssembler
{
    public static class UasmAssemblerDiagnosticExtensions
    {
        public static void ReportAssemblerError(this DiagnosticBag diagnostics, string message)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK5001",
                new TextSpan(0, 0),
                message,
                "Fix the assembler issue before using the generated UASM."
            ));
        }
    }
}

