using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundAssignmentExpression : BoundExpression
    {
        public VariableSymbol Variable { get; }
        public BoundExpression Expression { get; }
        public BoundExpression Target { get; }
        public override TypeSymbol Type => Target.Type;

        public BoundAssignmentExpression(BoundNameExpression target, BoundExpression expression)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            Variable = target.Symbol as VariableSymbol;
        }

        public BoundAssignmentExpression(
            VariableSymbol variable,
            BoundExpression expression)
        {
            Variable = variable ?? throw new ArgumentNullException(nameof(variable));
            Target = new BoundNameExpression(variable.Name, variable, variable.Type);
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
    }
}
