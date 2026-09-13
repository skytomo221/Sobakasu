using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class LoweringSession
    {
        internal LoweringSession()
        {
            ProgramLowerer = new ProgramLowerer(this);
            StoragePlanner = new StoragePlanner(this);
            StatementLowerer = new StatementLowerer(this);
            ExpressionLowerer = new ExpressionLowerer(this);
            AssignmentLowerer = new AssignmentLowerer(this);
            OperatorLowerer = new OperatorLowerer(this);
            ControlFlowLowerer = new ControlFlowLowerer(this);
            CallLowerer = new CallLowerer(this);
            AggregateLowerer = new AggregateLowerer(this);
            ArrayLowerer = new ArrayLowerer(this);
            AggregateStorageLowerer = new AggregateStorageLowerer();
            NetworkLowerer = new NetworkLowerer(this);
        }

        internal DiagnosticBag Diagnostics { get; } = new();
        internal Dictionary<FunctionSymbol, BoundFunctionDeclaration> Functions { get; } = new();
        internal Dictionary<StateVariableSymbol, IrStorage> StateStorageMap { get; } = new();
        internal IReadOnlyDictionary<StateVariableSymbol, IrStorage> StateStorage => StateStorageMap;
        internal ProgramLowerer ProgramLowerer { get; }
        internal StoragePlanner StoragePlanner { get; }
        internal StatementLowerer StatementLowerer { get; }
        internal ExpressionLowerer ExpressionLowerer { get; }
        internal AssignmentLowerer AssignmentLowerer { get; }
        internal OperatorLowerer OperatorLowerer { get; }
        internal ControlFlowLowerer ControlFlowLowerer { get; }
        internal CallLowerer CallLowerer { get; }
        internal AggregateLowerer AggregateLowerer { get; }
        internal ArrayLowerer ArrayLowerer { get; }
        internal AggregateStorageLowerer AggregateStorageLowerer { get; }
        internal NetworkLowerer NetworkLowerer { get; }

        internal void Initialize(BoundProgram program)
        {
            Functions.Clear();
            StateStorageMap.Clear();
            foreach (var function in program.Functions)
                Functions.Add(function.FunctionSymbol, function);
        }
    }

    internal sealed class SobakasuIrLowerer
    {
        private DiagnosticBag _diagnostics = new();

        public DiagnosticBag Diagnostics => _diagnostics;

        public IrProgram Lower(BoundProgram program)
        {
            var session = new LoweringSession();
            _diagnostics = session.Diagnostics;
            return session.ProgramLowerer.Lower(program);
        }
    }
}
