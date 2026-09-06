using System;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrCopyInstruction : IrInstruction { public IrStorage Target { get; } public IrValue Source { get; } public IrCopyInstruction(IrStorage target, IrValue source) { Target = target ?? throw new ArgumentNullException(nameof(target)); Source = source ?? throw new ArgumentNullException(nameof(source)); } } }
