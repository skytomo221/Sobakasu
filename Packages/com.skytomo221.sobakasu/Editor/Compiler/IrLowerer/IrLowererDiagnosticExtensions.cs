using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    public static class IrLowererDiagnosticExtensions
    {
        public static void ReportLoweringError(this DiagnosticBag diagnostics, string message)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK3001",
                new TextSpan(0, 0),
                message,
                "Fix the lowering issue before generating UASM."
            ));
        }
    }
}

