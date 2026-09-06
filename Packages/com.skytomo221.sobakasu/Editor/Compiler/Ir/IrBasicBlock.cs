using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Ir
{
    internal sealed class IrBasicBlock
    {
        private readonly List<IrInstruction> _instructions = new();
        public string Label { get; }
        public IReadOnlyList<IrInstruction> Instructions => _instructions;
        public IrTerminator Terminator { get; private set; }
        public IrBasicBlock(string label) { Label = label ?? throw new ArgumentNullException(nameof(label)); }
        public void AddInstruction(IrInstruction instruction)
        {
            if (instruction == null) throw new ArgumentNullException(nameof(instruction));
            _instructions.Add(instruction);
        }
        public void SetTerminator(IrTerminator terminator) { Terminator = terminator ?? throw new ArgumentNullException(nameof(terminator)); }
    }
}
