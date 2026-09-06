using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundStateDeclaration : BoundNode
    {
        public StateVariableSymbol StateSymbol { get; }
        public BoundExpression Initializer { get; }

        public BoundStateDeclaration(
            StateVariableSymbol stateSymbol,
            BoundExpression initializer)
        {
            StateSymbol = stateSymbol ?? throw new ArgumentNullException(nameof(stateSymbol));
            Initializer = initializer;
        }
    }
}
