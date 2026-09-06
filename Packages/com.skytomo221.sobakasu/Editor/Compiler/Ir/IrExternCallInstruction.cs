using System;
using System.Collections.Generic;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrExternCallInstruction : IrInstruction { public string ExternSignature { get; } public IReadOnlyList<IrValue> Arguments { get; } public IrStorage Result { get; } public IrExternCallInstruction(string externSignature, IReadOnlyList<IrValue> arguments, IrStorage result) { ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature)); Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments)); Result = result; } } }
