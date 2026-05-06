using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal static class SobakasuUasmEmitter
  {
    public static string Emit(IrProgram program)
    {
      var assembler = new SobakasuUasmAssembler();
      return assembler.Assemble(program);
    }
  }
}
