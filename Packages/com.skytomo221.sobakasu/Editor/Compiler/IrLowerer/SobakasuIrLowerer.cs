using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Ir
{
  internal sealed class IrProgram
  {
    public IReadOnlyList<IrModule> Modules { get; }

    public IrProgram(IReadOnlyList<IrModule> modules)
    {
      Modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }
  }

  internal sealed class IrModule
  {
    public BoundEventSymbol EventSymbol { get; }
    public string Name { get; }
    public string ExportName { get; }
    public IReadOnlyList<IrBasicBlock> Blocks { get; }

    public IrModule(
        BoundEventSymbol eventSymbol,
        IReadOnlyList<IrBasicBlock> blocks)
    {
      EventSymbol = eventSymbol ?? throw new ArgumentNullException(nameof(eventSymbol));
      Name = eventSymbol.SourceName;
      ExportName = eventSymbol.UdonName;
      Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
    }
  }

  internal sealed class IrBasicBlock
  {
    private readonly List<IrInstruction> _instructions = new();

    public string Label { get; }
    public IReadOnlyList<IrInstruction> Instructions => _instructions;
    public IrTerminator Terminator { get; private set; }

    public IrBasicBlock(string label)
    {
      Label = label ?? throw new ArgumentNullException(nameof(label));
    }

    public void AddInstruction(IrInstruction instruction)
    {
      if (instruction == null)
        throw new ArgumentNullException(nameof(instruction));

      _instructions.Add(instruction);
    }

    public void SetTerminator(IrTerminator terminator)
    {
      Terminator = terminator ?? throw new ArgumentNullException(nameof(terminator));
    }
  }

  internal abstract class IrValue
  {
    protected IrValue(TypeSymbol type)
    {
      Type = type ?? throw new ArgumentNullException(nameof(type));
    }

    public TypeSymbol Type { get; }
  }

  internal abstract class IrStorage : IrValue
  {
    protected IrStorage(TypeSymbol type)
        : base(type)
    {
    }
  }

  internal sealed class IrLocalStorage : IrStorage
  {
    public LocalVariableSymbol Variable { get; }

    public IrLocalStorage(LocalVariableSymbol variable)
        : base(variable?.Type ?? throw new ArgumentNullException(nameof(variable)))
    {
      Variable = variable;
    }
  }

  internal sealed class IrParameterStorage : IrStorage
  {
    public ParameterSymbol Parameter { get; }

    public IrParameterStorage(ParameterSymbol parameter)
        : base(parameter?.Type ?? throw new ArgumentNullException(nameof(parameter)))
    {
      Parameter = parameter;
    }
  }

  internal sealed class IrReturnValueStorage : IrStorage
  {
    public string Name { get; }

    public IrReturnValueStorage(string name)
        : base(TypeSymbol.Object)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
    }
  }

  internal sealed class IrTemporaryStorage : IrStorage
  {
    public int Id { get; }

    public IrTemporaryStorage(int id, TypeSymbol type)
        : base(type)
    {
      Id = id;
    }
  }

  internal sealed class IrConstantValue : IrValue
  {
    public object Value { get; }
    public TextSpan? Span { get; }

    public IrConstantValue(object value, TypeSymbol type, TextSpan? span = null)
        : base(type)
    {
      Value = value;
      Span = span;
    }
  }

  internal abstract class IrInstruction
  {
  }

  internal sealed class IrCopyInstruction : IrInstruction
  {
    public IrStorage Target { get; }
    public IrValue Source { get; }

    public IrCopyInstruction(IrStorage target, IrValue source)
    {
      Target = target ?? throw new ArgumentNullException(nameof(target));
      Source = source ?? throw new ArgumentNullException(nameof(source));
    }
  }

  internal sealed class IrExternCallInstruction : IrInstruction
  {
    public string ExternSignature { get; }
    public IReadOnlyList<IrValue> Arguments { get; }
    public IrStorage Result { get; }

    public IrExternCallInstruction(
        string externSignature,
        IReadOnlyList<IrValue> arguments,
        IrStorage result)
    {
      ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature));
      Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
      Result = result;
    }
  }

  internal abstract class IrTerminator
  {
  }

  internal sealed class IrJumpTerminator : IrTerminator
  {
    public string TargetLabel { get; }

    public IrJumpTerminator(string targetLabel)
    {
      TargetLabel = targetLabel ?? throw new ArgumentNullException(nameof(targetLabel));
    }
  }

  internal sealed class IrConditionalJumpTerminator : IrTerminator
  {
    public IrValue Condition { get; }
    public string TrueLabel { get; }
    public string FalseLabel { get; }

    public IrConditionalJumpTerminator(
        IrValue condition,
        string trueLabel,
        string falseLabel)
    {
      Condition = condition ?? throw new ArgumentNullException(nameof(condition));
      TrueLabel = trueLabel ?? throw new ArgumentNullException(nameof(trueLabel));
      FalseLabel = falseLabel ?? throw new ArgumentNullException(nameof(falseLabel));
    }
  }

  internal sealed class IrReturnTerminator : IrTerminator
  {
  }
}

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
  internal sealed class SobakasuIrLowerer
  {
    private readonly Dictionary<FunctionSymbol, BoundFunctionDeclaration> _functions = new();

    public DiagnosticBag Diagnostics { get; } = new();

    public IrProgram Lower(BoundProgram program)
    {
      _functions.Clear();
      foreach (var function in program.Functions)
        _functions[function.FunctionSymbol] = function;

      var modules = new List<IrModule>();

      foreach (var @event in program.Events)
      {
        var context = new EventLoweringContext(@event.EventSymbol);
        LowerBlock(@event.Body, context);

        if (context.CurrentBlock.Terminator == null)
          context.CurrentBlock.SetTerminator(new IrReturnTerminator());

        modules.Add(new IrModule(@event.EventSymbol, context.Blocks));
      }

      return new IrProgram(modules);
    }

    private void LowerBlock(BoundBlockStatement block, EventLoweringContext context)
    {
      foreach (var statement in block.Statements)
      {
        if (context.CurrentBlock.Terminator != null)
          break;

        LowerStatement(statement, context);
      }
    }

    private void LowerStatement(BoundStatement statement, EventLoweringContext context)
    {
      if (statement is BoundBlockStatement blockStatement)
      {
        LowerBlock(blockStatement, context);
        return;
      }

      if (statement is BoundVariableDeclarationStatement variableDeclarationStatement)
      {
        var source = LowerValueExpression(
            variableDeclarationStatement.Initializer,
            context,
            variableDeclarationStatement.Variable.Type);
        if (source == null)
          return;

        context.Emit(new IrCopyInstruction(
            context.GetLocalStorage(variableDeclarationStatement.Variable),
            source));
        return;
      }

      if (statement is BoundReturnStatement returnStatement)
      {
        LowerReturnStatement(returnStatement, context);
        return;
      }

      if (statement is BoundExpressionStatement expressionStatement)
      {
        LowerExpressionStatement(expressionStatement, context);
        return;
      }

      Diagnostics.ReportLoweringError(
          $"Unsupported bound statement '{statement.GetType().Name}'.");
    }

    private void LowerExpressionStatement(
        BoundExpressionStatement statement,
        EventLoweringContext context)
    {
      if (statement.Expression is BoundErrorExpression)
      {
        Diagnostics.ReportLoweringError(
            "Cannot lower expression that already contains semantic errors.");
        return;
      }

      if (statement.Expression is BoundCallExpression callExpression)
      {
        LowerCallExpression(callExpression, context, preserveResult: false);
        return;
      }

      if (statement.Expression is BoundUserFunctionCallExpression functionCallExpression)
      {
        LowerUserFunctionCallExpression(functionCallExpression, context, preserveResult: false);
        return;
      }

      LowerValueExpression(statement.Expression, context, statement.Expression.Type);
    }

    private IrValue LowerValueExpression(
        BoundExpression expression,
        EventLoweringContext context,
        TypeSymbol expectedType = null)
    {
      switch (expression)
      {
        case BoundLiteralExpression literalExpression:
          return LowerLiteralExpression(literalExpression, expectedType);

        case BoundNameExpression nameExpression
            when nameExpression.Symbol is LocalVariableSymbol local:
          return context.GetLocalStorage(local);

        case BoundNameExpression nameExpression
            when nameExpression.Symbol is ParameterSymbol parameter:
          return context.GetParameterStorage(parameter);

        case BoundUnaryExpression unaryExpression:
          return LowerUnaryExpression(unaryExpression, context);

        case BoundBinaryExpression binaryExpression:
          return binaryExpression.Operator.IsShortCircuit
              ? LowerShortCircuitBinaryExpression(binaryExpression, context)
              : LowerEagerBinaryExpression(binaryExpression, context);

        case BoundCallExpression callExpression:
          return LowerCallExpression(callExpression, context, preserveResult: true);

        case BoundUserFunctionCallExpression functionCallExpression:
          return LowerUserFunctionCallExpression(
              functionCallExpression,
              context,
              preserveResult: true);

        case BoundAssignmentExpression assignmentExpression:
        {
          var source = LowerValueExpression(
              assignmentExpression.Expression,
              context,
              assignmentExpression.Variable.Type);
          if (source == null)
            return null;

          var target = context.GetLocalStorage(assignmentExpression.Variable);
          context.Emit(new IrCopyInstruction(target, source));
          return target;
        }

        case BoundErrorExpression:
          Diagnostics.ReportLoweringError(
              "Cannot lower expression that already contains semantic errors.");
          return null;
      }

      Diagnostics.ReportLoweringError(
          $"Unsupported bound expression '{expression.GetType().Name}'.");
      return null;
    }

    private void LowerReturnStatement(
        BoundReturnStatement statement,
        EventLoweringContext context)
    {
      if (context.IsInsideInlineFunction)
      {
        LowerInlineFunctionReturnStatement(statement, context);
        return;
      }

      if (statement.Expression == null)
      {
        context.CurrentBlock.SetTerminator(new IrReturnTerminator());
        return;
      }

      var value = LowerValueExpression(
          statement.Expression,
          context,
          context.EventSymbol.ReturnType);
      if (value == null)
        return;

      if (string.IsNullOrEmpty(context.EventSymbol.ReturnValueStorageName))
      {
        Diagnostics.ReportLoweringError(
            $"Event '{context.EventSymbol.SourceName}' has a non-void return without a Udon return slot.");
        return;
      }

      context.Emit(new IrCopyInstruction(
          new IrReturnValueStorage(context.EventSymbol.ReturnValueStorageName),
          value));
      context.CurrentBlock.SetTerminator(new IrReturnTerminator());
    }

    private void LowerInlineFunctionReturnStatement(
        BoundReturnStatement statement,
        EventLoweringContext context)
    {
      if (statement.Expression == null)
      {
        context.TerminateWithJump(context.CurrentInlineEndLabel);
        return;
      }

      var resultStorage = context.CurrentInlineResultStorage;
      if (resultStorage == null)
      {
        Diagnostics.ReportLoweringError(
            $"Function '{context.CurrentInlineFunction.Name}' returned a value without a result slot.");
        return;
      }

      var value = LowerValueExpression(
          statement.Expression,
          context,
          context.CurrentInlineFunction.ReturnType);
      if (value == null)
        return;

      context.Emit(new IrCopyInstruction(resultStorage, value));
      context.TerminateWithJump(context.CurrentInlineEndLabel);
    }

    private IrValue LowerLiteralExpression(
        BoundLiteralExpression literalExpression,
        TypeSymbol expectedType)
    {
      if (literalExpression.Type == TypeSymbol.Null)
      {
        if (expectedType == null || !expectedType.IsReferenceType)
        {
          Diagnostics.ReportLoweringError(
              "Null literal requires a concrete reference type during lowering.");
          return null;
        }

        return new IrConstantValue(null, expectedType, literalExpression.Span);
      }

      return new IrConstantValue(
          literalExpression.Value,
          literalExpression.Type,
          literalExpression.Span);
    }

    private IrValue LowerUnaryExpression(
        BoundUnaryExpression expression,
        EventLoweringContext context)
    {
      var operand = LowerValueExpression(
          expression.Operand,
          context,
          expression.Operator.OperandType);
      if (operand == null)
        return null;

      var result = context.CreateTemporary(expression.Type);
      context.Emit(new IrExternCallInstruction(
          expression.Operator.ExternSignature,
          new[] { operand },
          result));
      return result;
    }

    private IrValue LowerEagerBinaryExpression(
        BoundBinaryExpression expression,
        EventLoweringContext context)
    {
      var left = LowerValueExpression(
          expression.Left,
          context,
          expression.Operator.LeftType);
      if (left == null)
        return null;

      var right = LowerValueExpression(
          expression.Right,
          context,
          expression.Operator.RightType);
      if (right == null)
        return null;

      var result = context.CreateTemporary(expression.Type);
      context.Emit(new IrExternCallInstruction(
          expression.Operator.ExternSignature,
          new[] { left, right },
          result));
      return result;
    }

    private IrValue LowerShortCircuitBinaryExpression(
        BoundBinaryExpression expression,
        EventLoweringContext context)
    {
      var left = LowerValueExpression(expression.Left, context, TypeSymbol.Bool);
      if (left == null)
        return null;

      var rhsBlock = context.CreateBlock("logical_rhs");
      var shortCircuitBlock = context.CreateBlock("logical_short");
      var mergeBlock = context.CreateBlock("logical_merge");
      var result = context.CreateTemporary(TypeSymbol.Bool);

      if (expression.Operator.Kind == BoundBinaryOperatorKind.LogicalAnd)
      {
        context.TerminateWithCondition(left, rhsBlock.Label, shortCircuitBlock.Label);
      }
      else
      {
        context.TerminateWithCondition(left, shortCircuitBlock.Label, rhsBlock.Label);
      }

      context.SwitchTo(shortCircuitBlock);
      context.Emit(new IrCopyInstruction(
          result,
          new IrConstantValue(
              expression.Operator.Kind == BoundBinaryOperatorKind.LogicalOr,
              TypeSymbol.Bool,
              null)));
      context.TerminateWithJump(mergeBlock.Label);

      context.SwitchTo(rhsBlock);
      var right = LowerValueExpression(expression.Right, context, TypeSymbol.Bool);
      if (right == null)
        return null;

      context.Emit(new IrCopyInstruction(result, right));
      context.TerminateWithJump(mergeBlock.Label);

      context.SwitchTo(mergeBlock);
      return result;
    }

    private IrValue LowerCallExpression(
        BoundCallExpression callExpression,
        EventLoweringContext context,
        bool preserveResult)
    {
      if (callExpression.Method == null)
      {
        Diagnostics.ReportLoweringError("Cannot lower unresolved method call.");
        return null;
      }

      if (string.IsNullOrEmpty(callExpression.Method.ExternSignature))
      {
        Diagnostics.ReportLoweringError(
            $"No extern signature was selected for '{callExpression.Method.DisplayName}'.");
        return null;
      }

      if (callExpression.Arguments.Count != callExpression.Method.Parameters.Count)
      {
        Diagnostics.ReportLoweringError(
            $"Argument count mismatch for '{callExpression.Method.DisplayName}'.");
        return null;
      }

      var arguments = new IrValue[callExpression.Arguments.Count];
      for (var index = 0; index < callExpression.Arguments.Count; index++)
      {
        arguments[index] = LowerValueExpression(
            callExpression.Arguments[index],
            context,
            callExpression.Method.Parameters[index].Type);
        if (arguments[index] == null)
          return null;
      }

      if (callExpression.Method.ReturnType == TypeSymbol.Void)
      {
        if (preserveResult)
        {
          Diagnostics.ReportLoweringError(
              $"Cannot use void-returning call '{callExpression.Method.DisplayName}' as a value.");
          return null;
        }

        context.Emit(new IrExternCallInstruction(
            callExpression.Method.ExternSignature,
            arguments,
            null));
        return null;
      }

      var result = context.CreateTemporary(callExpression.Method.ReturnType);
      context.Emit(new IrExternCallInstruction(
          callExpression.Method.ExternSignature,
          arguments,
          result));
      return result;
    }

    private IrValue LowerUserFunctionCallExpression(
        BoundUserFunctionCallExpression callExpression,
        EventLoweringContext context,
        bool preserveResult)
    {
      if (!_functions.TryGetValue(callExpression.Function, out var declaration))
      {
        Diagnostics.ReportLoweringError(
            $"Cannot lower unresolved user-defined function '{callExpression.Function.Name}'.");
        return null;
      }

      if (callExpression.Arguments.Count != callExpression.Function.Parameters.Count)
      {
        Diagnostics.ReportLoweringError(
            $"Argument count mismatch for function '{callExpression.Function.Name}'.");
        return null;
      }

      if (callExpression.Function.ReturnType == TypeSymbol.U0 && preserveResult)
      {
        Diagnostics.ReportLoweringError(
            $"Cannot use u0-returning function '{callExpression.Function.Name}' as a value.");
        return null;
      }

      var argumentValues = new IrValue[callExpression.Arguments.Count];
      for (var index = 0; index < callExpression.Arguments.Count; index++)
      {
        argumentValues[index] = LowerValueExpression(
            callExpression.Arguments[index],
            context,
            callExpression.Function.Parameters[index].Type);
        if (argumentValues[index] == null)
          return null;
      }

      IrTemporaryStorage resultStorage = null;
      if (callExpression.Function.ReturnType != TypeSymbol.U0)
        resultStorage = context.CreateTemporary(callExpression.Function.ReturnType);

      var endBlock = context.CreateBlock("fn_end");
      var inlineFrame = new InlineFunctionFrame(
          callExpression.Function,
          endBlock.Label,
          resultStorage);

      for (var index = 0; index < callExpression.Function.Parameters.Count; index++)
      {
        var parameter = callExpression.Function.Parameters[index];
        var parameterStorage = context.CreateTemporary(parameter.Type);
        inlineFrame.SetParameterStorage(parameter, parameterStorage);
        context.Emit(new IrCopyInstruction(parameterStorage, argumentValues[index]));
      }

      context.PushInlineFrame(inlineFrame);
      try
      {
        LowerBlock(declaration.Body, context);
        if (context.CurrentBlock.Terminator == null)
          context.TerminateWithJump(endBlock.Label);
      }
      finally
      {
        context.PopInlineFrame();
      }

      context.SwitchTo(endBlock);
      return preserveResult ? resultStorage : null;
    }

    private sealed class InlineFunctionFrame
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

    private sealed class EventLoweringContext
    {
      private int _nextBlockId = 1;
      private int _nextTemporaryId;
      private readonly Stack<InlineFunctionFrame> _inlineFrames = new();

      public EventLoweringContext(BoundEventSymbol eventSymbol)
      {
        EventSymbol = eventSymbol ?? throw new ArgumentNullException(nameof(eventSymbol));
        var entryBlock = new IrBasicBlock(eventSymbol.UdonName);
        Blocks.Add(entryBlock);
        CurrentBlock = entryBlock;
      }

      public BoundEventSymbol EventSymbol { get; }
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

      public IrTemporaryStorage CreateTemporary(TypeSymbol type)
      {
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

        return new IrLocalStorage(variable);
      }

      public IrStorage GetParameterStorage(ParameterSymbol parameter)
      {
        foreach (var frame in _inlineFrames)
        {
          if (frame.TryGetParameterStorage(parameter, out var storage))
            return storage;
        }

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

      public void Emit(IrInstruction instruction)
      {
        CurrentBlock.AddInstruction(instruction);
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
    }
  }
}
