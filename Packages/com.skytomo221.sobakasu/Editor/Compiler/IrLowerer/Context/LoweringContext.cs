using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class LoopLoweringFrame
    {
        public LoopLoweringFrame(
            LoopSymbol loop,
            string breakTarget,
            string continueTarget,
            string redoTarget,
            IrStorage resultStorage)
        {
            Loop = loop ?? throw new ArgumentNullException(nameof(loop));
            BreakTarget = breakTarget;
            ContinueTarget = continueTarget ??
                throw new ArgumentNullException(nameof(continueTarget));
            RedoTarget = redoTarget ??
                throw new ArgumentNullException(nameof(redoTarget));
            ResultStorage = resultStorage;
        }

        public LoopSymbol Loop { get; }
        public string BreakTarget { get; }
        public string ContinueTarget { get; }
        public string RedoTarget { get; }
        public IrStorage ResultStorage { get; }
    }

    internal sealed class InlineFunctionFrame
    {
        private readonly Dictionary<ParameterSymbol, IrStorage> _parameterStorage = new();
        private readonly Dictionary<LocalVariableSymbol, IrStorage> _localStorage = new();

        public InlineFunctionFrame(
            FunctionSymbol function,
            string endLabel,
            IrStorage resultStorage)
        {
            Function = function ?? throw new ArgumentNullException(nameof(function));
            EndLabel = endLabel ?? throw new ArgumentNullException(nameof(endLabel));
            ResultStorage = resultStorage;
        }

        public FunctionSymbol Function { get; }
        public string EndLabel { get; }
        public IrStorage ResultStorage { get; }
        public bool HasEndIncoming { get; set; }

        public void SetParameterStorage(ParameterSymbol parameter, IrStorage storage)
        {
            _parameterStorage[parameter] = storage;
        }

        public bool TryGetParameterStorage(ParameterSymbol parameter, out IrStorage storage)
        {
            return _parameterStorage.TryGetValue(parameter, out storage);
        }

        public bool TryGetLocalStorage(LocalVariableSymbol variable, out IrStorage storage)
        {
            return _localStorage.TryGetValue(variable, out storage);
        }

        public IrStorage GetOrCreateLocalStorage(
            LocalVariableSymbol variable,
            IrModuleBuilder builder,
            AggregateStorageLowerer aggregateStorageLowerer)
        {
            if (_localStorage.TryGetValue(variable, out var storage))
                return storage;

            storage = aggregateStorageLowerer.CreateTemporary(variable.Type, builder);
            _localStorage.Add(variable, storage);
            return storage;
        }
    }


    internal sealed class LoweringContext
    {
        private readonly IrModuleBuilder _builder;
        private readonly LoweringScope _scope;
        private readonly AggregateStorageLowerer _aggregateStorageLowerer;

        public LoweringContext(
            BoundEventSymbol eventSymbol,
            IReadOnlyDictionary<StateVariableSymbol, IrStorage> stateStorage,
            AggregateStorageLowerer aggregateStorageLowerer)
        {
            if (eventSymbol == null) throw new ArgumentNullException(nameof(eventSymbol));
            EntrySourceName = eventSymbol.SourceName;
            EntryReturnType = eventSymbol.ReturnType;
            ReturnValueStorageName = eventSymbol.ReturnValueStorageName;
            _builder = new IrModuleBuilder(eventSymbol.UdonName);
            _aggregateStorageLowerer = aggregateStorageLowerer ??
                throw new ArgumentNullException(nameof(aggregateStorageLowerer));
            _scope = new LoweringScope(
                _builder,
                stateStorage,
                new Dictionary<ParameterSymbol, IrStorage>(),
                _aggregateStorageLowerer);
        }

        public LoweringContext(
            NetworkReceiveSymbol receiveSymbol,
            IReadOnlyDictionary<StateVariableSymbol, IrStorage> stateStorage,
            IReadOnlyDictionary<ParameterSymbol, IrStorage> parameterStorage,
            AggregateStorageLowerer aggregateStorageLowerer)
        {
            if (receiveSymbol == null) throw new ArgumentNullException(nameof(receiveSymbol));
            EntrySourceName = receiveSymbol.Name;
            EntryReturnType = TypeSymbol.Unit;
            _builder = new IrModuleBuilder(receiveSymbol.ExportName);
            _aggregateStorageLowerer = aggregateStorageLowerer ??
                throw new ArgumentNullException(nameof(aggregateStorageLowerer));
            _scope = new LoweringScope(
                _builder,
                stateStorage,
                parameterStorage,
                _aggregateStorageLowerer);
        }

        public string EntrySourceName { get; }
        public TypeSymbol EntryReturnType { get; }
        public string ReturnValueStorageName { get; }
        public List<IrBasicBlock> Blocks => _builder.Blocks;
        public IrBasicBlock CurrentBlock => _builder.CurrentBlock;
        public bool IsInsideInlineFunction => _scope.IsInsideInlineFunction;
        public FunctionSymbol CurrentInlineFunction => _scope.CurrentInlineFunction;
        public string CurrentInlineEndLabel => _scope.CurrentInlineEndLabel;
        public IrStorage CurrentInlineResultStorage => _scope.CurrentInlineResultStorage;
        public IrBasicBlock CreateBlock(string prefix) => _builder.CreateBlock(prefix);
        public IrStorage CreateTemporary(TypeSymbol type) =>
            _aggregateStorageLowerer.CreateTemporary(type, _builder);
        public IrStorage GetLocalStorage(LocalVariableSymbol variable) => _scope.GetLocalStorage(variable);
        public IrStorage GetVariableStorage(VariableSymbol variable) => _scope.GetVariableStorage(variable);
        public IrStorage GetParameterStorage(ParameterSymbol parameter) => _scope.GetParameterStorage(parameter);
        public void PushInlineFrame(InlineFunctionFrame frame) => _scope.PushInlineFrame(frame);
        public void PopInlineFrame() => _scope.PopInlineFrame();
        public void MarkInlineEndIncoming() => _scope.MarkInlineEndIncoming();
        public void PushLoop(LoopLoweringFrame loop) => _scope.PushLoop(loop);
        public void PopLoop(LoopSymbol symbol) => _scope.PopLoop(symbol);
        public LoopLoweringFrame FindLoop(LoopSymbol symbol) => _scope.FindLoop(symbol);
        public void Emit(IrInstruction instruction) => _builder.Emit(instruction);
        public void EmitCopy(IrStorage target, IrValue source) =>
            _aggregateStorageLowerer.EmitCopy(target, source, _builder);
        public void TerminateWithJump(string targetLabel) => _builder.TerminateWithJump(targetLabel);
        public void TerminateWithCondition(IrValue condition, string trueLabel, string falseLabel) => _builder.TerminateWithCondition(condition, trueLabel, falseLabel);
        public void SwitchTo(IrBasicBlock block) => _builder.SwitchTo(block);
        public void RemoveBlock(IrBasicBlock block) => _builder.RemoveBlock(block);
    }
}
