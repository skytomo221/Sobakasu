using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class IrModuleBuilder
    {
        private int _nextBlockId = 1;
        private int _nextTemporaryId;

        public IrModuleBuilder(string entryLabel)
        {
            var entryBlock = new IrBasicBlock(entryLabel);
            Blocks.Add(entryBlock);
            CurrentBlock = entryBlock;
        }

        public List<IrBasicBlock> Blocks { get; } = new();
        public IrBasicBlock CurrentBlock { get; private set; }
        public IrBasicBlock CreateBlock(string prefix)
        {
            var block = new IrBasicBlock($"__{prefix}_{_nextBlockId}");
            _nextBlockId++;
            Blocks.Add(block);
            return block;
        }

        public IrStorage CreateTemporary(TypeSymbol type)
        {
            var temporary = new IrTemporaryStorage(_nextTemporaryId, type);
            _nextTemporaryId++;
            return temporary;
        }

        public void Emit(IrInstruction instruction)
        {
            CurrentBlock.AddInstruction(instruction);
        }

        public void TerminateWithJump(string targetLabel)
        {
            CurrentBlock.SetTerminator(new IrJumpTerminator(targetLabel));
        }

        public void TerminateWithCondition(
            IrValue condition,
            string trueLabel,
            string falseLabel)
        {
            CurrentBlock.SetTerminator(
                new IrConditionalJumpTerminator(condition, trueLabel, falseLabel));
        }

        public void SwitchTo(IrBasicBlock block)
        {
            CurrentBlock = block ?? throw new ArgumentNullException(nameof(block));
        }

        public void RemoveBlock(IrBasicBlock block)
        {
            if (ReferenceEquals(CurrentBlock, block))
            {
                throw new InvalidOperationException(
                    "Cannot remove the active IR basic block.");
            }

            Blocks.Remove(block);
        }
    }
}
