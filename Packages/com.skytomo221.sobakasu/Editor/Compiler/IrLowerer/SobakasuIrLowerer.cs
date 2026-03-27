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

      foreach (var @event in program.Events)
      {
        var exportAddress = new ExportAddress(@event.ExportName, @event.ExportName);
        var module = new AssemblyModule(@event.Name, exportAddress);

        foreach (var statement in @event.Body.Statements)
        {
          LowerStatement(statement, module, assemblyProgram, constantSlots, ref constantIndex);
        }

        module.AddInstruction(new AssemblyInstruction(InstructionKind.Jump, ExitAddress));
        assemblyProgram.AddModule(module);
      }

      return assemblyProgram;
    }

    private void LowerStatement(
        BoundStatement statement,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        ref int constantIndex)
    {
      if (statement is BoundExpressionStatement expressionStatement)
      {
        LowerExpression(
            expressionStatement.Expression,
            module,
            assemblyProgram,
            constantSlots,
            ref constantIndex);
        return;
      }

      Diagnostics.ReportLoweringError(
          $"Unsupported bound statement '{statement.GetType().Name}'.");
    }

    private void LowerExpression(
        BoundExpression expression,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        ref int constantIndex)
    {
      if (expression is BoundCallExpression callExpression)
      {
        LowerCallExpression(
            callExpression,
            module,
            assemblyProgram,
            constantSlots,
            ref constantIndex);
        return;
      }

      if (expression is BoundErrorExpression)
      {
        Diagnostics.ReportLoweringError("Cannot lower expression that already contains semantic errors.");
        return;
      }

      Diagnostics.ReportLoweringError(
          $"Unsupported bound expression '{expression.GetType().Name}'.");
    }

    private void LowerCallExpression(
        BoundCallExpression callExpression,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        ref int constantIndex)
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

      if (!TryGetOrCreateLiteralSlot(
              callExpression.Arguments[0],
              assemblyProgram,
              constantSlots,
              ref constantIndex,
              out var slotName))
      {
        Diagnostics.ReportLoweringError(
            $"Only primitive literal arguments are supported for '{callExpression.Method.DisplayName}'.");
        return;
      }

      module.AddInstruction(new AssemblyInstruction(InstructionKind.Push, slotName));
      module.AddInstruction(new AssemblyInstruction(InstructionKind.Extern, resolvedExtern.Signature));
    }

    private static bool TryGetOrCreateLiteralSlot(
        BoundExpression expression,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> constantSlots,
        ref int constantIndex,
        out string slotName)
    {
      slotName = null;

      if (expression is not BoundLiteralExpression literal)
        return false;

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
  }
}
