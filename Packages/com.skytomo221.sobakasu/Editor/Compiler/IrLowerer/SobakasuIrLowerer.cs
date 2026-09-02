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
    public IReadOnlyList<StateVariableSymbol> States { get; }
    public IReadOnlyList<IrModule> Modules { get; }

    public IrProgram(
        IReadOnlyList<StateVariableSymbol> states,
        IReadOnlyList<IrModule> modules)
    {
      States = states ?? throw new ArgumentNullException(nameof(states));
      Modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }
  }

  internal sealed class IrModule
  {
    public string Name { get; }
    public string ExportName { get; }
    public IReadOnlyList<ParameterSymbol> Parameters { get; }
    public string ReturnValueStorageName { get; }
    public IReadOnlyList<IrBasicBlock> Blocks { get; }

    public IrModule(
        BoundEventSymbol eventSymbol,
        IReadOnlyList<IrBasicBlock> blocks)
    {
      if (eventSymbol == null)
        throw new ArgumentNullException(nameof(eventSymbol));
      Name = eventSymbol.SourceName;
      ExportName = eventSymbol.UdonName;
      Parameters = eventSymbol.Parameters;
      ReturnValueStorageName = eventSymbol.ReturnValueStorageName;
      Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
    }

    public IrModule(
        NetworkReceiveSymbol receiveSymbol,
        IReadOnlyList<IrBasicBlock> blocks)
    {
      if (receiveSymbol == null)
        throw new ArgumentNullException(nameof(receiveSymbol));
      Name = receiveSymbol.Name;
      ExportName = receiveSymbol.ExportName;
      var parameters = new List<ParameterSymbol>(
          receiveSymbol.PhysicalParameters.Count);
      foreach (var parameter in receiveSymbol.PhysicalParameters)
        parameters.Add(parameter.PhysicalParameter);
      Parameters = parameters;
      ReturnValueStorageName = null;
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

  internal sealed class IrStateStorage : IrStorage
  {
    public StateVariableSymbol State { get; }

    public IrStateStorage(StateVariableSymbol state)
        : base(state?.Type ?? throw new ArgumentNullException(nameof(state)))
    {
      State = state;
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

  internal sealed class IrAggregateValue : IrValue
  {
    public IReadOnlyList<IrValue> Leaves { get; }

    public IrAggregateValue(TypeSymbol type, IReadOnlyList<IrValue> leaves)
        : base(type)
    {
      Leaves = leaves ?? throw new ArgumentNullException(nameof(leaves));
    }
  }

  internal sealed class IrAggregateStorage : IrStorage
  {
    public IReadOnlyList<IrStorage> Leaves { get; }

    public IrAggregateStorage(TypeSymbol type, IReadOnlyList<IrStorage> leaves)
        : base(type)
    {
      Leaves = leaves ?? throw new ArgumentNullException(nameof(leaves));
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

  internal sealed class IrThisValue : IrValue
  {
    public IrThisValue(TypeSymbol type)
        : base(type)
    {
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
    private readonly Dictionary<StateVariableSymbol, IrStorage> _stateStorage = new();

    public DiagnosticBag Diagnostics { get; } = new();

    public IrProgram Lower(BoundProgram program)
    {
      _functions.Clear();
      _stateStorage.Clear();
      foreach (var function in program.Functions)
        _functions[function.FunctionSymbol] = function;

      var states = CreatePhysicalStates(program.States);

      var modules = new List<IrModule>();

      foreach (var @event in program.Events)
      {
        var context = new EventLoweringContext(@event.EventSymbol, _stateStorage);
        LowerBlock(@event.Body, context);

        if (context.CurrentBlock.Terminator == null)
          context.CurrentBlock.SetTerminator(new IrReturnTerminator());

        modules.Add(new IrModule(@event.EventSymbol, context.Blocks));
      }

      foreach (var receiver in program.NetworkReceivers)
      {
        var parameterStorage = CreateNetworkReceiveParameterStorage(
            receiver.ReceiveSymbol);
        var context = new EventLoweringContext(
            receiver.ReceiveSymbol,
            _stateStorage,
            parameterStorage);
        LowerBlock(receiver.Body, context);

        if (context.CurrentBlock.Terminator == null)
          context.CurrentBlock.SetTerminator(new IrReturnTerminator());

        modules.Add(new IrModule(receiver.ReceiveSymbol, context.Blocks));
      }

      return new IrProgram(states, modules);
    }

    private static IReadOnlyDictionary<ParameterSymbol, IrStorage>
        CreateNetworkReceiveParameterStorage(NetworkReceiveSymbol receiver)
    {
      var result = new Dictionary<ParameterSymbol, IrStorage>();
      foreach (var logical in receiver.Parameters)
      {
        var leaves = new List<IrStorage>();
        foreach (var physical in receiver.PhysicalParameters)
        {
          if (ReferenceEquals(physical.LogicalParameter, logical))
            leaves.Add(new IrParameterStorage(physical.PhysicalParameter));
        }

        if (logical.Type.UsesFlattenedAggregateStorage && logical.Type.AggregateKind == UserAggregateKind.Struct)
          result[logical] = new IrAggregateStorage(logical.Type, leaves);
        else if (leaves.Count > 0)
          result[logical] = leaves[0];
      }

      return result;
    }

    private List<StateVariableSymbol> CreatePhysicalStates(
      IReadOnlyList<BoundStateDeclaration> declarations)
    {
      var states = new List<StateVariableSymbol>();
      var usedNames = new HashSet<string>(StringComparer.Ordinal);
      foreach (var declaration in declarations)
      {
        if (!IsAggregateStorageType(declaration.StateSymbol.Type))
        {
          usedNames.Add(declaration.StateSymbol.Name);
        }
      }

      foreach (var declaration in declarations)
      {
        var state = declaration.StateSymbol;
        if (!IsAggregateStorageType(state.Type))
        {
          states.Add(state);
          _stateStorage[state] = new IrStateStorage(state);
          continue;
        }

        var descriptors = AggregateLayout.GetLeaves(state.Type);
        var constant = state.InitialValue as AggregateConstantValue;
        var leaves = new List<IrStorage>(descriptors.Count);
        for (var index = 0; index < descriptors.Count; index++)
        {
          var pathName = string.Join("__", descriptors[index].Path);
          var publicName = string.IsNullOrEmpty(pathName)
              ? state.Name
              : $"{state.Name}__{pathName}";
          var candidate = publicName;
          var suffix = 0;
          while (!usedNames.Add(candidate))
            candidate = $"{publicName}__aggregate_{++suffix}";
          publicName = candidate;

          var leafState = new StateVariableSymbol(
              publicName,
              descriptors[index].Type,
              state.IsPublic,
              state.SynchronizationMode,
              constant != null && index < constant.Leaves.Count
                  ? constant.Leaves[index]
                  : null,
              state.DeclarationSpan,
              state.InitializerSpan,
              states.Count);
          states.Add(leafState);
          leaves.Add(new IrStateStorage(leafState));
        }

        _stateStorage[state] = new IrAggregateStorage(state.Type, leaves);
      }

      return states;
    }

    private static bool IsAggregateStorageType(TypeSymbol type)
    {
      return type?.UsesFlattenedAggregateStorage == true ||
          type?.TypeKind == TypeKind.Array && type.ElementType?.UsesFlattenedAggregateStorage == true;
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

        context.EmitCopy(
            context.GetLocalStorage(variableDeclarationStatement.Variable),
            source);
        return;
      }

      if (statement is BoundNetworkSendStatement sendStatement)
      {
        LowerNetworkSendStatement(sendStatement, context);
        return;
      }

      if (statement is BoundReturnStatement returnStatement)
      {
        LowerReturnStatement(returnStatement, context);
        return;
      }

      if (statement is BoundBreakStatement breakStatement)
      {
        LowerBreakStatement(breakStatement, context);
        return;
      }

      if (statement is BoundContinueStatement continueStatement)
      {
        LowerContinueStatement(continueStatement, context);
        return;
      }

      if (statement is BoundRedoStatement redoStatement)
      {
        LowerRedoStatement(redoStatement, context);
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

    private void LowerNetworkSendStatement(
        BoundNetworkSendStatement statement,
        EventLoweringContext context)
    {
      var physicalArguments = new List<IrValue>();
      for (var index = 0; index < statement.Arguments.Count; index++)
      {
        var expectedType = index < statement.Receiver.Parameters.Count
            ? statement.Receiver.Parameters[index].Type
            : statement.Arguments[index].Type;
        var value = LowerValueExpression(
            statement.Arguments[index],
            context,
            expectedType);
        if (value == null)
          return;

        if (expectedType.UsesFlattenedAggregateStorage &&
            (expectedType.AggregateKind == UserAggregateKind.Struct ||
             expectedType.AggregateKind == UserAggregateKind.Tuple))
          physicalArguments.AddRange(GetAggregateLeaves(value));
        else
          physicalArguments.Add(value);
      }

      var target = LowerValueExpression(
          statement.Target,
          context,
          statement.Target.Type);
      if (target == null)
        return;

      var arguments = new List<IrValue>(physicalArguments.Count + 3)
      {
        new IrThisValue(statement.CurrentBehaviourType),
        target,
        new IrConstantValue(
            statement.Receiver.ExportName,
            TypeSymbol.String,
            statement.Receiver.SourceSpan)
      };
      arguments.AddRange(physicalArguments);
      context.Emit(new IrExternCallInstruction(
          statement.ExternSignature,
          arguments,
          null));
    }

    private void LowerExpressionStatement(
        BoundExpressionStatement statement,
        EventLoweringContext context)
    {
      LowerExpressionForEffect(statement.Expression, context);
    }

    private void LowerExpressionForEffect(
        BoundExpression expression,
        EventLoweringContext context)
    {
      if (expression is BoundErrorExpression)
      {
        Diagnostics.ReportLoweringError(
            "Cannot lower expression that already contains semantic errors.");
        return;
      }

      if (expression is BoundCallExpression callExpression)
      {
        LowerCallExpression(callExpression, context, preserveResult: false);
        return;
      }

      if (expression is BoundUserFunctionCallExpression functionCallExpression)
      {
        LowerUserFunctionCallExpression(functionCallExpression, context, preserveResult: false);
        return;
      }

      LowerValueExpression(expression, context, expression.Type);
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

        case BoundArrayLiteralExpression arrayLiteralExpression:
          return LowerArrayLiteralExpression(arrayLiteralExpression, context);

        case BoundArrayRepeatExpression arrayRepeatExpression:
          return LowerArrayRepeatExpression(arrayRepeatExpression, context);

        case BoundElementAccessExpression elementAccessExpression:
          return LowerElementAccessExpression(elementAccessExpression, context);

        case BoundElementAssignmentExpression elementAssignmentExpression:
          return LowerElementAssignmentExpression(elementAssignmentExpression, context);

        case BoundArrayLengthExpression arrayLengthExpression:
          return LowerArrayLengthExpression(arrayLengthExpression, context);

        case BoundStructConstructionExpression structConstructionExpression:
          return LowerStructConstructionExpression(structConstructionExpression, context);

        case BoundTupleExpression tupleExpression:
          return LowerTupleExpression(tupleExpression, context);

        case BoundEnumConstructionExpression enumConstructionExpression:
          return LowerEnumConstructionExpression(enumConstructionExpression, context);

        case BoundMaybeExternBindingExpression maybeExternBindingExpression:
          return LowerMaybeExternBindingExpression(
              maybeExternBindingExpression,
              context);

        case BoundAggregateFieldAccessExpression fieldAccessExpression:
          return LowerAggregateFieldAccessExpression(fieldAccessExpression, context);

        case BoundAggregateFieldAssignmentExpression fieldAssignmentExpression:
          return LowerAggregateFieldAssignmentExpression(fieldAssignmentExpression, context);

        case BoundNameExpression nameExpression
            when nameExpression.Symbol is LocalVariableSymbol local:
          return context.GetLocalStorage(local);

        case BoundNameExpression nameExpression
            when nameExpression.Symbol is StateVariableSymbol state:
          return context.GetVariableStorage(state);

        case BoundNameExpression nameExpression
            when nameExpression.Symbol is ConstantSymbol constant:
          return new IrConstantValue(
              constant.ConstantValue,
              constant.Type,
              constant.InitializerSpan);

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

        case BoundBlockExpression blockExpression:
          return LowerBlockExpression(blockExpression, context);

        case BoundIfExpression ifExpression:
          return LowerIfExpression(ifExpression, context);

        case BoundMatchExpression matchExpression:
          return LowerMatchExpression(matchExpression, context);

        case BoundWhileExpression whileExpression:
          return LowerWhileExpression(whileExpression, context);

        case BoundLoopExpression loopExpression:
          return LowerLoopExpression(loopExpression, context);

        case BoundAssignmentExpression assignmentExpression:
        {
          var source = LowerValueExpression(
              assignmentExpression.Expression,
              context,
              assignmentExpression.Variable.Type);
          if (source == null)
            return null;

          var target = context.GetVariableStorage(assignmentExpression.Variable);
          context.EmitCopy(target, source);
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

    private IrValue LowerBlockExpression(
        BoundBlockExpression expression,
        EventLoweringContext context)
    {
      LowerBlock(expression.Block, context);
      if (context.CurrentBlock.Terminator != null)
        return null;

      if (expression.TrailingExpression == null)
      {
        return expression.Type == TypeSymbol.Unit
            ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
            : null;
      }

      if (expression.Type == TypeSymbol.Unit)
      {
        LowerExpressionForEffect(expression.TrailingExpression, context);
        return new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>());
      }

      return LowerValueExpression(
          expression.TrailingExpression,
          context,
          expression.Type);
    }

    private IrValue LowerIfExpression(
        BoundIfExpression expression,
        EventLoweringContext context)
    {
      var condition = LowerValueExpression(
          expression.Condition,
          context,
          TypeSymbol.Bool);
      if (condition == null)
        return null;

      var thenBlock = context.CreateBlock("if_then");
      var elseBlock = expression.ElseExpression == null
          ? null
          : context.CreateBlock("if_else");
      var mergeBlock = expression.Type == TypeSymbol.Never
          ? null
          : context.CreateBlock("if_merge");
      IrStorage result = null;
      if (expression.Type != TypeSymbol.Unit &&
          expression.Type != TypeSymbol.Never)
      {
        result = context.CreateTemporary(expression.Type);
      }

      context.TerminateWithCondition(
          condition,
          thenBlock.Label,
          elseBlock?.Label ?? mergeBlock.Label);

      context.SwitchTo(thenBlock);
      var thenValue = LowerBlockExpression(expression.ThenExpression, context);
      CompleteIfBranch(thenValue, result, mergeBlock, context);

      if (elseBlock != null)
      {
        context.SwitchTo(elseBlock);
        var elseValue = LowerValueExpression(
            expression.ElseExpression,
            context,
            expression.Type);
        CompleteIfBranch(elseValue, result, mergeBlock, context);
      }

      if (mergeBlock == null)
        return null;

      context.SwitchTo(mergeBlock);
      return expression.Type == TypeSymbol.Unit
          ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
          : result;
    }

    private static void CompleteIfBranch(
        IrValue value,
        IrStorage result,
        IrBasicBlock mergeBlock,
        EventLoweringContext context)
    {
      if (context.CurrentBlock.Terminator != null)
        return;

      if (result != null && value != null)
        context.EmitCopy(result, value);

      context.TerminateWithJump(mergeBlock.Label);
    }

    private IrValue LowerMatchExpression(
        BoundMatchExpression expression,
        EventLoweringContext context)
    {
      var loweredScrutinee = LowerValueExpression(
          expression.Expression,
          context,
          expression.Expression.Type);
      if (loweredScrutinee == null)
        return null;

      var scrutinee = context.CreateTemporary(expression.Expression.Type);
      context.EmitCopy(scrutinee, loweredScrutinee);

      var mergeBlock = expression.Type == TypeSymbol.Never
          ? null
          : context.CreateBlock("match_merge");
      IrStorage result = null;
      if (expression.Type != TypeSymbol.Unit &&
          expression.Type != TypeSymbol.Never)
      {
        result = context.CreateTemporary(expression.Type);
      }

      foreach (var arm in expression.Arms)
      {
        if (!arm.IsReachable || arm.Pattern is BoundInvalidPattern)
          continue;

        var armBlock = context.CreateBlock("match_arm");
        IrBasicBlock nextTestBlock = null;
        if (arm.Pattern is BoundWildcardPattern)
        {
          context.TerminateWithJump(armBlock.Label);
        }
        else
        {
          nextTestBlock = context.CreateBlock("match_test");
          var condition = LowerMatchPatternCondition(
              arm.Pattern,
              scrutinee,
              expression.Expression.Type,
              context);
          if (condition == null)
            return null;
          context.TerminateWithCondition(
              condition,
              armBlock.Label,
              nextTestBlock.Label);
        }

        context.SwitchTo(armBlock);
        if (arm.Pattern is BoundEnumVariantPattern enumPattern)
        {
          EmitMatchPatternBindings(
              enumPattern,
              scrutinee,
              expression.Expression.Type,
              context);
        }

        IrValue armValue = null;
        if (arm.Expression.Type == TypeSymbol.Unit)
          LowerExpressionForEffect(arm.Expression, context);
        else
          armValue = LowerValueExpression(arm.Expression, context, expression.Type);

        CompleteMatchArm(armValue, result, mergeBlock, context);
        if (nextTestBlock == null)
          break;
        context.SwitchTo(nextTestBlock);
      }

      if (context.CurrentBlock.Terminator == null)
      {
        if (mergeBlock != null)
          context.TerminateWithJump(mergeBlock.Label);
        else
          context.TerminateWithJump(context.CurrentBlock.Label);
      }

      if (mergeBlock == null)
        return null;

      context.SwitchTo(mergeBlock);
      return expression.Type == TypeSymbol.Unit
          ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
          : result;
    }

    private IrValue LowerMatchPatternCondition(
        BoundPattern pattern,
        IrStorage scrutinee,
        TypeSymbol scrutineeType,
        EventLoweringContext context)
    {
      IrValue left;
      IrConstantValue right;
      BoundBinaryOperator comparison;
      if (pattern is BoundLiteralPattern literalPattern)
      {
        left = scrutinee;
        right = new IrConstantValue(
            literalPattern.Literal.Value,
            literalPattern.Literal.Type,
            literalPattern.Literal.Span);
        comparison = literalPattern.ComparisonOperator;
      }
      else if (pattern is BoundEnumVariantPattern enumPattern)
      {
        left = GetEnumTagValue(scrutinee, scrutineeType);
        right = new IrConstantValue(enumPattern.Variant.Tag, TypeSymbol.I32);
        comparison = enumPattern.TagComparisonOperator;
      }
      else
      {
        return null;
      }

      if (left == null || comparison == null)
      {
        Diagnostics.ReportLoweringError(
            "Resolved match pattern has no comparison operation.");
        return null;
      }

      var result = context.CreateTemporary(TypeSymbol.Bool);
      context.Emit(new IrExternCallInstruction(
          comparison.ExternSignature,
          new[] { left, right },
          result));
      return result;
    }

    private static IrValue GetEnumTagValue(IrValue value, TypeSymbol enumType)
    {
      var leaves = GetAggregateLeaves(value);
      var descriptors = AggregateLayout.GetLeaves(enumType);
      for (var index = 0; index < descriptors.Count && index < leaves.Count; index++)
      {
        if (descriptors[index].IsEnumTag)
          return leaves[index];
      }
      return null;
    }

    private void EmitMatchPatternBindings(
        BoundEnumVariantPattern pattern,
        IrValue scrutinee,
        TypeSymbol enumType,
        EventLoweringContext context)
    {
      foreach (var binding in pattern.Bindings)
      {
        var source = ProjectEnumVariantField(
            scrutinee,
            enumType,
            pattern.Variant,
            binding.Field);
        if (source == null)
        {
          Diagnostics.ReportLoweringError(
              $"Resolved match binding '{binding.Variable.Name}' has no payload storage.");
          continue;
        }
        context.EmitCopy(context.GetLocalStorage(binding.Variable), source);
      }
    }

    private static IrValue ProjectEnumVariantField(
        IrValue receiver,
        TypeSymbol enumType,
        EnumVariantSymbol variant,
        AggregateFieldSymbol field)
    {
      var receiverLeaves = GetAggregateLeaves(receiver);
      var descriptors = AggregateLayout.GetLeaves(enumType);
      var valueLeaves = new List<IrValue>();
      var storageLeaves = new List<IrStorage>();
      var allStorage = true;
      for (var index = 0; index < descriptors.Count && index < receiverLeaves.Count; index++)
      {
        var path = descriptors[index].Path;
        if (path.Count < 2 ||
            !string.Equals(path[0], variant.Name, StringComparison.Ordinal) ||
            !string.Equals(path[1], field.Name, StringComparison.Ordinal))
        {
          continue;
        }

        var leaf = receiverLeaves[index];
        valueLeaves.Add(leaf);
        if (leaf is IrStorage storage)
          storageLeaves.Add(storage);
        else
          allStorage = false;
      }

      var expectedLeafCount = AggregateLayout.GetLeaves(field.Type).Count;
      if (valueLeaves.Count != expectedLeafCount)
        return null;

      if (!IsAggregateStorageType(field.Type))
        return valueLeaves.Count > 0 ? valueLeaves[0] : null;

      return allStorage
          ? new IrAggregateStorage(field.Type, storageLeaves)
          : new IrAggregateValue(field.Type, valueLeaves);
    }

    private static void CompleteMatchArm(
        IrValue value,
        IrStorage result,
        IrBasicBlock mergeBlock,
        EventLoweringContext context)
    {
      if (context.CurrentBlock.Terminator != null)
        return;

      if (result != null && value != null)
        context.EmitCopy(result, value);

      if (mergeBlock != null)
        context.TerminateWithJump(mergeBlock.Label);
      else
        context.TerminateWithJump(context.CurrentBlock.Label);
    }

    private IrValue LowerWhileExpression(
      BoundWhileExpression expression,
      EventLoweringContext context)
    {
      var conditionBlock = context.CreateBlock("while_condition");

      context.TerminateWithJump(conditionBlock.Label);
      context.SwitchTo(conditionBlock);
      var condition = LowerValueExpression(
          expression.Condition,
          context,
          TypeSymbol.Bool);
      if (condition == null)
        return null;

      var bodyBlock = context.CreateBlock("while_body");
      var exitBlock = context.CreateBlock("while_exit");
      context.TerminateWithCondition(
          condition,
          bodyBlock.Label,
          exitBlock.Label);

      context.PushLoop(new LoopLoweringFrame(
          expression.Loop,
          exitBlock.Label,
          conditionBlock.Label,
          bodyBlock.Label,
          null));
      try
      {
        context.SwitchTo(bodyBlock);
        LowerBlockExpression(expression.Body, context);
        if (context.CurrentBlock.Terminator == null)
          context.TerminateWithJump(conditionBlock.Label);
      }
      finally
      {
        context.PopLoop(expression.Loop);
      }

      context.SwitchTo(exitBlock);
      return new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>());
    }

    private IrValue LowerLoopExpression(
        BoundLoopExpression expression,
        EventLoweringContext context)
    {
      var bodyBlock = context.CreateBlock("loop_body");
      var hasExit = expression.Type != TypeSymbol.Never;
      var exitBlock = hasExit
          ? context.CreateBlock("loop_exit")
          : null;
      IrStorage result = null;
      if (expression.Type != TypeSymbol.Unit &&
          expression.Type != TypeSymbol.Never)
      {
        result = context.CreateTemporary(expression.Type);
      }

      context.TerminateWithJump(bodyBlock.Label);
      context.PushLoop(new LoopLoweringFrame(
          expression.Loop,
          exitBlock?.Label,
          bodyBlock.Label,
          bodyBlock.Label,
          result));
      try
      {
        context.SwitchTo(bodyBlock);
        LowerBlockExpression(expression.Body, context);
        if (context.CurrentBlock.Terminator == null)
          context.TerminateWithJump(bodyBlock.Label);
      }
      finally
      {
        context.PopLoop(expression.Loop);
      }

      if (exitBlock == null)
        return null;

      context.SwitchTo(exitBlock);
      return expression.Type == TypeSymbol.Unit
          ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
          : result;
    }

    private void LowerBreakStatement(
        BoundBreakStatement statement,
        EventLoweringContext context)
    {
      var loop = context.FindLoop(statement.Target);
      if (loop == null || string.IsNullOrEmpty(loop.BreakTarget))
      {
        Diagnostics.ReportLoweringError(
            "Resolved break target is not active during lowering.");
        return;
      }

      if (statement.Expression != null)
      {
        if (statement.Expression.Type == TypeSymbol.Unit)
        {
          LowerExpressionForEffect(statement.Expression, context);
          if (context.CurrentBlock.Terminator != null)
            return;

          context.TerminateWithJump(loop.BreakTarget);
          return;
        }

        var value = LowerValueExpression(
            statement.Expression,
            context,
            statement.Expression.Type);
        if (value == null || context.CurrentBlock.Terminator != null)
          return;

        if (loop.ResultStorage == null)
        {
          Diagnostics.ReportLoweringError(
              "Value-producing break does not have a loop result slot.");
          return;
        }

        context.EmitCopy(loop.ResultStorage, value);
      }

      context.TerminateWithJump(loop.BreakTarget);
    }

    private void LowerContinueStatement(
        BoundContinueStatement statement,
        EventLoweringContext context)
    {
      var loop = context.FindLoop(statement.Target);
      if (loop == null)
      {
        Diagnostics.ReportLoweringError(
            "Resolved continue target is not active during lowering.");
        return;
      }

      context.TerminateWithJump(loop.ContinueTarget);
    }

    private void LowerRedoStatement(
        BoundRedoStatement statement,
        EventLoweringContext context)
    {
      var loop = context.FindLoop(statement.Target);
      if (loop == null)
      {
        Diagnostics.ReportLoweringError(
            "Resolved redo target is not active during lowering.");
        return;
      }

      context.TerminateWithJump(loop.RedoTarget);
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

      if (statement.Expression.Type == TypeSymbol.Unit)
      {
        LowerExpressionForEffect(statement.Expression, context);
        if (context.CurrentBlock.Terminator == null)
          context.CurrentBlock.SetTerminator(new IrReturnTerminator());
        return;
      }

      var value = LowerValueExpression(
          statement.Expression,
          context,
          context.EntryReturnType);
      if (value == null)
        return;

      if (string.IsNullOrEmpty(context.ReturnValueStorageName))
      {
        Diagnostics.ReportLoweringError(
            $"Entry point '{context.EntrySourceName}' has a non-void return without a Udon return slot.");
        return;
      }

      context.EmitCopy(
          new IrReturnValueStorage(context.ReturnValueStorageName),
          value);
      context.CurrentBlock.SetTerminator(new IrReturnTerminator());
    }

    private void LowerInlineFunctionReturnStatement(
        BoundReturnStatement statement,
        EventLoweringContext context)
    {
      if (statement.Expression == null)
      {
        context.MarkInlineEndIncoming();
        context.TerminateWithJump(context.CurrentInlineEndLabel);
        return;
      }

      if (statement.Expression.Type == TypeSymbol.Unit)
      {
        LowerExpressionForEffect(statement.Expression, context);
        if (context.CurrentBlock.Terminator == null)
        {
          context.MarkInlineEndIncoming();
          context.TerminateWithJump(context.CurrentInlineEndLabel);
        }
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

      context.EmitCopy(resultStorage, value);
      context.MarkInlineEndIncoming();
      context.TerminateWithJump(context.CurrentInlineEndLabel);
    }

    private IrValue LowerLiteralExpression(
        BoundLiteralExpression literalExpression,
        TypeSymbol expectedType)
    {
      return new IrConstantValue(
          literalExpression.Value,
          literalExpression.Type,
          literalExpression.Span);
    }

    private IrValue LowerStructConstructionExpression(
        BoundStructConstructionExpression expression,
        EventLoweringContext context)
    {
      var values = new IrValue[AggregateLayout.GetLeaves(expression.Type).Count];
      foreach (var initializer in expression.Initializers)
      {
        var value = LowerValueExpression(
            initializer.Expression,
            context,
            initializer.Field.Type);
        if (value == null)
          return null;

        var fieldLeaves = ExpandValueLeaves(initializer.Field.Type, value);
        var indices = AggregateLayout.GetFieldLeafIndices(
            expression.Type,
            initializer.Field);
        for (var index = 0; index < indices.Count && index < fieldLeaves.Count; index++)
          values[indices[index]] = fieldLeaves[index];
      }

      return new IrAggregateValue(expression.Type, values);
    }

    private IrValue LowerEnumConstructionExpression(
        BoundEnumConstructionExpression expression,
        EventLoweringContext context)
    {
      var descriptors = AggregateLayout.GetLeaves(expression.Type);
      var values = new IrValue[descriptors.Count];
      foreach (var initializer in expression.Initializers)
      {
        var value = LowerValueExpression(
            initializer.Expression,
            context,
            initializer.Field.Type);
        if (value == null)
          return null;

        var fieldLeaves = ExpandValueLeaves(initializer.Field.Type, value);
        var leafIndex = 0;
        for (var index = 0; index < descriptors.Count; index++)
        {
          var path = descriptors[index].Path;
          if (path.Count < 2 ||
              !string.Equals(path[0], expression.Variant.Name, StringComparison.Ordinal) ||
              !string.Equals(path[1], initializer.Field.Name, StringComparison.Ordinal))
          {
            continue;
          }

          if (leafIndex < fieldLeaves.Count)
            values[index] = fieldLeaves[leafIndex++];
        }
      }

      for (var index = 0; index < descriptors.Count; index++)
      {
        if (descriptors[index].IsEnumTag)
        {
          values[index] = new IrConstantValue(
              expression.Variant.Tag,
              TypeSymbol.I32);
        }
      }

      return new IrAggregateValue(expression.Type, values);
    }

    private IrValue LowerTupleExpression(
        BoundTupleExpression expression,
        EventLoweringContext context)
    {
      var values = new IrValue[AggregateLayout.GetLeaves(expression.Type).Count];
      for (var index = 0; index < expression.Elements.Count; index++)
      {
        var element = LowerValueExpression(
            expression.Elements[index],
            context,
            expression.Type.TupleElementTypes[index]);
        if (element == null)
          return null;

        var indices = AggregateLayout.GetFieldLeafIndices(
            expression.Type,
            expression.Type.AggregateFields[index]);
        var elementLeaves = IsAggregateStorageType(element.Type)
            ? GetAggregateLeaves(element)
            : new[] { element };
        for (var leafIndex = 0;
             leafIndex < indices.Count && leafIndex < elementLeaves.Count;
             leafIndex++)
        {
          values[indices[leafIndex]] = elementLeaves[leafIndex];
        }
      }
      return new IrAggregateValue(expression.Type, values);
    }

    private IrValue LowerMaybeExternBindingExpression(
        BoundMaybeExternBindingExpression expression,
        EventLoweringContext context)
    {
      var rawValue = LowerCallExpression(
          expression.RawExpression,
          context,
          preserveResult: true);
      if (rawValue == null)
        return null;

      return LowerMaybeOutputProjection(
          rawValue,
          expression.Projection,
          context,
          "maybe_extern");
    }

    private IrValue LowerMaybeOutputProjection(
        IrValue rawValue,
        ExternMaybeOutputProjection projection,
        EventLoweringContext context,
        string labelPrefix)
    {
      if (projection.ValidityMethod.Parameters.Count != 1 ||
          projection.ValidityMethod.ReturnType != TypeSymbol.Bool)
      {
        Diagnostics.ReportLoweringError(
            "The resolved Maybe validity method must accept one value and return bool.");
        return null;
      }

      var isValid = context.CreateTemporary(TypeSymbol.Bool);
      context.Emit(new IrExternCallInstruction(
          projection.ValidityMethod.ExternSignature,
          new[] { rawValue },
          isValid));

      var justBlock = context.CreateBlock($"{labelPrefix}_just");
      var nothingBlock = context.CreateBlock($"{labelPrefix}_nothing");
      var mergeBlock = context.CreateBlock($"{labelPrefix}_merge");
      var result = context.CreateTemporary(projection.Type);
      context.TerminateWithCondition(
          isValid,
          justBlock.Label,
          nothingBlock.Label);

      context.SwitchTo(justBlock);
      context.EmitCopy(
          result,
          CreateMaybeExternEnumValue(projection.JustVariant, rawValue));
      context.TerminateWithJump(mergeBlock.Label);

      context.SwitchTo(nothingBlock);
      context.EmitCopy(
          result,
          CreateMaybeExternEnumValue(projection.NothingVariant, null));
      context.TerminateWithJump(mergeBlock.Label);

      context.SwitchTo(mergeBlock);
      return result;
    }

    private static IrAggregateValue CreateMaybeExternEnumValue(
        EnumVariantSymbol variant,
        IrValue payload)
    {
      var descriptors = AggregateLayout.GetLeaves(variant.ContainingType);
      var values = new IrValue[descriptors.Count];
      for (var index = 0; index < descriptors.Count; index++)
      {
        var descriptor = descriptors[index];
        if (descriptor.IsEnumTag)
        {
          values[index] = new IrConstantValue(variant.Tag, TypeSymbol.I32);
          continue;
        }

        if (payload != null &&
            descriptor.Path.Count >= 2 &&
            string.Equals(
                descriptor.Path[0],
                variant.Name,
                StringComparison.Ordinal))
        {
          values[index] = payload;
        }
      }

      return new IrAggregateValue(variant.ContainingType, values);
    }

    private IrValue LowerAggregateFieldAccessExpression(
        BoundAggregateFieldAccessExpression expression,
        EventLoweringContext context)
    {
      var receiver = LowerValueExpression(
          expression.Receiver,
          context,
          expression.Receiver.Type);
      if (receiver == null)
        return null;
      return ProjectAggregateField(receiver, expression.Receiver.Type, expression.Field);
    }

    private IrValue LowerAggregateFieldAssignmentExpression(
        BoundAggregateFieldAssignmentExpression expression,
        EventLoweringContext context)
    {
      if (TryGetAggregateArrayElementRoot(
              expression.Target,
              out var element,
              out var fields))
      {
        return LowerAggregateArrayFieldAssignment(
            expression,
            element,
            fields,
            context);
      }

      if (!TryLowerDirectStorage(expression.Target, context, out var target))
      {
        Diagnostics.ReportLoweringError("Aggregate field assignment has no writable storage.");
        return null;
      }

      IrValue value;
      if (expression.CompoundOperator == null)
      {
        value = LowerValueExpression(expression.Value, context, expression.Target.Type);
      }
      else
      {
        var right = LowerValueExpression(
            expression.Value,
            context,
            expression.CompoundOperator.RightType);
        if (right == null)
          return null;
        var result = context.CreateTemporary(expression.Target.Type);
        context.Emit(new IrExternCallInstruction(
            expression.CompoundOperator.ExternSignature,
            new IrValue[] { target, right },
            result));
        value = result;
      }

      if (value == null)
        return null;
      context.EmitCopy(target, value);
      return target;
    }

    private bool TryLowerDirectStorage(
        BoundExpression expression,
        EventLoweringContext context,
        out IrStorage storage)
    {
      if (expression is BoundNameExpression name)
      {
        storage = name.Symbol switch
        {
          VariableSymbol variable => context.GetVariableStorage(variable),
          ParameterSymbol parameter => context.GetParameterStorage(parameter),
          _ => null
        };
        return storage != null;
      }

      if (expression is BoundAggregateFieldAccessExpression field &&
          TryLowerDirectStorage(field.Receiver, context, out var receiverStorage))
      {
        storage = ProjectAggregateField(
            receiverStorage,
            field.Receiver.Type,
            field.Field) as IrStorage;
        return storage != null;
      }

      storage = null;
      return false;
    }

    private static IrValue ProjectAggregateField(
        IrValue receiver,
        TypeSymbol receiverType,
        AggregateFieldSymbol field)
    {
      var receiverLeaves = GetAggregateLeaves(receiver);
      var indices = AggregateLayout.GetFieldLeafIndices(receiverType, field);
      if (!IsAggregateStorageType(field.Type))
        return indices.Count > 0 ? receiverLeaves[indices[0]] : null;

      var storageLeaves = new List<IrStorage>();
      var valueLeaves = new List<IrValue>();
      var allStorage = true;
      foreach (var index in indices)
      {
        var leaf = receiverLeaves[index];
        valueLeaves.Add(leaf);
        if (leaf is IrStorage leafStorage)
          storageLeaves.Add(leafStorage);
        else
          allStorage = false;
      }

      return allStorage
          ? new IrAggregateStorage(field.Type, storageLeaves)
          : new IrAggregateValue(field.Type, valueLeaves);
    }

    private static IReadOnlyList<IrValue> ExpandValueLeaves(
        TypeSymbol type,
        IrValue value)
    {
      return IsAggregateStorageType(type)
          ? GetAggregateLeaves(value)
          : new[] { value };
    }

    private static IReadOnlyList<IrValue> GetAggregateLeaves(IrValue value)
    {
      if (value is IrAggregateValue aggregateValue)
        return aggregateValue.Leaves;
      if (value is IrAggregateStorage aggregateStorage)
      {
        var result = new IrValue[aggregateStorage.Leaves.Count];
        for (var index = 0; index < result.Length; index++)
          result[index] = aggregateStorage.Leaves[index];
        return result;
      }
      throw new InvalidOperationException(
          $"IR value '{value?.GetType().Name ?? "<null>"}' is not aggregate storage.");
    }

    private static bool TryGetAggregateArrayElementRoot(
        BoundAggregateFieldAccessExpression target,
        out BoundElementAccessExpression element,
        out IReadOnlyList<AggregateFieldSymbol> fields)
    {
      var reversed = new List<AggregateFieldSymbol>();
      BoundExpression current = target;
      while (current is BoundAggregateFieldAccessExpression field)
      {
        reversed.Add(field.Field);
        current = field.Receiver;
      }

      if (current is not BoundElementAccessExpression arrayElement ||
          !IsAggregateStorageType(arrayElement.Array.Type))
      {
        element = null;
        fields = null;
        return false;
      }

      reversed.Reverse();
      element = arrayElement;
      fields = reversed;
      return true;
    }

    private IrValue LowerArrayLiteralExpression(
        BoundArrayLiteralExpression expression,
        EventLoweringContext context)
    {
      if (IsAggregateStorageType(expression.Type))
        return LowerAggregateArrayLiteralExpression(expression, context);

      var result = context.CreateTemporary(expression.Type);
      context.Emit(new IrExternCallInstruction(
          expression.Intrinsics.ConstructorExternSignature,
          new IrValue[]
          {
            new IrConstantValue(
                expression.Elements.Count,
                expression.Intrinsics.IndexType)
          },
          result));

      for (var index = 0; index < expression.Elements.Count; index++)
      {
        var element = LowerValueExpression(
            expression.Elements[index],
            context,
            expression.ElementType);
        if (element == null)
          return null;

        context.Emit(new IrExternCallInstruction(
            expression.Intrinsics.SetterExternSignature,
            new IrValue[]
            {
              result,
              new IrConstantValue(index, expression.Intrinsics.IndexType),
              element
            },
            null));
      }

      return result;
    }

    private IrValue LowerArrayRepeatExpression(
        BoundArrayRepeatExpression expression,
        EventLoweringContext context)
    {
      if (IsAggregateStorageType(expression.Type))
        return LowerAggregateArrayRepeatExpression(expression, context);

      var loweredLength = LowerValueExpression(
          expression.Length,
          context,
          expression.Intrinsics.IndexType);
      if (loweredLength == null)
        return null;

      var length = context.CreateTemporary(expression.Intrinsics.IndexType);
      context.EmitCopy(length, loweredLength);

      var result = context.CreateTemporary(expression.Type);
      context.Emit(new IrExternCallInstruction(
          expression.Intrinsics.ConstructorExternSignature,
          new IrValue[] { length },
          result));

      if (expression.UsesDefaultValue)
        return result;

      var index = context.CreateTemporary(expression.Intrinsics.IndexType);
      context.EmitCopy(
          index,
          new IrConstantValue(0, expression.Intrinsics.IndexType));

      var conditionBlock = context.CreateBlock("array_repeat_condition");
      var bodyBlock = context.CreateBlock("array_repeat_body");
      var exitBlock = context.CreateBlock("array_repeat_exit");
      context.TerminateWithJump(conditionBlock.Label);

      context.SwitchTo(conditionBlock);
      var condition = context.CreateTemporary(TypeSymbol.Bool);
      context.Emit(new IrExternCallInstruction(
          expression.IndexLessThanOperator.ExternSignature,
          new IrValue[] { index, length },
          condition));
      context.TerminateWithCondition(
          condition,
          bodyBlock.Label,
          exitBlock.Label);

      context.SwitchTo(bodyBlock);
      var element = LowerValueExpression(
          expression.Operand,
          context,
          expression.Type.ElementType);
      if (element == null)
        return null;

      context.Emit(new IrExternCallInstruction(
          expression.Intrinsics.SetterExternSignature,
          new IrValue[] { result, index, element },
          null));

      var nextIndex = context.CreateTemporary(expression.Intrinsics.IndexType);
      context.Emit(new IrExternCallInstruction(
          expression.IndexIncrementOperator.ExternSignature,
          new IrValue[]
          {
            index,
            new IrConstantValue(1, expression.Intrinsics.IndexType)
          },
          nextIndex));
      context.EmitCopy(index, nextIndex);
      context.TerminateWithJump(conditionBlock.Label);

      context.SwitchTo(exitBlock);
      return result;
    }

    private IrValue LowerElementAccessExpression(
        BoundElementAccessExpression expression,
        EventLoweringContext context)
    {
      if (IsAggregateStorageType(expression.Array.Type))
        return LowerAggregateArrayElementAccess(expression, context);

      var array = LowerValueExpression(
          expression.Array,
          context,
          expression.Array.Type);
      if (array == null)
        return null;

      var index = LowerValueExpression(
          expression.Index,
          context,
          expression.Intrinsics.IndexType);
      if (index == null)
        return null;

      var result = context.CreateTemporary(expression.Type);
      context.Emit(new IrExternCallInstruction(
          expression.Intrinsics.GetterExternSignature,
          new IrValue[] { array, index },
          result));
      return result;
    }

    private IrValue LowerElementAssignmentExpression(
        BoundElementAssignmentExpression expression,
        EventLoweringContext context)
    {
      if (IsAggregateStorageType(expression.Target.Array.Type))
      {
        return LowerAggregateArrayElementAssignment(
            expression,
            expression.Target,
            Array.Empty<AggregateFieldSymbol>(),
            context);
      }

      var loweredArray = LowerValueExpression(
          expression.Target.Array,
          context,
          expression.Target.Array.Type);
      if (loweredArray == null)
        return null;

      var array = context.CreateTemporary(expression.Target.Array.Type);
      context.EmitCopy(array, loweredArray);

      var loweredIndex = LowerValueExpression(
          expression.Target.Index,
          context,
          expression.Target.Intrinsics.IndexType);
      if (loweredIndex == null)
        return null;

      var index = context.CreateTemporary(expression.Target.Intrinsics.IndexType);
      context.EmitCopy(index, loweredIndex);

      IrValue value;
      if (expression.CompoundOperator == null)
      {
        value = LowerValueExpression(
            expression.Value,
            context,
            expression.Target.Type);
      }
      else
      {
        var oldValue = context.CreateTemporary(expression.Target.Type);
        context.Emit(new IrExternCallInstruction(
            expression.Target.Intrinsics.GetterExternSignature,
            new IrValue[] { array, index },
            oldValue));

        var right = LowerValueExpression(
            expression.Value,
            context,
            expression.CompoundOperator.RightType);
        if (right == null)
          return null;

        var compoundValue = context.CreateTemporary(expression.Target.Type);
        context.Emit(new IrExternCallInstruction(
            expression.CompoundOperator.ExternSignature,
            new IrValue[] { oldValue, right },
            compoundValue));
        value = compoundValue;
      }

      if (value == null)
        return null;

      var result = context.CreateTemporary(expression.Target.Type);
      context.EmitCopy(result, value);
      context.Emit(new IrExternCallInstruction(
          expression.Target.Intrinsics.SetterExternSignature,
          new IrValue[] { array, index, result },
          null));
      return result;
    }

    private IrValue LowerArrayLengthExpression(
        BoundArrayLengthExpression expression,
        EventLoweringContext context)
    {
      if (IsAggregateStorageType(expression.Array.Type))
      {
        var aggregateArray = LowerValueExpression(
            expression.Array,
            context,
            expression.Array.Type);
        if (aggregateArray == null)
          return null;
        var leaves = GetAggregateLeaves(aggregateArray);
        if (leaves.Count == 0 || expression.AggregateLeafIntrinsics?.Count == 0)
          return null;
        var aggregateResult = context.CreateTemporary(TypeSymbol.I32);
        context.Emit(new IrExternCallInstruction(
            expression.AggregateLeafIntrinsics[0].LengthExternSignature,
            new[] { leaves[0] },
            aggregateResult));
        return aggregateResult;
      }

      var array = LowerValueExpression(
          expression.Array,
          context,
          expression.Array.Type);
      if (array == null)
        return null;

      var result = context.CreateTemporary(expression.Type);
      context.Emit(new IrExternCallInstruction(
          expression.Intrinsics.LengthExternSignature,
          new IrValue[] { array },
          result));
      return result;
    }

    private IrValue LowerAggregateArrayLiteralExpression(
        BoundArrayLiteralExpression expression,
        EventLoweringContext context)
    {
      var result = context.CreateTemporary(expression.Type) as IrAggregateStorage;
      if (result == null || expression.AggregateLeafIntrinsics == null)
        return null;

      for (var leafIndex = 0; leafIndex < result.Leaves.Count; leafIndex++)
      {
        context.Emit(new IrExternCallInstruction(
            expression.AggregateLeafIntrinsics[leafIndex].ConstructorExternSignature,
            new IrValue[]
            {
              new IrConstantValue(expression.Elements.Count, TypeSymbol.I32)
            },
            result.Leaves[leafIndex]));
      }

      for (var index = 0; index < expression.Elements.Count; index++)
      {
        var element = LowerValueExpression(
            expression.Elements[index],
            context,
            expression.ElementType);
        if (element == null)
          return null;
        EmitAggregateArraySet(
            result.Leaves,
            expression.AggregateLeafIntrinsics,
            new IrConstantValue(index, TypeSymbol.I32),
            expression.ElementType,
            element,
            context);
      }

      return result;
    }

    private IrValue LowerAggregateArrayRepeatExpression(
        BoundArrayRepeatExpression expression,
        EventLoweringContext context)
    {
      var loweredLength = LowerValueExpression(expression.Length, context, TypeSymbol.I32);
      if (loweredLength == null)
        return null;
      var length = context.CreateTemporary(TypeSymbol.I32);
      context.EmitCopy(length, loweredLength);

      var result = context.CreateTemporary(expression.Type) as IrAggregateStorage;
      if (result == null || expression.AggregateLeafIntrinsics == null)
        return null;
      for (var leafIndex = 0; leafIndex < result.Leaves.Count; leafIndex++)
      {
        context.Emit(new IrExternCallInstruction(
            expression.AggregateLeafIntrinsics[leafIndex].ConstructorExternSignature,
            new[] { (IrValue)length },
            result.Leaves[leafIndex]));
      }

      if (expression.UsesDefaultValue)
        return result;

      var index = context.CreateTemporary(TypeSymbol.I32);
      context.EmitCopy(index, new IrConstantValue(0, TypeSymbol.I32));
      var conditionBlock = context.CreateBlock("aggregate_array_repeat_condition");
      var bodyBlock = context.CreateBlock("aggregate_array_repeat_body");
      var exitBlock = context.CreateBlock("aggregate_array_repeat_exit");
      context.TerminateWithJump(conditionBlock.Label);

      context.SwitchTo(conditionBlock);
      var condition = context.CreateTemporary(TypeSymbol.Bool);
      context.Emit(new IrExternCallInstruction(
          expression.IndexLessThanOperator.ExternSignature,
          new IrValue[] { index, length },
          condition));
      context.TerminateWithCondition(condition, bodyBlock.Label, exitBlock.Label);

      context.SwitchTo(bodyBlock);
      var element = LowerValueExpression(
          expression.Operand,
          context,
          expression.Type.ElementType);
      if (element == null)
        return null;
      EmitAggregateArraySet(
          result.Leaves,
          expression.AggregateLeafIntrinsics,
          index,
          expression.Type.ElementType,
          element,
          context);
      var nextIndex = context.CreateTemporary(TypeSymbol.I32);
      context.Emit(new IrExternCallInstruction(
          expression.IndexIncrementOperator.ExternSignature,
          new IrValue[] { index, new IrConstantValue(1, TypeSymbol.I32) },
          nextIndex));
      context.EmitCopy(index, nextIndex);
      context.TerminateWithJump(conditionBlock.Label);

      context.SwitchTo(exitBlock);
      return result;
    }

    private IrValue LowerAggregateArrayElementAccess(
        BoundElementAccessExpression expression,
        EventLoweringContext context)
    {
      var array = LowerValueExpression(expression.Array, context, expression.Array.Type);
      if (array == null || expression.AggregateLeafIntrinsics == null)
        return null;
      var arrayLeaves = GetAggregateLeaves(array);

      var loweredIndex = LowerValueExpression(expression.Index, context, TypeSymbol.I32);
      if (loweredIndex == null)
        return null;
      var index = context.CreateTemporary(TypeSymbol.I32);
      context.EmitCopy(index, loweredIndex);

      var result = context.CreateTemporary(expression.Type) as IrAggregateStorage;
      if (result == null)
        return null;
      for (var leafIndex = 0; leafIndex < result.Leaves.Count; leafIndex++)
      {
        context.Emit(new IrExternCallInstruction(
            expression.AggregateLeafIntrinsics[leafIndex].GetterExternSignature,
            new[] { arrayLeaves[leafIndex], (IrValue)index },
            result.Leaves[leafIndex]));
      }
      return result;
    }

    private IrValue LowerAggregateArrayFieldAssignment(
        BoundAggregateFieldAssignmentExpression expression,
        BoundElementAccessExpression element,
        IReadOnlyList<AggregateFieldSymbol> fields,
        EventLoweringContext context)
    {
      return LowerAggregateArrayAssignmentCore(
          element,
          fields,
          expression.Target.Type,
          expression.Value,
          expression.CompoundOperator,
          context);
    }

    private IrValue LowerAggregateArrayElementAssignment(
        BoundElementAssignmentExpression expression,
        BoundElementAccessExpression element,
        IReadOnlyList<AggregateFieldSymbol> fields,
        EventLoweringContext context)
    {
      return LowerAggregateArrayAssignmentCore(
          element,
          fields,
          expression.Target.Type,
          expression.Value,
          expression.CompoundOperator,
          context);
    }

    private IrValue LowerAggregateArrayAssignmentCore(
        BoundElementAccessExpression element,
        IReadOnlyList<AggregateFieldSymbol> fields,
        TypeSymbol targetType,
        BoundExpression valueExpression,
        BoundBinaryOperator compoundOperator,
        EventLoweringContext context)
    {
      if (!TryLowerAggregateArrayLocation(
              element,
              fields,
              context,
              out var arrays,
              out var intrinsics,
              out var index))
      {
        return null;
      }

      IrValue value;
      if (compoundOperator == null)
      {
        value = LowerValueExpression(valueExpression, context, targetType);
      }
      else
      {
        if (arrays.Count != 1)
          return null;
        var oldValue = context.CreateTemporary(targetType);
        context.Emit(new IrExternCallInstruction(
            intrinsics[0].GetterExternSignature,
            new IrValue[] { arrays[0], index },
            oldValue));
        var right = LowerValueExpression(
            valueExpression,
            context,
            compoundOperator.RightType);
        if (right == null)
          return null;
        var compoundValue = context.CreateTemporary(targetType);
        context.Emit(new IrExternCallInstruction(
            compoundOperator.ExternSignature,
            new IrValue[] { oldValue, right },
            compoundValue));
        value = compoundValue;
      }

      if (value == null)
        return null;
      EmitAggregateArraySet(arrays, intrinsics, index, targetType, value, context);
      return value;
    }

    private bool TryLowerAggregateArrayLocation(
        BoundElementAccessExpression element,
        IReadOnlyList<AggregateFieldSymbol> fields,
        EventLoweringContext context,
        out IReadOnlyList<IrValue> arrays,
        out IReadOnlyList<ArrayIntrinsicSymbols> intrinsics,
        out IrStorage index)
    {
      arrays = null;
      intrinsics = null;
      index = null;
      var array = LowerValueExpression(element.Array, context, element.Array.Type);
      if (array == null || element.AggregateLeafIntrinsics == null)
        return false;

      var currentArrays = new List<IrValue>(GetAggregateLeaves(array));
      var currentIntrinsics = new List<ArrayIntrinsicSymbols>(element.AggregateLeafIntrinsics);
      var currentType = element.Type;
      foreach (var field in fields)
      {
        var indices = AggregateLayout.GetFieldLeafIndices(currentType, field);
        var selectedArrays = new List<IrValue>(indices.Count);
        var selectedIntrinsics = new List<ArrayIntrinsicSymbols>(indices.Count);
        foreach (var fieldIndex in indices)
        {
          selectedArrays.Add(currentArrays[fieldIndex]);
          selectedIntrinsics.Add(currentIntrinsics[fieldIndex]);
        }
        currentArrays = selectedArrays;
        currentIntrinsics = selectedIntrinsics;
        currentType = field.Type;
      }

      var loweredIndex = LowerValueExpression(element.Index, context, TypeSymbol.I32);
      if (loweredIndex == null)
        return false;
      index = context.CreateTemporary(TypeSymbol.I32);
      context.EmitCopy(index, loweredIndex);
      arrays = currentArrays;
      intrinsics = currentIntrinsics;
      return true;
    }

    private static void EmitAggregateArraySet(
        IReadOnlyList<IrValue> arrays,
        IReadOnlyList<ArrayIntrinsicSymbols> intrinsics,
        IrValue index,
        TypeSymbol elementType,
        IrValue value,
        EventLoweringContext context)
    {
      var valueLeaves = IsAggregateStorageType(elementType)
          ? GetAggregateLeaves(value)
          : new[] { value };
      var descriptors = IsAggregateStorageType(elementType)
          ? AggregateLayout.GetLeaves(elementType)
          : new[] { new AggregateLeafDescriptor(elementType, Array.Empty<string>()) };
      for (var pass = 0; pass < 2; pass++)
      {
        for (var leafIndex = 0; leafIndex < arrays.Count; leafIndex++)
        {
          var isTag = leafIndex < descriptors.Count && descriptors[leafIndex].IsEnumTag;
          if ((pass == 0 && isTag) || (pass == 1 && !isTag))
            continue;
          if (leafIndex >= valueLeaves.Count || valueLeaves[leafIndex] == null)
            continue;
          context.Emit(new IrExternCallInstruction(
              intrinsics[leafIndex].SetterExternSignature,
              new[] { arrays[leafIndex], index, valueLeaves[leafIndex] },
              null));
        }
      }
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
      context.EmitCopy(
          result,
          new IrConstantValue(
              expression.Operator.Kind == BoundBinaryOperatorKind.LogicalOr,
              TypeSymbol.Bool,
              null));
      context.TerminateWithJump(mergeBlock.Label);

      context.SwitchTo(rhsBlock);
      var right = LowerValueExpression(expression.Right, context, TypeSymbol.Bool);
      if (right == null)
        return null;

      context.EmitCopy(result, right);
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

      if (callExpression.Method is ExternMethodSymbol externMethod &&
          externMethod.UsesAbiAdapter)
      {
        return LowerExternAbiCallExpression(
            callExpression,
            externMethod,
            context,
            preserveResult);
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

      if (callExpression.Method.ReturnType == TypeSymbol.Unit)
      {
        context.Emit(new IrExternCallInstruction(
            callExpression.Method.ExternSignature,
            arguments,
            null));
        return preserveResult
            ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
            : null;
      }

      var result = context.CreateTemporary(callExpression.Type);
      context.Emit(new IrExternCallInstruction(
          callExpression.Method.ExternSignature,
          arguments,
          result));
      return result;
    }

    private IrValue LowerExternAbiCallExpression(
        BoundCallExpression callExpression,
        ExternMethodSymbol method,
        EventLoweringContext context,
        bool preserveResult)
    {
      var logicalArguments = new IrValue[callExpression.Arguments.Count];
      for (var index = 0; index < logicalArguments.Length; index++)
      {
        logicalArguments[index] = LowerValueExpression(
            callExpression.Arguments[index],
            context,
            method.Parameters[index].Type);
        if (logicalArguments[index] == null)
          return null;
      }

      var logicalOutputTypes = GetExternLogicalOutputTypes(callExpression.Type);
      var physicalArguments = new List<IrValue>();
      if (!method.IsStatic)
        physicalArguments.Add(logicalArguments[0]);

      var outputs = new List<IrValue>();
      var maybeOutputProjections =
          new List<KeyValuePair<int, ExternMaybeOutputProjection>>();
      IrStorage abiReturnStorage = null;
      var outputIndex = 0;
      if (method.AbiReturnType != TypeSymbol.Unit)
      {
        var outputType = outputIndex < logicalOutputTypes.Count
            ? logicalOutputTypes[outputIndex]
            : method.AbiReturnType;
        abiReturnStorage = context.CreateTemporary(outputType);
        outputs.Add(abiReturnStorage);
        outputIndex++;
      }

      foreach (var parameter in method.AbiParameters)
      {
        switch (parameter.PassingMode)
        {
          case ExternParameterPassingMode.Normal:
            physicalArguments.Add(logicalArguments[parameter.LogicalInputOrdinal]);
            break;

          case ExternParameterPassingMode.In:
          {
            var input = context.CreateTemporary(parameter.Type);
            context.EmitCopy(input, logicalArguments[parameter.LogicalInputOrdinal]);
            physicalArguments.Add(input);
            break;
          }

          case ExternParameterPassingMode.Ref:
          {
            var outputType = outputIndex < logicalOutputTypes.Count
                ? logicalOutputTypes[outputIndex]
                : parameter.Type;
            var reference = context.CreateTemporary(outputType);
            context.EmitCopy(
                reference,
                logicalArguments[parameter.LogicalInputOrdinal]);
            physicalArguments.Add(reference);
            outputs.Add(reference);
            outputIndex++;
            break;
          }

          case ExternParameterPassingMode.Out:
          {
            var outputType = outputIndex < logicalOutputTypes.Count
                ? logicalOutputTypes[outputIndex]
                : parameter.Type;
            var output = context.CreateTemporary(
                parameter.MaybeProjection == null
                    ? outputType
                    : parameter.Type);
            physicalArguments.Add(output);
            outputs.Add(output);
            if (parameter.MaybeProjection != null)
            {
              maybeOutputProjections.Add(
                  new KeyValuePair<int, ExternMaybeOutputProjection>(
                      outputs.Count - 1,
                      parameter.MaybeProjection));
            }
            outputIndex++;
            break;
          }

          case ExternParameterPassingMode.GenericTypeArgument:
          {
            if (method.TypeArguments.Count <= physicalArguments.Count -
                (method.IsStatic ? 0 : 1))
            {
              Diagnostics.ReportLoweringError(
                  $"Generic extern '{method.DisplayName}' has incomplete type operand metadata.");
              return null;
            }
            var genericOperandIndex = 0;
            foreach (var previous in method.AbiParameters)
            {
              if (ReferenceEquals(previous, parameter))
                break;
              if (previous.PassingMode == ExternParameterPassingMode.GenericTypeArgument)
                genericOperandIndex++;
            }
            var argumentType = method.TypeArguments[genericOperandIndex];
            var runtimeType = argumentType.RuntimeClrType ??
                SobakasuTypeMapper.ResolveRuntimeType(argumentType.RuntimeQualifiedName);
            physicalArguments.Add(new IrConstantValue(runtimeType, parameter.Type));
            break;
          }
        }
      }

      context.Emit(new IrExternCallInstruction(
          method.ExternSignature,
          physicalArguments,
          abiReturnStorage));

      if (!preserveResult)
        return null;

      foreach (var pair in maybeOutputProjections)
      {
        var projected = LowerMaybeOutputProjection(
            outputs[pair.Key],
            pair.Value,
            context,
            "maybe_out");
        if (projected == null)
          return null;
        outputs[pair.Key] = projected;
      }

      if (outputs.Count == 0)
        return new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>());
      if (outputs.Count == 1)
        return outputs[0];
      return CreateExternLogicalAggregateResult(callExpression.Type, outputs);
    }

    private IrAggregateValue CreateExternLogicalAggregateResult(
        TypeSymbol resultType,
        IReadOnlyList<IrValue> outputs)
    {
      var values = new IrValue[AggregateLayout.GetLeaves(resultType).Count];
      for (var outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
      {
        var indices = AggregateLayout.GetFieldLeafIndices(
            resultType,
            resultType.AggregateFields[outputIndex]);
        var output = outputs[outputIndex];
        var leaves = IsAggregateStorageType(output.Type)
            ? GetAggregateLeaves(output)
            : new[] { output };
        if (indices.Count != leaves.Count)
        {
          Diagnostics.ReportLoweringError(
              $"Extern logical output {outputIndex} does not match its aggregate layout.");
          return null;
        }

        for (var leafIndex = 0; leafIndex < indices.Count; leafIndex++)
          values[indices[leafIndex]] = leaves[leafIndex];
      }

      return new IrAggregateValue(resultType, values);
    }

    private static IReadOnlyList<TypeSymbol> GetExternLogicalOutputTypes(TypeSymbol type)
    {
      if (type == TypeSymbol.Unit)
        return Array.Empty<TypeSymbol>();
      if (type.TypeKind == TypeKind.Tuple)
        return type.TupleElementTypes;
      return new[] { type };
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

      IrValue receiverValue = null;
      if (callExpression.Function.SelfParameter != null)
      {
        if (callExpression.Receiver == null)
        {
          Diagnostics.ReportLoweringError(
              $"Instance method '{callExpression.Function.DisplayName}' has no receiver.");
          return null;
        }

        receiverValue = LowerValueExpression(
            callExpression.Receiver,
            context,
            callExpression.Function.ContainingType);
        if (receiverValue == null)
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

      IrStorage resultStorage = null;
      if (callExpression.Function.ReturnType != TypeSymbol.Unit)
        resultStorage = context.CreateTemporary(callExpression.Function.ReturnType);

      var endBlock = context.CreateBlock("fn_end");
      var inlineFrame = new InlineFunctionFrame(
          callExpression.Function,
          endBlock.Label,
          resultStorage);

      if (callExpression.Function.SelfParameter != null)
      {
        var selfStorage = context.CreateTemporary(
            callExpression.Function.SelfParameter.Type);
        inlineFrame.SetParameterStorage(
            callExpression.Function.SelfParameter,
            selfStorage);
        context.EmitCopy(selfStorage, receiverValue);
      }

      for (var index = 0; index < callExpression.Function.Parameters.Count; index++)
      {
        var parameter = callExpression.Function.Parameters[index];
        var parameterStorage = context.CreateTemporary(parameter.Type);
        inlineFrame.SetParameterStorage(parameter, parameterStorage);
        context.EmitCopy(parameterStorage, argumentValues[index]);
      }

      context.PushInlineFrame(inlineFrame);
      try
      {
        LowerBlock(declaration.Body, context);
        if (context.CurrentBlock.Terminator == null)
        {
          inlineFrame.HasEndIncoming = true;
          context.TerminateWithJump(endBlock.Label);
        }
      }
      finally
      {
        context.PopInlineFrame();
      }

      if (!inlineFrame.HasEndIncoming)
      {
        context.RemoveBlock(endBlock);
        return null;
      }

      context.SwitchTo(endBlock);
      if (!preserveResult)
        return null;
      return callExpression.Function.ReturnType == TypeSymbol.Unit
          ? new IrAggregateValue(TypeSymbol.Unit, Array.Empty<IrValue>())
          : resultStorage;
    }

    private sealed class LoopLoweringFrame
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

    private sealed class EventLoweringContext
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
        if (IsAggregateStorageType(type))
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

        if (!IsAggregateStorageType(variable.Type))
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

        var sourceLeaves = GetAggregateLeaves(source);
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
}
