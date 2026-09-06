using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundVariableDeclarationStatement : BoundStatement
    {
        public LocalVariableSymbol Variable { get; }
        public BoundExpression Initializer { get; }

        public BoundVariableDeclarationStatement(
            LocalVariableSymbol variable,
            BoundExpression initializer)
        {
            Variable = variable ?? throw new ArgumentNullException(nameof(variable));
            Initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
        }
    }
}
