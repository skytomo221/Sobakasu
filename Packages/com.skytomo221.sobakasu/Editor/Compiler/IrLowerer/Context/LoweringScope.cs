using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class LoweringScope
    {
        private readonly Stack<InlineFunctionFrame> _inlineFrames = new();
        private readonly List<LoopLoweringFrame> _loops = new();
        private readonly IReadOnlyDictionary<StateVariableSymbol, IrStorage> _stateStorage;
        private readonly Dictionary<LocalVariableSymbol, IrStorage> _aggregateLocalStorage = new();
        private readonly IReadOnlyDictionary<ParameterSymbol, IrStorage> _entryParameterStorage;
        private readonly IrModuleBuilder _builder;
        private readonly AggregateStorageLowerer _aggregateStorageLowerer;

        public LoweringScope(
            IrModuleBuilder builder,
            IReadOnlyDictionary<StateVariableSymbol, IrStorage> stateStorage,
            IReadOnlyDictionary<ParameterSymbol, IrStorage> entryParameterStorage,
            AggregateStorageLowerer aggregateStorageLowerer)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _stateStorage = stateStorage ?? throw new ArgumentNullException(nameof(stateStorage));
            _entryParameterStorage = entryParameterStorage ?? throw new ArgumentNullException(nameof(entryParameterStorage));
            _aggregateStorageLowerer = aggregateStorageLowerer ??
                throw new ArgumentNullException(nameof(aggregateStorageLowerer));
        }

        public bool IsInsideInlineFunction => _inlineFrames.Count > 0;
        public FunctionSymbol CurrentInlineFunction => _inlineFrames.Peek().Function;
        public string CurrentInlineEndLabel => _inlineFrames.Peek().EndLabel;
        public IrStorage CurrentInlineResultStorage => _inlineFrames.Peek().ResultStorage;
        public IrStorage GetLocalStorage(LocalVariableSymbol variable)
        {
            foreach (var frame in _inlineFrames)
            {
                if (frame.TryGetLocalStorage(variable, out var storage))
                    return storage;
            }

            if (_inlineFrames.Count > 0)
                return _inlineFrames.Peek().GetOrCreateLocalStorage(
                    variable,
                    _builder,
                    _aggregateStorageLowerer);

            if (!_aggregateStorageLowerer.IsAggregateStorageType(variable.Type))
                return new IrLocalStorage(variable);

            if (_aggregateLocalStorage.TryGetValue(variable, out var aggregateStorage))
                return aggregateStorage;

            aggregateStorage = _aggregateStorageLowerer.CreateTemporary(variable.Type, _builder);
            _aggregateLocalStorage.Add(variable, aggregateStorage);
            return aggregateStorage;
        }

        public IrStorage GetVariableStorage(VariableSymbol variable)
        {
            return variable switch
            {
                LocalVariableSymbol local => GetLocalStorage(local),
                StateVariableSymbol state when _stateStorage.TryGetValue(state, out var storage) => storage,
                _ => throw new InvalidOperationException(
                    $"Unsupported variable storage '{variable?.GetType().Name ?? "<null>"}'.")
            };
        }

        public IrStorage GetParameterStorage(ParameterSymbol parameter)
        {
            foreach (var frame in _inlineFrames)
            {
                if (frame.TryGetParameterStorage(parameter, out var storage))
                    return storage;
            }

            if (_entryParameterStorage.TryGetValue(parameter, out var entryStorage))
                return entryStorage;

            return new IrParameterStorage(parameter);
        }

        public void PushInlineFrame(InlineFunctionFrame frame)
        {
            _inlineFrames.Push(frame ?? throw new ArgumentNullException(nameof(frame)));
        }

        public void PopInlineFrame()
        {
            _inlineFrames.Pop();
        }

        public void MarkInlineEndIncoming()
        {
            _inlineFrames.Peek().HasEndIncoming = true;
        }

        public void PushLoop(LoopLoweringFrame loop)
        {
            _loops.Add(loop ?? throw new ArgumentNullException(nameof(loop)));
        }

        public void PopLoop(LoopSymbol symbol)
        {
            if (_loops.Count == 0 ||
                !ReferenceEquals(_loops[^1].Loop, symbol))
            {
                throw new InvalidOperationException("Loop lowering contexts became unbalanced.");
            }

            _loops.RemoveAt(_loops.Count - 1);
        }

        public LoopLoweringFrame FindLoop(LoopSymbol symbol)
        {
            for (var index = _loops.Count - 1; index >= 0; index--)
            {
                if (ReferenceEquals(_loops[index].Loop, symbol))
                    return _loops[index];
            }

            return null;
        }

    }
}
