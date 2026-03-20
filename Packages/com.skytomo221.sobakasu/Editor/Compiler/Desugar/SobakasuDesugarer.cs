using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Desugar
{
  internal sealed class SobakasuDesugarer
  {
    public DiagnosticBag Diagnostics { get; } = new();

    public BoundProgram Desugar(BoundProgram program)
    {
      // Current supported syntax has no sugar yet.
      return program;
    }
  }
}
