using System;

using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    /// <summary>
    /// Base for lowering responsibilities which participate in one compilation.
    /// Components intentionally share only invocation-scoped state through the
    /// session; no lowerer instance is reused between compilations.
    /// </summary>
    internal abstract class LoweringComponent
    {
        protected LoweringComponent(LoweringSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        protected LoweringSession Session { get; }
        protected DiagnosticBag Diagnostics => Session.Diagnostics;

        protected IrValue LowerValueExpression(BoundExpression expression, LoweringContext context, TypeSymbol expectedType = null) =>
            Session.ExpressionLowerer.Lower(expression, context, expectedType);

        protected void LowerBlock(BoundBlockStatement block, LoweringContext context) =>
            Session.StatementLowerer.LowerBlock(block, context);

        protected void LowerExpressionForEffect(BoundExpression expression, LoweringContext context) =>
            Session.StatementLowerer.LowerExpressionForEffect(expression, context);

        protected bool IsAggregateStorageType(TypeSymbol type) =>
            Session.AggregateStorageLowerer.IsAggregateStorageType(type);

        protected IReadOnlyList<IrValue> GetAggregateLeaves(IrValue value) =>
            Session.AggregateStorageLowerer.GetAggregateLeaves(value);
    }
}
