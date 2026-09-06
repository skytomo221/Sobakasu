using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundUserFunctionCallExpression : BoundExpression
    {
        public FunctionSymbol Function { get; }
        public BoundExpression Receiver { get; }
        public IReadOnlyList<BoundExpression> Arguments { get; }
        public override TypeSymbol Type => Function.ReturnType;

        public BoundUserFunctionCallExpression(
            FunctionSymbol function,
            IReadOnlyList<BoundExpression> arguments,
            BoundExpression receiver = null)
        {
            Function = function ?? throw new ArgumentNullException(nameof(function));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            Receiver = receiver;
        }
    }
}
