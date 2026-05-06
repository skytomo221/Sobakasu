using System;
using System.Collections.Generic;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Assembly;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;

namespace Skytomo221.Sobakasu.Compiler.UasmAssembler
{
  internal sealed class SobakasuUasmAssembler
  {
    private const string ExitAddress = "0xFFFFFFFC";

    private readonly List<HeapPatchEntry> _heapPatches = new();

    public DiagnosticBag Diagnostics { get; } = new();
    public IReadOnlyList<HeapPatchEntry> HeapPatches => _heapPatches;

    public string Assemble(IrProgram program)
    {
      _heapPatches.Clear();

      var layout = new SlotLayoutBuilder(Diagnostics, _heapPatches);
      layout.Collect(program);

      var builder = new StringBuilder();
      builder.Append(".data_start\n\n");
      foreach (var slot in layout.DataSlots)
      {
        if (slot.IsExported)
          builder.AppendFormat("    .export {0}\n", slot.Name);

        if (!string.IsNullOrWhiteSpace(slot.SyncMode))
          builder.AppendFormat("    .sync {0}, {1}\n", slot.Name, slot.SyncMode);

        builder.AppendFormat(
            "    {0}: {1}, {2}\n",
            slot.Name,
            slot.TypeName,
            FormatValue(slot.TypeName, slot.InitialValue));
      }

      builder.Append("\n.data_end\n\n");
      builder.Append(".code_start\n");

      foreach (var module in program.Modules)
      {
        builder.AppendFormat("    .export {0}\n", module.ExportName);

        foreach (var block in module.Blocks)
        {
          builder.AppendFormat("    {0}:\n", block.Label);

          foreach (var instruction in block.Instructions)
            WriteInstruction(instruction, layout, builder);

          WriteTerminator(block.Terminator, layout, builder);
        }
      }

      builder.Append(".code_end\n");
      return builder.ToString();
    }

    private void WriteInstruction(
        IrInstruction instruction,
        SlotLayoutBuilder layout,
        StringBuilder builder)
    {
      switch (instruction)
      {
        case IrCopyInstruction copyInstruction:
          WriteIndentedLine(
              $"PUSH, {layout.GetSlotName(copyInstruction.Source)}",
              builder);
          WriteIndentedLine(
              $"PUSH, {layout.GetSlotName(copyInstruction.Target)}",
              builder);
          WriteIndentedLine("COPY", builder);
          return;

        case IrExternCallInstruction externCallInstruction:
          foreach (var argument in externCallInstruction.Arguments)
            WriteIndentedLine($"PUSH, {layout.GetSlotName(argument)}", builder);

          if (externCallInstruction.Result != null)
            WriteIndentedLine($"PUSH, {layout.GetSlotName(externCallInstruction.Result)}", builder);

          WriteIndentedLine(
              $"EXTERN, \"{externCallInstruction.ExternSignature}\"",
              builder);
          return;
      }

      Diagnostics.ReportAssemblerError(
          $"Unsupported IR instruction '{instruction?.GetType().Name ?? "<null>"}'.");
    }

    private void WriteTerminator(
        IrTerminator terminator,
        SlotLayoutBuilder layout,
        StringBuilder builder)
    {
      switch (terminator)
      {
        case IrJumpTerminator jumpTerminator:
          WriteIndentedLine($"JUMP, {jumpTerminator.TargetLabel}", builder);
          return;

        case IrConditionalJumpTerminator conditionalJumpTerminator:
          WriteIndentedLine(
              $"PUSH, {layout.GetSlotName(conditionalJumpTerminator.Condition)}",
              builder);
          WriteIndentedLine(
              $"JUMP_IF_FALSE, {conditionalJumpTerminator.FalseLabel}",
              builder);
          WriteIndentedLine($"JUMP, {conditionalJumpTerminator.TrueLabel}", builder);
          return;

        case IrReturnTerminator:
          WriteIndentedLine($"JUMP, {ExitAddress}", builder);
          return;

        case null:
          Diagnostics.ReportAssemblerError("IR basic block is missing a terminator.");
          return;
      }

      Diagnostics.ReportAssemblerError(
          $"Unsupported IR terminator '{terminator.GetType().Name}'.");
    }

    private static void WriteIndentedLine(string text, StringBuilder builder)
    {
      builder.AppendFormat("        {0}\n", text);
    }

    private static string FormatValue(string typeName, string initialValue)
    {
      if (typeName == "%SystemString")
      {
        if (initialValue == "null")
          return "null";

        if (initialValue.Length >= 2 &&
            initialValue[0] == '"' &&
            initialValue[^1] == '"')
        {
          return initialValue;
        }

        return $"\"{EscapeString(initialValue)}\"";
      }

      return initialValue;
    }

    private static string EscapeString(string value)
    {
      return value
          .Replace("\\", "\\\\")
          .Replace("\"", "\\\"");
    }

    private sealed class SlotLayoutBuilder
    {
      private readonly DiagnosticBag _diagnostics;
      private readonly IList<HeapPatchEntry> _heapPatches;
      private readonly List<AssemblyDataSlot> _dataSlots = new();
      private readonly Dictionary<LocalVariableSymbol, string> _localSlots = new();
      private readonly Dictionary<ParameterSymbol, string> _parameterSlots = new();
      private readonly Dictionary<string, string> _returnValueSlots = new(StringComparer.Ordinal);
      private readonly Dictionary<int, string> _temporarySlots = new();
      private readonly Dictionary<string, string> _constantSlots = new(StringComparer.Ordinal);
      private int _nextLocalId;
      private int _nextTemporaryId;
      private int _nextConstantId;

      public SlotLayoutBuilder(
          DiagnosticBag diagnostics,
          IList<HeapPatchEntry> heapPatches)
      {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _heapPatches = heapPatches ?? throw new ArgumentNullException(nameof(heapPatches));
        _dataSlots.Add(new AssemblyDataSlot("__exit_addr", "%SystemUInt32", ExitAddress));
        _dataSlots.Add(new AssemblyDataSlot("__jump_addr", "%SystemUInt32", ExitAddress));
      }

      public IReadOnlyList<AssemblyDataSlot> DataSlots => _dataSlots;

      public void Collect(IrProgram program)
      {
        foreach (var module in program.Modules)
        {
          foreach (var parameter in module.EventSymbol.Parameters)
            EnsureParameterSlot(parameter);

          if (!string.IsNullOrEmpty(module.EventSymbol.ReturnValueStorageName))
            EnsureReturnValueSlot(module.EventSymbol.ReturnValueStorageName);

          foreach (var block in module.Blocks)
          {
            foreach (var instruction in block.Instructions)
              CollectInstruction(instruction);

            CollectTerminator(block.Terminator);
          }
        }
      }

      public string GetSlotName(IrValue value)
      {
        return value switch
        {
          IrLocalStorage localStorage => _localSlots[localStorage.Variable],
          IrParameterStorage parameterStorage => _parameterSlots[parameterStorage.Parameter],
          IrReturnValueStorage returnValueStorage => _returnValueSlots[returnValueStorage.Name],
          IrTemporaryStorage temporaryStorage => _temporarySlots[temporaryStorage.Id],
          IrConstantValue constantValue => GetConstantSlotName(constantValue),
          _ => throw new InvalidOperationException(
              $"Unsupported IR value '{value?.GetType().Name ?? "<null>"}'.")
        };
      }

      private void CollectInstruction(IrInstruction instruction)
      {
        switch (instruction)
        {
          case IrCopyInstruction copyInstruction:
            EnsureStorageSlot(copyInstruction.Target);
            EnsureValueSlot(copyInstruction.Source);
            return;

          case IrExternCallInstruction externCallInstruction:
            foreach (var argument in externCallInstruction.Arguments)
              EnsureValueSlot(argument);

            if (externCallInstruction.Result != null)
              EnsureStorageSlot(externCallInstruction.Result);
            return;

          default:
            _diagnostics.ReportAssemblerError(
                $"Unsupported IR instruction '{instruction?.GetType().Name ?? "<null>"}'.");
            return;
        }
      }

      private void CollectTerminator(IrTerminator terminator)
      {
        if (terminator is IrConditionalJumpTerminator conditionalJumpTerminator)
          EnsureValueSlot(conditionalJumpTerminator.Condition);
      }

      private void EnsureValueSlot(IrValue value)
      {
        switch (value)
        {
          case IrLocalStorage localStorage:
            EnsureLocalSlot(localStorage.Variable);
            return;

          case IrParameterStorage parameterStorage:
            EnsureParameterSlot(parameterStorage.Parameter);
            return;

          case IrReturnValueStorage returnValueStorage:
            EnsureReturnValueSlot(returnValueStorage.Name);
            return;

          case IrTemporaryStorage temporaryStorage:
            EnsureTemporarySlot(temporaryStorage);
            return;

          case IrConstantValue constantValue:
            EnsureConstantSlot(constantValue);
            return;
        }

        _diagnostics.ReportAssemblerError(
            $"Unsupported IR value '{value?.GetType().Name ?? "<null>"}'.");
      }

      private void EnsureStorageSlot(IrStorage storage)
      {
        switch (storage)
        {
          case IrLocalStorage localStorage:
            EnsureLocalSlot(localStorage.Variable);
            return;

          case IrParameterStorage parameterStorage:
            EnsureParameterSlot(parameterStorage.Parameter);
            return;

          case IrReturnValueStorage returnValueStorage:
            EnsureReturnValueSlot(returnValueStorage.Name);
            return;

          case IrTemporaryStorage temporaryStorage:
            EnsureTemporarySlot(temporaryStorage);
            return;
        }

        _diagnostics.ReportAssemblerError(
            $"Unsupported IR storage '{storage?.GetType().Name ?? "<null>"}'.");
      }

      private void EnsureLocalSlot(LocalVariableSymbol variable)
      {
        if (_localSlots.ContainsKey(variable))
          return;

        if (!TryGetAssemblyTypeName(variable.Type, out var assemblyTypeName) ||
            !TryGetPlaceholderValue(variable.Type, out var initialValue))
        {
          _diagnostics.ReportAssemblerError(
              $"Unsupported local variable type '{variable.Type.Name}'.");
          return;
        }

        var slotName = $"__local_{_nextLocalId}";
        _nextLocalId++;
        _localSlots.Add(variable, slotName);
        _dataSlots.Add(new AssemblyDataSlot(slotName, assemblyTypeName, initialValue));
      }

      private void EnsureParameterSlot(ParameterSymbol parameter)
      {
        if (_parameterSlots.ContainsKey(parameter))
          return;

        if (!TryGetAssemblyTypeName(parameter.Type, out var assemblyTypeName) ||
            !TryGetPlaceholderValue(parameter.Type, out var initialValue))
        {
          _diagnostics.ReportAssemblerError(
              $"Unsupported event parameter type '{parameter.Type.Name}'.");
          return;
        }

        _parameterSlots.Add(parameter, parameter.UdonStorageName);
        _dataSlots.Add(new AssemblyDataSlot(
            parameter.UdonStorageName,
            assemblyTypeName,
            initialValue));
      }

      private void EnsureReturnValueSlot(string name)
      {
        if (_returnValueSlots.ContainsKey(name))
          return;

        _returnValueSlots.Add(name, name);
        _dataSlots.Add(new AssemblyDataSlot(name, "%SystemObject", "null"));
      }

      private void EnsureTemporarySlot(IrTemporaryStorage temporary)
      {
        if (_temporarySlots.ContainsKey(temporary.Id))
          return;

        if (!TryGetAssemblyTypeName(temporary.Type, out var assemblyTypeName) ||
            !TryGetPlaceholderValue(temporary.Type, out var initialValue))
        {
          _diagnostics.ReportAssemblerError(
              $"Unsupported temporary value type '{temporary.Type.Name}'.");
          return;
        }

        var slotName = $"__temp_{_nextTemporaryId}";
        _nextTemporaryId++;
        _temporarySlots.Add(temporary.Id, slotName);
        _dataSlots.Add(new AssemblyDataSlot(slotName, assemblyTypeName, initialValue));
      }

      private void EnsureConstantSlot(IrConstantValue constant)
      {
        var _ = GetConstantSlotName(constant);
      }

      private string GetConstantSlotName(IrConstantValue constant)
      {
        var key = BuildConstantKey(constant, out var runtimeValue);
        if (_constantSlots.TryGetValue(key, out var existingSlotName))
          return existingSlotName;

        if (!TryGetAssemblyTypeName(constant.Type, out var assemblyTypeName))
        {
          _diagnostics.ReportAssemblerError(
              $"Unsupported constant type '{constant.Type.Name}'.");
          return string.Empty;
        }

        var slotName = $"__const_{_nextConstantId}";
        _nextConstantId++;
        _constantSlots.Add(key, slotName);

        if (constant.Value == null)
        {
          _dataSlots.Add(new AssemblyDataSlot(slotName, assemblyTypeName, "null"));
          return slotName;
        }

        if (!TryGetPlaceholderValue(constant.Type, out var initialValue))
        {
          _diagnostics.ReportAssemblerError(
              $"Unsupported constant type '{constant.Type.Name}'.");
          return string.Empty;
        }

        _dataSlots.Add(new AssemblyDataSlot(slotName, assemblyTypeName, initialValue));
        _heapPatches.Add(new HeapPatchEntry(
            slotName,
            constant.Type.TypeKind,
            constant.Value,
            HeapPatchKind.Constant,
            constant.Span));
        return slotName;
      }

      private static string BuildConstantKey(
          IrConstantValue constant,
          out string runtimeValue)
      {
        if (constant.Value == null)
        {
          runtimeValue = "null";
          return $"{constant.Type.QualifiedName}:null";
        }

        runtimeValue = HeapPatchValueSerializer.SerializeRuntimeValue(
            constant.Value,
            constant.Type.TypeKind);
        return $"{constant.Type.QualifiedName}:{runtimeValue}";
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

        if (type == TypeSymbol.Object)
        {
          assemblyTypeName = "%SystemObject";
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

        if (type.TypeKind == TypeKind.Array &&
            TryGetAssemblyTypeName(type.ElementType, out var elementAssemblyTypeName))
        {
          assemblyTypeName = elementAssemblyTypeName + "Array";
          return true;
        }

        if (type.TypeKind == TypeKind.Named)
        {
          assemblyTypeName = "%" + ToUdonTypeName(type.QualifiedName);
          return true;
        }

        assemblyTypeName = null;
        return false;
      }

      private static string ToUdonTypeName(string qualifiedName)
      {
        return qualifiedName
            .Replace(".", string.Empty)
            .Replace("+", string.Empty);
      }

      private static bool TryGetPlaceholderValue(
          TypeSymbol type,
          out string initialValue)
      {
        if (type.TypeKind == TypeKind.Named ||
            type.TypeKind == TypeKind.Array)
        {
          initialValue = "null";
          return true;
        }

        try
        {
          initialValue = HeapPatchValueSerializer.GetPlaceholderValue(type.TypeKind);
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
}
