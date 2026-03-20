using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Assembly;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
  internal sealed class SobakasuIrLowerer
  {
    private const string ExitAddress = "0xFFFFFFFC";
    private const string DebugLogExternSignature = "UnityEngineDebug.__Log__SystemObject__SystemVoid";

    public DiagnosticBag Diagnostics { get; } = new();

    public AssemblyProgram Lower(BoundProgram program)
    {
      var assemblyProgram = new AssemblyProgram();
      var stringSlots = new Dictionary<string, string>();

      assemblyProgram.AddDataSlot(
          new AssemblyDataSlot("__exit_addr", "%SystemUInt32", ExitAddress));
      assemblyProgram.AddDataSlot(
          new AssemblyDataSlot("__jump_addr", "%SystemUInt32", ExitAddress));

      var stringIndex = 0;

      foreach (var @event in program.Events)
      {
        var exportAddress = new ExportAddress(@event.ExportName, @event.ExportName);
        var module = new AssemblyModule(@event.Name, exportAddress);

        foreach (var statement in @event.Body.Statements)
        {
          LowerStatement(statement, module, assemblyProgram, stringSlots, ref stringIndex);
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
        IDictionary<string, string> stringSlots,
        ref int stringIndex)
    {
      if (statement is BoundExpressionStatement expressionStatement)
      {
        LowerExpression(expressionStatement.Expression, module, assemblyProgram, stringSlots, ref stringIndex);
        return;
      }

      Diagnostics.ReportLoweringError(
          $"Unsupported bound statement '{statement.GetType().Name}'.");
    }

    private void LowerExpression(
        BoundExpression expression,
        AssemblyModule module,
        AssemblyProgram assemblyProgram,
        IDictionary<string, string> stringSlots,
        ref int stringIndex)
    {
      if (expression is BoundCallExpression callExpression)
      {
        LowerCallExpression(callExpression, module, assemblyProgram, stringSlots, ref stringIndex);
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
        IDictionary<string, string> stringSlots,
        ref int stringIndex)
    {
      if (callExpression.Method == null)
      {
        Diagnostics.ReportLoweringError("Cannot lower unresolved method call.");
        return;
      }

      if (callExpression.Method.ExternSignature != DebugLogExternSignature)
      {
        Diagnostics.ReportLoweringError(
            $"Unsupported extern call '{callExpression.Method.ExternSignature}'.");
        return;
      }

      if (callExpression.Arguments.Count != 1)
      {
        Diagnostics.ReportLoweringError(
            $"Unsupported argument count {callExpression.Arguments.Count} for '{callExpression.Method.Name}'.");
        return;
      }

      if (callExpression.Arguments[0] is not BoundStringLiteralExpression stringLiteral)
      {
        Diagnostics.ReportLoweringError(
            $"Only string literal arguments are supported for '{callExpression.Method.Name}'.");
        return;
      }

      if (!stringSlots.TryGetValue(stringLiteral.Value, out var slotName))
      {
        slotName = $"__str_{stringIndex}";
        stringIndex++;
        stringSlots.Add(stringLiteral.Value, slotName);
        assemblyProgram.AddDataSlot(
            new AssemblyDataSlot(slotName, "%SystemString", stringLiteral.Value));
      }

      module.AddInstruction(new AssemblyInstruction(InstructionKind.Push, slotName));
      module.AddInstruction(new AssemblyInstruction(InstructionKind.Extern, callExpression.Method.ExternSignature));
    }
  }
}
