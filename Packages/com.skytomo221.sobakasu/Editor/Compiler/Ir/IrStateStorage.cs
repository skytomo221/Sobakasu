using System;
using Skytomo221.Sobakasu.Compiler.Binder;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrStateStorage : IrStorage { public StateVariableSymbol State { get; } public IrStateStorage(StateVariableSymbol state) : base(state?.Type ?? throw new ArgumentNullException(nameof(state))) { State = state; } } }
