using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler.Ir
{
    internal sealed class IrProgram
    {
        public IReadOnlyList<StateVariableSymbol> States { get; }
        public IReadOnlyList<IrModule> Modules { get; }

        public IrProgram(IReadOnlyList<StateVariableSymbol> states, IReadOnlyList<IrModule> modules)
        {
            States = states ?? throw new ArgumentNullException(nameof(states));
            Modules = modules ?? throw new ArgumentNullException(nameof(modules));
        }
    }
}
