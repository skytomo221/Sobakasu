using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Assembly
{
  internal enum InstructionKind
  {
    Nop,
    Push,
    Pop,
    Copy,
    Jump,
    JumpIfFalse,
    Extern,
    ExternSet,
    ExternGet,
    JumpIndirect,
    Return,
    Comment,
    ExportTag,
    SyncTag,
  }

  internal sealed class AssemblyInstruction
  {
    public InstructionKind Kind { get; }
    public IReadOnlyList<string> Operands { get; }

    public AssemblyInstruction(InstructionKind kind, params string[] operands)
    {
      Kind = kind;
      Operands = operands ?? Array.Empty<string>();
    }
  }
}
