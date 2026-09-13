using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class ProgramLowerer : LoweringComponent
    {
        public ProgramLowerer(LoweringSession session)
            : base(session)
        {
        }

        internal IrProgram Lower(BoundProgram program)
        {
            Session.Initialize(program);
            var states = Session.StoragePlanner.Plan(program.States);
            var modules = new List<IrModule>();

            foreach (var @event in program.Events)
            {
                var context = new LoweringContext(
                    @event.EventSymbol,
                    Session.StateStorage,
                    Session.AggregateStorageLowerer);
                Session.StatementLowerer.LowerBlock(@event.Body, context);
                CompleteModule(context);
                modules.Add(new IrModule(@event.EventSymbol, context.Blocks));
            }

            foreach (var receiver in program.NetworkReceivers)
            {
                var context = new LoweringContext(
                    receiver.ReceiveSymbol,
                    Session.StateStorage,
                    Session.StoragePlanner.CreateNetworkReceiveParameterStorage(receiver.ReceiveSymbol),
                    Session.AggregateStorageLowerer);
                Session.StatementLowerer.LowerBlock(receiver.Body, context);
                CompleteModule(context);
                modules.Add(new IrModule(receiver.ReceiveSymbol, context.Blocks));
            }

            return new IrProgram(states, modules);
        }

        private static void CompleteModule(LoweringContext context)
        {
            if (context.CurrentBlock.Terminator == null)
                context.CurrentBlock.SetTerminator(new IrReturnTerminator());
        }
    }
}
