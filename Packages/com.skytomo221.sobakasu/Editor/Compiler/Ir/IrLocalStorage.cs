using System;
using Skytomo221.Sobakasu.Compiler.Binder;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrLocalStorage : IrStorage { public LocalVariableSymbol Variable { get; } public IrLocalStorage(LocalVariableSymbol variable) : base(variable?.Type ?? throw new ArgumentNullException(nameof(variable))) { Variable = variable; } } }
