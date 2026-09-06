using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class SobakasuIrLowerer
    {
        private readonly IrLoweringEngine _engine = new();

        public DiagnosticBag Diagnostics => _engine.Diagnostics;

        public IrProgram Lower(BoundProgram program)
        {
            return _engine.Lower(program);
        }
    }
}
