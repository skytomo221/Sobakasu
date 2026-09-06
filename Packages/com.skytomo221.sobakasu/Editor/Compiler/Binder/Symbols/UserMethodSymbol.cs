using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class UserMethodSymbol : MethodSymbol
    {
        public FunctionSymbol Function { get; }

        public UserMethodSymbol(FunctionSymbol function)
            : base(
                function?.Name ?? throw new ArgumentNullException(nameof(function)),
                function.ContainingType,
                function.Parameters,
                function.ReturnType,
                function.IsStatic)
        {
            Function = function;
        }
    }
}
