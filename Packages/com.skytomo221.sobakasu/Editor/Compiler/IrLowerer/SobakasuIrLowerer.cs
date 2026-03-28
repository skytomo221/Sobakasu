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

    private readonly ExternResolver _externResolver;

    public DiagnosticBag Diagnostics { get; } = new();

    public SobakasuIrLowerer()
        : this(SobakasuBuiltInEnvironment.Default.ExternResolver)
    {
    }

    internal SobakasuIrLowerer(ExternResolver externResolver)
    {
      _externResolver = externResolver ?? throw new ArgumentNullException(nameof(externResolver));
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

      if (!TryLowerValueExpression(
              statement.Initializer,
              statement.Variable.Type,
              module,
              assemblyProgram,
              constantSlots,
              context,
              ref constantIndex,
              ref localIndex,
              out var sourceSlot))
      {
        Diagnostics.ReportLoweringError(
            $"Unsupported initializer expression '{statement.Initializer.GetType().Name}'.");
        return;
      }

      EmitCopy(module, sourceSlot, targetSlot);
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
        LowerCallExpression(
            callExpression,
            module,
            assemblyProgram,
            constantSlots,
            context,
            ref constantIndex,
            ref localIndex);
        return;
      }

      if (statement.Expression is BoundAssignmentExpression assignmentExpression)
      {
        if (!TryLowerValueExpression(
                assignmentExpression,
                assignmentExpression.Variable.Type,
                module,
                assemblyProgram,
                constantSlots,
                context,
                ref constantIndex,
                ref localIndex,
                out _))
        {
          Diagnostics.ReportLoweringError(
              $"Unsupported assignment expression '{assignmentExpression.GetType().Name}'.");
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

    private void LowerCallExpression(
        BoundCallExpression callExpression,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        EventLoweringContext context,
        ref int constantIndex,
        ref int localIndex)
    {
      if (callExpression.Method == null)
      {
        Diagnostics.ReportLoweringError("Cannot lower unresolved method call.");
        return;
      }

      if (!_externResolver.TryResolve(callExpression.Method, out var resolvedExtern))
      {
        Diagnostics.ReportLoweringError(
            $"No extern signature mapping was found for '{callExpression.Method.DisplayName}'.");
        return;
      }

      if (callExpression.Arguments.Count != 1)
      {
        Diagnostics.ReportLoweringError(
            $"Unsupported argument count {callExpression.Arguments.Count} for '{callExpression.Method.Name}'.");
        return;
      }

      if (!TryLowerValueExpression(
              callExpression.Arguments[0],
              callExpression.Method.Parameters[0].Type,
              module,
              assemblyProgram,
              constantSlots,
              context,
              ref constantIndex,
              ref localIndex,
              out var slotName))
      {
        Diagnostics.ReportLoweringError(
            $"Unsupported call argument '{callExpression.Arguments[0].GetType().Name}' for '{callExpression.Method.DisplayName}'.");
        return;
      }

      module.AddInstruction(new AssemblyInstruction(InstructionKind.Push, slotName));
      module.AddInstruction(new AssemblyInstruction(InstructionKind.Extern, resolvedExtern.Signature));
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
        out string slotName)
    {
      slotName = null;

      if (expression is BoundLiteralExpression literal)
      {
        return TryGetOrCreateLiteralSlot(
            literal,
            expectedType,
            assemblyProgram,
            constantSlots,
            ref constantIndex,
            out slotName);
      }

      if (expression is BoundNameExpression nameExpression &&
          nameExpression.Symbol is LocalVariableSymbol local)
      {
        return context.LocalSlots.TryGetValue(local, out slotName);
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
                out var sourceSlot))
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

        EmitCopy(module, sourceSlot, targetSlot);
        slotName = targetSlot;
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
    }
  }
}
