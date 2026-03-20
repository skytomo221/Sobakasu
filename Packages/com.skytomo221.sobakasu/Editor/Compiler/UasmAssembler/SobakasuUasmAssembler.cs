using System.Text;
using Skytomo221.Sobakasu.Compiler.Assembly;
using Skytomo221.Sobakasu.Compiler.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.UasmAssembler
{
  internal sealed class SobakasuUasmAssembler
  {
    public DiagnosticBag Diagnostics { get; } = new();

    public string Assemble(AssemblyProgram program)
    {
      var builder = new StringBuilder();

      builder.Append(".data_start\n\n");
      foreach (var slot in program.DataSlots)
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
        builder.AppendFormat("    .export {0}\n", module.ExportAddress.ExportName);
        builder.AppendFormat("    {0}:\n", module.ExportAddress.Label);

        foreach (var instruction in module.Instructions)
          WriteInstruction(instruction, builder);
      }

      builder.Append(".code_end\n");
      return builder.ToString();
    }

    private void WriteInstruction(AssemblyInstruction instruction, StringBuilder builder)
    {
      switch (instruction.Kind)
      {
        case InstructionKind.Nop:
          WriteIndentedLine("NOP", builder);
          break;

        case InstructionKind.Push:
          if (!TryRequireOperandCount(instruction, 1))
            break;
          WriteIndentedLine($"PUSH, {instruction.Operands[0]}", builder);
          break;

        case InstructionKind.Pop:
          WriteIndentedLine("POP", builder);
          break;

        case InstructionKind.Copy:
          WriteIndentedLine("COPY", builder);
          break;

        case InstructionKind.Jump:
          if (!TryRequireOperandCount(instruction, 1))
            break;
          WriteIndentedLine($"JUMP, {instruction.Operands[0]}", builder);
          break;

        case InstructionKind.Extern:
          if (!TryRequireOperandCount(instruction, 1))
            break;
          WriteIndentedLine($"EXTERN, \"{instruction.Operands[0]}\"", builder);
          break;

        case InstructionKind.Comment:
          if (!TryRequireOperandCount(instruction, 1))
            break;
          builder.AppendFormat("# {0}\n", instruction.Operands[0]);
          break;

        default:
          Diagnostics.ReportAssemblerError(
              $"Unsupported instruction kind '{instruction.Kind}'.");
          break;
      }
    }

    private bool TryRequireOperandCount(AssemblyInstruction instruction, int expected)
    {
      if (instruction.Operands.Count == expected)
        return true;

      Diagnostics.ReportAssemblerError(
          $"Instruction '{instruction.Kind}' expects {expected} operand(s), but got {instruction.Operands.Count}.");
      return false;
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
          return initialValue;

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
  }
}
