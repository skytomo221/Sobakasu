using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundFunctionDeclaration : BoundNode
    {
        public FunctionSymbol FunctionSymbol { get; }
        public string Name => FunctionSymbol.Name;
        public BoundBlockStatement Body { get; }

        public BoundFunctionDeclaration(
            FunctionSymbol functionSymbol,
            BoundBlockStatement body)
        {
            FunctionSymbol = functionSymbol ?? throw new ArgumentNullException(nameof(functionSymbol));
            Body = body ?? throw new ArgumentNullException(nameof(body));
        }
    }
}
