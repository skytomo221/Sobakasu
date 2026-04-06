using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Assembly;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
  internal sealed class SobakasuIrLowerer
  {
    private const string ExitAddress = "0xFFFFFFFC";

    public DiagnosticBag Diagnostics { get; } = new();

    public SobakasuIrLowerer()
    {
    }

    public AssemblyProgram Lower(BoundProgram program)
    {
      var assemblyProgram = new AssemblyProgram();
      var constantSlots = new Dictionary<string, string>(StringComparer.Ordinal);

      assemblyProgram.AddDataSlot(
          new AssemblyDataSlot("__exit_addr", "%SystemUInt32", ExitAddress));
      assemblyProgram.AddDataSlot(
          new AssemblyDataSlot("__jump_addr", "%SystemUInt32", ExitAddress));

      var constantIndex = 0;
      var localIndex = 0;

      foreach (var @event in program.Events)
      {
        var exportAddress = new ExportAddress(@event.ExportName, @event.ExportName);
        var module = new AssemblyModule(@event.Name, exportAddress);
        var context = new EventLoweringContext();

        LowerBlock(
            @event.Body,
            module,
            assemblyProgram,
            constantSlots,
            context,
            ref constantIndex,
            ref localIndex);

        module.AddInstruction(new AssemblyInstruction(InstructionKind.Jump, ExitAddress));
        assemblyProgram.AddModule(module);
      }

      return assemblyProgram;
    }

    private void LowerBlock(
        BoundBlockStatement block,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        EventLoweringContext context,
        ref int constantIndex,
        ref int localIndex)
    {
      foreach (var statement in block.Statements)
      {
        LowerStatement(
            statement,
            module,
            assemblyProgram,
            constantSlots,
            context,
            ref constantIndex,
            ref localIndex);
      }
    }

    private void LowerStatement(
        BoundStatement statement,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        EventLoweringContext context,
        ref int constantIndex,
        ref int localIndex)
    {
      if (statement is BoundBlockStatement blockStatement)
      {
        LowerBlock(
            blockStatement,
            module,
            assemblyProgram,
            constantSlots,
            context,
            ref constantIndex,
            ref localIndex);
        return;
      }

      if (statement is BoundVariableDeclarationStatement variableDeclarationStatement)
      {
        LowerVariableDeclarationStatement(
            variableDeclarationStatement,
            module,
            assemblyProgram,
            constantSlots,
            context,
            ref constantIndex,
            ref localIndex);
        return;
      }

      if (statement is BoundExpressionStatement expressionStatement)
      {
        LowerExpressionStatement(
            expressionStatement,
            module,
            assemblyProgram,
            constantSlots,
            context,
            ref constantIndex,
            ref localIndex);
        return;
      }

      Diagnostics.ReportLoweringError(
          $"Unsupported bound statement '{statement.GetType().Name}'.");
    }

    private void LowerVariableDeclarationStatement(
        BoundVariableDeclarationStatement statement,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        EventLoweringContext context,
        ref int constantIndex,
        ref int localIndex)
    {
      if (!TryEnsureLocalSlot(
              statement.Variable,
              assemblyProgram,
              context,
              ref localIndex,
              out var targetSlot))
      {
        Diagnostics.ReportLoweringError(
            $"Unsupported local variable type '{statement.Variable.Type.Name}'.");
        return;
      }

      var diagnosticCount = Diagnostics.Diagnostics.Count;
      if (!TryLowerValueExpression(
              statement.Initializer,
              statement.Variable.Type,
              module,
              assemblyProgram,
              constantSlots,
              context,
              ref constantIndex,
              ref localIndex,
              out var sourceValue))
      {
        if (Diagnostics.Diagnostics.Count == diagnosticCount)
        {
          Diagnostics.ReportLoweringError(
              $"Unsupported initializer expression '{statement.Initializer.GetType().Name}'.");
        }

        return;
      }

      EmitCopy(module, sourceValue.SlotName, targetSlot);
      ReleaseTemporaryIfNeeded(sourceValue, context);
    }

    private void LowerExpressionStatement(
        BoundExpressionStatement statement,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        EventLoweringContext context,
        ref int constantIndex,
        ref int localIndex)
    {
      if (statement.Expression is BoundCallExpression callExpression)
      {
        var diagnosticCount = Diagnostics.Diagnostics.Count;
        if (!TryLowerCallExpression(
                callExpression,
                module,
                assemblyProgram,
                constantSlots,
                context,
                ref constantIndex,
                ref localIndex,
                preserveResult: false,
                out _)
            && Diagnostics.Diagnostics.Count == diagnosticCount)
        {
          Diagnostics.ReportLoweringError(
              $"Unsupported call expression '{callExpression.GetType().Name}'.");
        }

        return;
      }

      if (statement.Expression is BoundAssignmentExpression assignmentExpression)
      {
        var diagnosticCount = Diagnostics.Diagnostics.Count;
        if (!TryLowerValueExpression(
                assignmentExpression,
                assignmentExpression.Variable.Type,
                module,
                assemblyProgram,
                constantSlots,
                context,
                ref constantIndex,
                ref localIndex,
                out var loweredValue))
        {
          if (Diagnostics.Diagnostics.Count == diagnosticCount)
          {
            Diagnostics.ReportLoweringError(
                $"Unsupported assignment expression '{assignmentExpression.GetType().Name}'.");
          }
        }
        else
        {
          ReleaseTemporaryIfNeeded(loweredValue, context);
        }

        return;
      }

      if (statement.Expression is BoundErrorExpression)
      {
        Diagnostics.ReportLoweringError("Cannot lower expression that already contains semantic errors.");
        return;
      }

      Diagnostics.ReportLoweringError(
          $"Unsupported expression statement '{statement.Expression.GetType().Name}'.");
    }

    private bool TryLowerCallExpression(
        BoundCallExpression callExpression,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        EventLoweringContext context,
        ref int constantIndex,
        ref int localIndex,
        bool preserveResult,
        out LoweredValue loweredValue)
    {
      loweredValue = default;

      if (callExpression.Method == null)
      {
        Diagnostics.ReportLoweringError("Cannot lower unresolved method call.");
        return false;
      }

      if (string.IsNullOrEmpty(callExpression.Method.ExternSignature))
      {
        Diagnostics.ReportLoweringError(
            $"No extern signature was selected for '{callExpression.Method.DisplayName}'.");
        return false;
      }

      if (callExpression.Arguments.Count != callExpression.Method.Parameters.Count)
      {
        Diagnostics.ReportLoweringError(
            $"Argument count mismatch for '{callExpression.Method.DisplayName}'.");
        return false;
      }

      var loweredArguments = new LoweredValue[callExpression.Arguments.Count];
      for (var index = 0; index < callExpression.Arguments.Count; index++)
      {
        var diagnosticCount = Diagnostics.Diagnostics.Count;
        if (!TryLowerValueExpression(
                callExpression.Arguments[index],
                callExpression.Method.Parameters[index].Type,
                module,
                assemblyProgram,
                constantSlots,
                context,
                ref constantIndex,
                ref localIndex,
                out loweredArguments[index]))
        {
          ReleaseTemporaryValues(loweredArguments, index, context);

          if (Diagnostics.Diagnostics.Count == diagnosticCount)
          {
            Diagnostics.ReportLoweringError(
                $"Unsupported call argument '{callExpression.Arguments[index].GetType().Name}' for '{callExpression.Method.DisplayName}'.");
          }

          return false;
        }
      }

      var hasReturnValue = callExpression.Method.ReturnType != TypeSymbol.Void;
      if (!hasReturnValue && preserveResult)
      {
        ReleaseTemporaryValues(loweredArguments, loweredArguments.Length, context);
        Diagnostics.ReportLoweringError(
            $"Cannot use void-returning call '{callExpression.Method.DisplayName}' as a value.");
        return false;
      }

      if (hasReturnValue &&
          !TryAcquireTemporarySlot(
              callExpression.Method.ReturnType,
              assemblyProgram,
              context,
              out loweredValue))
      {
        ReleaseTemporaryValues(loweredArguments, loweredArguments.Length, context);
        Diagnostics.ReportLoweringError(
            $"Unsupported return type '{callExpression.Method.ReturnType.Name}' for '{callExpression.Method.DisplayName}'.");
        return false;
      }

      EmitExternCall(
          module,
          loweredArguments,
          hasReturnValue ? loweredValue.SlotName : null,
          callExpression.Method.ExternSignature);
      ReleaseTemporaryValues(loweredArguments, loweredArguments.Length, context);

      if (!preserveResult)
      {
        ReleaseTemporaryIfNeeded(loweredValue, context);
        loweredValue = default;
      }

      return true;
    }

    private bool TryLowerValueExpression(
        BoundExpression expression,
        TypeSymbol expectedType,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        EventLoweringContext context,
        ref int constantIndex,
        ref int localIndex,
        out LoweredValue loweredValue)
    {
      loweredValue = default;

      if (expression is BoundLiteralExpression literal)
      {
        if (TryGetOrCreateLiteralSlot(
            literal,
            expectedType,
            assemblyProgram,
            constantSlots,
            ref constantIndex,
            out var literalSlot))
        {
          loweredValue = new LoweredValue(literalSlot, literal.Type, false);
          return true;
        }

        return false;
      }

      if (expression is BoundNameExpression nameExpression &&
          nameExpression.Symbol is LocalVariableSymbol local)
      {
        if (context.LocalSlots.TryGetValue(local, out var localSlot))
        {
          loweredValue = new LoweredValue(localSlot, local.Type, false);
          return true;
        }

        return false;
      }

      if (expression is BoundCallExpression callExpression)
      {
        return TryLowerCallExpression(
            callExpression,
            module,
            assemblyProgram,
            constantSlots,
            context,
            ref constantIndex,
            ref localIndex,
            preserveResult: true,
            out loweredValue);
      }

      if (expression is BoundAssignmentExpression assignmentExpression)
      {
        if (!TryLowerValueExpression(
                assignmentExpression.Expression,
                assignmentExpression.Variable.Type,
                module,
                assemblyProgram,
                constantSlots,
                context,
                ref constantIndex,
                ref localIndex,
                out var sourceValue))
        {
          return false;
        }

        if (!TryEnsureLocalSlot(
                assignmentExpression.Variable,
                assemblyProgram,
                context,
                ref localIndex,
                out var targetSlot))
        {
          return false;
        }

        EmitCopy(module, sourceValue.SlotName, targetSlot);
        ReleaseTemporaryIfNeeded(sourceValue, context);
        loweredValue = new LoweredValue(targetSlot, assignmentExpression.Variable.Type, false);
        return true;
      }

      return false;
    }

    private bool TryEnsureLocalSlot(
        LocalVariableSymbol local,
        AssemblyProgram assemblyProgram,
        EventLoweringContext context,
        ref int localIndex,
        out string slotName)
    {
      if (context.LocalSlots.TryGetValue(local, out slotName))
        return true;

      if (!TryGetAssemblyTypeName(local.Type, out var assemblyTypeName) ||
          !TryGetPlaceholderValue(local.Type.TypeKind, out var initialValue))
      {
        slotName = null;
        return false;
      }

      slotName = $"__local_{localIndex}";
      localIndex++;

      context.LocalSlots.Add(local, slotName);
      assemblyProgram.AddDataSlot(
          new AssemblyDataSlot(slotName, assemblyTypeName, initialValue));
      return true;
    }

    private bool TryAcquireTemporarySlot(
        TypeSymbol type,
        AssemblyProgram assemblyProgram,
        EventLoweringContext context,
        out LoweredValue loweredValue)
    {
      loweredValue = default;

      if (context.AvailableTemporarySlots.TryGetValue(type, out var availableSlots) &&
          availableSlots.Count > 0)
      {
        loweredValue = new LoweredValue(availableSlots.Pop(), type, true);
        return true;
      }

      if (!TryGetAssemblyTypeName(type, out var assemblyTypeName) ||
          !TryGetPlaceholderValue(type.TypeKind, out var initialValue))
      {
        return false;
      }

      var slotName = $"__temp_{context.TemporarySlotCount}";
      context.TemporarySlotCount++;

      if (!context.AllTemporarySlots.TryGetValue(type, out var allSlots))
      {
        allSlots = new List<string>();
        context.AllTemporarySlots.Add(type, allSlots);
      }

      allSlots.Add(slotName);
      assemblyProgram.AddDataSlot(
          new AssemblyDataSlot(slotName, assemblyTypeName, initialValue));
      loweredValue = new LoweredValue(slotName, type, true);
      return true;
    }

    private static void EmitExternCall(
        AssemblyModule module,
        IReadOnlyList<LoweredValue> arguments,
        string resultSlot,
        string externSignature)
    {
      for (var index = 0; index < arguments.Count; index++)
        module.AddInstruction(new AssemblyInstruction(InstructionKind.Push, arguments[index].SlotName));

      if (!string.IsNullOrEmpty(resultSlot))
        module.AddInstruction(new AssemblyInstruction(InstructionKind.Push, resultSlot));

      module.AddInstruction(new AssemblyInstruction(InstructionKind.Extern, externSignature));
    }

    private static void ReleaseTemporaryValues(
        IReadOnlyList<LoweredValue> values,
        int count,
        EventLoweringContext context)
    {
      for (var index = 0; index < count; index++)
        ReleaseTemporaryIfNeeded(values[index], context);
    }

    private static void ReleaseTemporaryIfNeeded(
        LoweredValue loweredValue,
        EventLoweringContext context)
    {
      if (!loweredValue.IsTemporary || string.IsNullOrEmpty(loweredValue.SlotName))
        return;

      if (!context.AvailableTemporarySlots.TryGetValue(loweredValue.Type, out var availableSlots))
      {
        availableSlots = new Stack<string>();
        context.AvailableTemporarySlots.Add(loweredValue.Type, availableSlots);
      }

      availableSlots.Push(loweredValue.SlotName);
    }

    private static void EmitCopy(
        AssemblyModule module,
        string sourceSlot,
        string targetSlot)
    {
      module.AddInstruction(new AssemblyInstruction(InstructionKind.Push, sourceSlot));
      module.AddInstruction(new AssemblyInstruction(InstructionKind.Push, targetSlot));
      module.AddInstruction(new AssemblyInstruction(InstructionKind.Copy));
    }

    private static bool TryGetOrCreateLiteralSlot(
        BoundLiteralExpression literal,
        TypeSymbol expectedType,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        ref int constantIndex,
        out string slotName)
    {
      slotName = null;

      if (literal.Type == TypeSymbol.Null)
      {
        return TryGetOrCreateNullSlot(
            expectedType,
            assemblyProgram,
            constantSlots,
            ref constantIndex,
            out slotName);
      }

      if (!TryGetAssemblyTypeName(literal.Type, out var assemblyTypeName))
        return false;

      var symbolType = literal.Type.TypeKind;
      if (!TryGetPlaceholderValue(symbolType, out var initialValue))
        return false;

      string runtimeValue;
      try
      {
        runtimeValue = HeapPatchValueSerializer.SerializeRuntimeValue(
            literal.Value,
            symbolType);
      }
      catch
      {
        return false;
      }

      var key = $"{literal.Type.QualifiedName}:{runtimeValue}";
      if (!constantSlots.TryGetValue(key, out slotName))
      {
        slotName = $"__const_{constantIndex}";
        constantIndex++;
        constantSlots.Add(key, slotName);
        assemblyProgram.AddDataSlot(
            new AssemblyDataSlot(slotName, assemblyTypeName, initialValue));
        assemblyProgram.AddHeapPatch(
            new HeapPatchEntry(
                slotName,
                symbolType,
                literal.Value,
                HeapPatchKind.Constant,
                literal.Span));
      }

      return true;
    }

    private static bool TryGetOrCreateNullSlot(
        TypeSymbol expectedType,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        ref int constantIndex,
        out string slotName)
    {
      slotName = null;

      if (expectedType == null ||
          !expectedType.IsReferenceType ||
          !TryGetAssemblyTypeName(expectedType, out var assemblyTypeName))
      {
        return false;
      }

      var key = $"{expectedType.QualifiedName}:null";
      if (!constantSlots.TryGetValue(key, out slotName))
      {
        slotName = $"__const_{constantIndex}";
        constantIndex++;
        constantSlots.Add(key, slotName);
        assemblyProgram.AddDataSlot(
            new AssemblyDataSlot(slotName, assemblyTypeName, "null"));
      }

      return true;
    }

    private static bool TryGetAssemblyTypeName(TypeSymbol type, out string assemblyTypeName)
    {
      if (type == TypeSymbol.String)
      {
        assemblyTypeName = "%SystemString";
        return true;
      }

      if (type == TypeSymbol.Bool)
      {
        assemblyTypeName = "%SystemBoolean";
        return true;
      }

      if (type == TypeSymbol.I8)
      {
        assemblyTypeName = "%SystemSByte";
        return true;
      }

      if (type == TypeSymbol.U8)
      {
        assemblyTypeName = "%SystemByte";
        return true;
      }

      if (type == TypeSymbol.I16)
      {
        assemblyTypeName = "%SystemInt16";
        return true;
      }

      if (type == TypeSymbol.U16)
      {
        assemblyTypeName = "%SystemUInt16";
        return true;
      }

      if (type == TypeSymbol.I32)
      {
        assemblyTypeName = "%SystemInt32";
        return true;
      }

      if (type == TypeSymbol.U32)
      {
        assemblyTypeName = "%SystemUInt32";
        return true;
      }

      if (type == TypeSymbol.I64)
      {
        assemblyTypeName = "%SystemInt64";
        return true;
      }

      if (type == TypeSymbol.U64)
      {
        assemblyTypeName = "%SystemUInt64";
        return true;
      }

      if (type == TypeSymbol.F32)
      {
        assemblyTypeName = "%SystemSingle";
        return true;
      }

      if (type == TypeSymbol.F64)
      {
        assemblyTypeName = "%SystemDouble";
        return true;
      }

      if (type == TypeSymbol.Char)
      {
        assemblyTypeName = "%SystemChar";
        return true;
      }

      assemblyTypeName = null;
      return false;
    }

    private static bool TryGetPlaceholderValue(
        TypeKind type,
        out string initialValue)
    {
      try
      {
        initialValue = HeapPatchValueSerializer.GetPlaceholderValue(type);
        return true;
      }
      catch
      {
        initialValue = null;
        return false;
      }
    }

    private sealed class EventLoweringContext
    {
      public Dictionary<LocalVariableSymbol, string> LocalSlots { get; } =
          new Dictionary<LocalVariableSymbol, string>();
      public Dictionary<TypeSymbol, List<string>> AllTemporarySlots { get; } =
          new Dictionary<TypeSymbol, List<string>>();
      public Dictionary<TypeSymbol, Stack<string>> AvailableTemporarySlots { get; } =
          new Dictionary<TypeSymbol, Stack<string>>();
      public int TemporarySlotCount { get; set; }
    }

    private readonly struct LoweredValue
    {
      public string SlotName { get; }
      public TypeSymbol Type { get; }
      public bool IsTemporary { get; }

      public LoweredValue(string slotName, TypeSymbol type, bool isTemporary)
      {
        SlotName = slotName;
        Type = type;
        IsTemporary = isTemporary;
      }
    }
  }
}
