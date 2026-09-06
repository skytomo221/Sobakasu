

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundReturnStatement : BoundStatement
    {
        public BoundExpression Expression { get; }

        public BoundReturnStatement(BoundExpression expression)
        {
            Expression = expression;
        }
    }
}
