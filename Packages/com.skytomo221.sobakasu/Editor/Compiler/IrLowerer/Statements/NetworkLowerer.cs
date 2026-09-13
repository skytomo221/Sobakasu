using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class NetworkLowerer : LoweringComponent
    {
        public NetworkLowerer(LoweringSession session) : base(session) { }

        internal void LowerSend(BoundNetworkSendStatement statement, LoweringContext context)
        {
            var physicalArguments = new List<IrValue>();
            for (var index = 0; index < statement.Arguments.Count; index++)
            {
                var expectedType = index < statement.Receiver.Parameters.Count
                    ? statement.Receiver.Parameters[index].Type
                    : statement.Arguments[index].Type;
                var value = LowerValueExpression(statement.Arguments[index], context, expectedType);
                if (value == null)
                    return;

                if (expectedType.UsesFlattenedAggregateStorage &&
                    (expectedType.AggregateKind == UserAggregateKind.Struct ||
                     expectedType.AggregateKind == UserAggregateKind.Tuple))
                    physicalArguments.AddRange(
                        Session.AggregateStorageLowerer.GetAggregateLeaves(value));
                else
                    physicalArguments.Add(value);
            }

            var target = LowerValueExpression(statement.Target, context, statement.Target.Type);
            if (target == null)
                return;

            var arguments = new List<IrValue>(physicalArguments.Count + 3)
            {
                new IrThisValue(statement.CurrentBehaviourType),
                target,
                new IrConstantValue(statement.Receiver.ExportName, TypeSymbol.String, statement.Receiver.SourceSpan)
            };
            arguments.AddRange(physicalArguments);
            context.Emit(new IrExternCallInstruction(statement.ExternSignature, arguments, null));
        }
    }
}
