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
            EventLoweringContext context)
        {
            if (_localStorage.TryGetValue(variable, out var storage))
                return storage;

            storage = context.CreateTemporary(variable.Type);
            _localStorage.Add(variable, storage);
            return storage;
        }
    }

    internal sealed class EventLoweringContext
    {
        private int _nextBlockId = 1;
        private int _nextTemporaryId;
        private readonly Stack<InlineFunctionFrame> _inlineFrames = new();
        private readonly List<LoopLoweringFrame> _loops = new();
        private readonly IReadOnlyDictionary<StateVariableSymbol, IrStorage> _stateStorage;
        private readonly Dictionary<LocalVariableSymbol, IrStorage> _aggregateLocalStorage = new();
        private readonly IReadOnlyDictionary<ParameterSymbol, IrStorage> _entryParameterStorage;

        public EventLoweringContext(
            BoundEventSymbol eventSymbol,
            IReadOnlyDictionary<StateVariableSymbol, IrStorage> stateStorage)
        {
            if (eventSymbol == null)
                throw new ArgumentNullException(nameof(eventSymbol));
            EntrySourceName = eventSymbol.SourceName;
            EntryReturnType = eventSymbol.ReturnType;
            ReturnValueStorageName = eventSymbol.ReturnValueStorageName;
            _stateStorage = stateStorage ?? throw new ArgumentNullException(nameof(stateStorage));
            _entryParameterStorage = new Dictionary<ParameterSymbol, IrStorage>();
            var entryBlock = new IrBasicBlock(eventSymbol.UdonName);
            Blocks.Add(entryBlock);
            CurrentBlock = entryBlock;
        }

        public EventLoweringContext(
            NetworkReceiveSymbol receiveSymbol,
            IReadOnlyDictionary<StateVariableSymbol, IrStorage> stateStorage,
            IReadOnlyDictionary<ParameterSymbol, IrStorage> parameterStorage)
        {
            if (receiveSymbol == null)
                throw new ArgumentNullException(nameof(receiveSymbol));
            EntrySourceName = receiveSymbol.Name;
            EntryReturnType = TypeSymbol.Unit;
            ReturnValueStorageName = null;
            _stateStorage = stateStorage ?? throw new ArgumentNullException(nameof(stateStorage));
            _entryParameterStorage = parameterStorage ??
                throw new ArgumentNullException(nameof(parameterStorage));
            var entryBlock = new IrBasicBlock(receiveSymbol.ExportName);
            Blocks.Add(entryBlock);
            CurrentBlock = entryBlock;
        }

        public string EntrySourceName { get; }
        public TypeSymbol EntryReturnType { get; }
        public string ReturnValueStorageName { get; }
        public List<IrBasicBlock> Blocks { get; } = new();
        public IrBasicBlock CurrentBlock { get; private set; }
        public bool IsInsideInlineFunction => _inlineFrames.Count > 0;
        public FunctionSymbol CurrentInlineFunction => _inlineFrames.Peek().Function;
        public string CurrentInlineEndLabel => _inlineFrames.Peek().EndLabel;
        public IrStorage CurrentInlineResultStorage => _inlineFrames.Peek().ResultStorage;

        public IrBasicBlock CreateBlock(string prefix)
        {
            var block = new IrBasicBlock($"__{prefix}_{_nextBlockId}");
            _nextBlockId++;
            Blocks.Add(block);
            return block;
        }

        public IrStorage CreateTemporary(TypeSymbol type)
        {
            if (IrLoweringEngine.IsAggregateStorageType(type))
            {
                var leaves = new List<IrStorage>();
                foreach (var descriptor in AggregateLayout.GetLeaves(type))
                {
                    leaves.Add(new IrTemporaryStorage(_nextTemporaryId, descriptor.Type));
                    _nextTemporaryId++;
                }
                return new IrAggregateStorage(type, leaves);
            }

            var temporary = new IrTemporaryStorage(_nextTemporaryId, type);
            _nextTemporaryId++;
            return temporary;
        }

        public IrStorage GetLocalStorage(LocalVariableSymbol variable)
        {
            foreach (var frame in _inlineFrames)
            {
                if (frame.TryGetLocalStorage(variable, out var storage))
                    return storage;
            }

            if (_inlineFrames.Count > 0)
                return _inlineFrames.Peek().GetOrCreateLocalStorage(variable, this);

            if (!IrLoweringEngine.IsAggregateStorageType(variable.Type))
                return new IrLocalStorage(variable);

            if (_aggregateLocalStorage.TryGetValue(variable, out var aggregateStorage))
                return aggregateStorage;

            aggregateStorage = CreateTemporary(variable.Type);
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

        public void Emit(IrInstruction instruction)
        {
            CurrentBlock.AddInstruction(instruction);
        }

        public void EmitCopy(IrStorage target, IrValue source)
        {
            if (target is not IrAggregateStorage aggregateTarget)
            {
                Emit(new IrCopyInstruction(target, source));
                return;
            }

            var sourceLeaves = IrLoweringEngine.GetAggregateLeaves(source);
            var descriptors = AggregateLayout.GetLeaves(target.Type);
            for (var pass = 0; pass < 2; pass++)
            {
                for (var index = 0; index < aggregateTarget.Leaves.Count; index++)
                {
                    var isTag = index < descriptors.Count && descriptors[index].IsEnumTag;
                    if ((pass == 0 && isTag) || (pass == 1 && !isTag))
                        continue;
                    if (index >= sourceLeaves.Count || sourceLeaves[index] == null)
                        continue;
                    Emit(new IrCopyInstruction(
                        aggregateTarget.Leaves[index],
                        sourceLeaves[index]));
                }
            }
        }

        public void TerminateWithJump(string targetLabel)
        {
            CurrentBlock.SetTerminator(new IrJumpTerminator(targetLabel));
        }

        public void TerminateWithCondition(
            IrValue condition,
            string trueLabel,
            string falseLabel)
        {
            CurrentBlock.SetTerminator(
                new IrConditionalJumpTerminator(condition, trueLabel, falseLabel));
        }

        public void SwitchTo(IrBasicBlock block)
        {
            CurrentBlock = block ?? throw new ArgumentNullException(nameof(block));
        }

        public void RemoveBlock(IrBasicBlock block)
        {
            if (ReferenceEquals(CurrentBlock, block))
            {
                throw new InvalidOperationException(
                    "Cannot remove the active IR basic block.");
            }

            Blocks.Remove(block);
        }
    }
}

