using System;
using System.Collections.Generic;
using System.Globalization;
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

      if (!TryFormatLiteralValue(literal, out var initialValue))
        return false;

      var key = $"{literal.Type.QualifiedName}:{initialValue}";
      if (!constantSlots.TryGetValue(key, out slotName))
      {
        slotName = $"__const_{constantIndex}";
        constantIndex++;
        constantSlots.Add(key, slotName);
        assemblyProgram.AddDataSlot(
            new AssemblyDataSlot(slotName, assemblyTypeName, initialValue));
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

    private static bool TryFormatLiteralValue(
        BoundLiteralExpression literal,
        out string initialValue)
    {
      if (literal.Type == TypeSymbol.String && literal.Value is string stringValue)
      {
        initialValue = stringValue;
        return true;
      }

      if (literal.Type == TypeSymbol.Bool && literal.Value is bool boolValue)
      {
        initialValue = boolValue ? "true" : "false";
        return true;
      }

      if (literal.Type == TypeSymbol.I8 && literal.Value is sbyte int8Value)
      {
        initialValue = int8Value.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.U8 && literal.Value is byte uint8Value)
      {
        initialValue = uint8Value.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.I16 && literal.Value is short int16Value)
      {
        initialValue = int16Value.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.U16 && literal.Value is ushort uint16Value)
      {
        initialValue = uint16Value.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.I32 && literal.Value is int intValue)
      {
        initialValue = intValue.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.U32 && literal.Value is uint uintValue)
      {
        initialValue = uintValue.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.I64 && literal.Value is long int64Value)
      {
        initialValue = int64Value.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.U64 && literal.Value is ulong uint64Value)
      {
        initialValue = uint64Value.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.F32 && literal.Value is float floatValue)
      {
        initialValue = floatValue.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.F64 && literal.Value is double doubleValue)
      {
        initialValue = doubleValue.ToString(CultureInfo.InvariantCulture);
        return true;
      }

      if (literal.Type == TypeSymbol.Char && literal.Value is char charValue)
      {
        initialValue = ((int)charValue).ToString(CultureInfo.InvariantCulture);
        return true;
      }

      initialValue = null;
      return false;
    }
  }
}
