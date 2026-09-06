using System;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrJumpTerminator : IrTerminator { public string TargetLabel { get; } public IrJumpTerminator(string targetLabel) { TargetLabel = targetLabel ?? throw new ArgumentNullException(nameof(targetLabel)); } } }
