using Skytomo221.Sobakasu.Compiler.Assembly;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal static class SobakasuUasmEmitter
  {
    public static string Emit(AssemblyProgram program)
    {
      var assembler = new SobakasuUasmAssembler();
      return assembler.Assemble(program);
    }
  }
}
