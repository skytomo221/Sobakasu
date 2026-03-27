using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Desugar;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Optimizer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler
{
  public enum HeapPatchKind
  {
    Constant,
    GlobalInitializer,
    FieldInitializer,
    ArrayInitializer,
    UserDefinedValue
  }

  public sealed class HeapPatchEntry
  {
    public string SymbolName { get; }
    public TypeKind SymbolType { get; }
    public object RuntimeValue { get; }
    public HeapPatchKind Kind { get; }
    public TextSpan? SourceSpan { get; }

    public HeapPatchEntry(
        string symbolName,
        TypeKind symbolType,
        object runtimeValue,
        HeapPatchKind kind,
        TextSpan? sourceSpan = null)
    {
      SymbolName = symbolName ?? throw new ArgumentNullException(nameof(symbolName));
      SymbolType = symbolType;
      RuntimeValue = runtimeValue;
      Kind = kind;
      SourceSpan = sourceSpan;
    }
  }

  public static class SobakasuTypeMapper
  {
    public static Type ToSystemType(TypeKind type)
    {
      return type switch
      {
        TypeKind.Bool => typeof(bool),
        TypeKind.Char => typeof(char),
        TypeKind.I8 => typeof(sbyte),
        TypeKind.U8 => typeof(byte),
        TypeKind.I16 => typeof(short),
        TypeKind.U16 => typeof(ushort),
        TypeKind.I32 => typeof(int),
        TypeKind.U32 => typeof(uint),
        TypeKind.I64 => typeof(long),
        TypeKind.U64 => typeof(ulong),
        TypeKind.F32 => typeof(float),
        TypeKind.F64 => typeof(double),
        TypeKind.String => typeof(string),
        _ => throw new NotSupportedException(
            $"Sobakasu heap patch type '{type}' is not supported.")
      };
    }
  }

  internal static class HeapPatchValueSerializer
  {
    public static string SerializeRuntimeValue(object value, TypeKind type)
    {
      if (value == null)
      {
        throw new InvalidOperationException(
            $"Heap patch runtime value for '{type}' must not be null.");
      }

      return type switch
      {
        TypeKind.Bool when value is bool boolValue =>
            boolValue ? "true" : "false",
        TypeKind.Char when value is char charValue =>
            ((int)charValue).ToString(CultureInfo.InvariantCulture),
        TypeKind.I8 when value is sbyte int8Value =>
            int8Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.U8 when value is byte uint8Value =>
            uint8Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.I16 when value is short int16Value =>
            int16Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.U16 when value is ushort uint16Value =>
            uint16Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.I32 when value is int int32Value =>
            int32Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.U32 when value is uint uint32Value =>
            uint32Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.I64 when value is long int64Value =>
            int64Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.U64 when value is ulong uint64Value =>
            uint64Value.ToString(CultureInfo.InvariantCulture),
        TypeKind.F32 when value is float floatValue =>
            floatValue.ToString("R", CultureInfo.InvariantCulture),
        TypeKind.F64 when value is double doubleValue =>
            doubleValue.ToString("R", CultureInfo.InvariantCulture),
        TypeKind.String when value is string stringValue =>
            stringValue,
        _ => throw new InvalidOperationException(
            $"Heap patch runtime value '{value}' does not match Sobakasu type '{type}'.")
      };
    }

    public static object DeserializeRuntimeValue(string value, TypeKind type)
    {
      return type switch
      {
        TypeKind.Bool => value == "true",
        TypeKind.Char => (char)int.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.I8 => sbyte.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.U8 => byte.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.I16 => short.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.U16 => ushort.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.I32 => int.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.U32 => uint.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.I64 => long.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.U64 => ulong.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.F32 => float.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.F64 => double.Parse(value, CultureInfo.InvariantCulture),
        TypeKind.String => value ?? string.Empty,
        _ => throw new NotSupportedException(
            $"Sobakasu heap patch type '{type}' is not supported.")
      };
    }

    public static string GetPlaceholderValue(TypeKind type)
    {
      return type switch
      {
        TypeKind.String => string.Empty,
        TypeKind.Bool => "false",
        TypeKind.Char => "0",
        TypeKind.I8 => "0",
        TypeKind.U8 => "0",
        TypeKind.I16 => "0",
        TypeKind.U16 => "0",
        TypeKind.I32 => "0",
        TypeKind.U32 => "0",
        TypeKind.I64 => "0",
        TypeKind.U64 => "0",
        TypeKind.F32 => "0",
        TypeKind.F64 => "0",
        _ => throw new NotSupportedException(
            $"Sobakasu heap patch type '{type}' is not supported.")
      };
    }
  }

  public static class SobakasuCompiler
  {
    public readonly struct CompileResult
    {
      public readonly bool Success;
      public readonly string Uasm;
      public readonly string ErrorText;
      public readonly IReadOnlyList<HeapPatchEntry> HeapPatches;
      public readonly IReadOnlyList<DiagnosticItem> Diagnostics;

      public CompileResult(
          bool success,
          string uasm,
          string errorText,
          IReadOnlyList<HeapPatchEntry> heapPatches,
          IReadOnlyList<DiagnosticItem> diagnostics)
      {
        Success = success;
        Uasm = uasm;
        ErrorText = errorText;
        HeapPatches = heapPatches ?? Array.Empty<HeapPatchEntry>();
        Diagnostics = diagnostics ?? Array.Empty<DiagnosticItem>();
      }

      public static CompileResult Ok(
          string uasm,
          IReadOnlyList<HeapPatchEntry> heapPatches,
          IReadOnlyList<DiagnosticItem> diagnostics)
      {
        return new CompileResult(
            true,
            uasm,
            "",
            heapPatches ?? Array.Empty<HeapPatchEntry>(),
            diagnostics ?? Array.Empty<DiagnosticItem>());
      }

      public static CompileResult Fail(
          string errorText,
          IReadOnlyList<DiagnosticItem> diagnostics)
      {
        return new CompileResult(
            false,
            "",
            errorText,
            Array.Empty<HeapPatchEntry>(),
            diagnostics ?? Array.Empty<DiagnosticItem>());
      }
    }

    public static CompileResult CompileToUasm(string sourceText)
    {
      var text = SourceText.From(sourceText ?? string.Empty);
      var parser = new SobakasuParser(text);
      var syntax = parser.ParseCompilationUnit();

      var diagnostics = new DiagnosticBag();
      diagnostics.AddRange(parser.Diagnostics);

      var binder = new SobakasuBinder();
      var boundProgram = binder.BindProgram(syntax);
      diagnostics.AddRange(binder.Diagnostics);

      if (diagnostics.Diagnostics.Count > 0)
      {
        var errorText = FormatDiagnostics(text, diagnostics);
        return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
      }

      var desugarer = new SobakasuDesugarer();
      var desugaredProgram = desugarer.Desugar(boundProgram);
      diagnostics.AddRange(desugarer.Diagnostics);

      if (diagnostics.Diagnostics.Count > 0)
      {
        var errorText = FormatDiagnostics(text, diagnostics);
        return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
      }

      var irLowerer = new SobakasuIrLowerer();
      var irProgram = irLowerer.Lower(desugaredProgram);
      diagnostics.AddRange(irLowerer.Diagnostics);

      if (diagnostics.Diagnostics.Count > 0)
      {
        var errorText = FormatDiagnostics(text, diagnostics);
        return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
      }

      var optimizer = new SobakasuOptimizer();
      var optimizedProgram = optimizer.Optimize(irProgram);

      var uasmAssembler = new SobakasuUasmAssembler();
      var uasm = uasmAssembler.Assemble(optimizedProgram);
      diagnostics.AddRange(uasmAssembler.Diagnostics);

      if (diagnostics.Diagnostics.Count > 0)
      {
        var errorText = FormatDiagnostics(text, diagnostics);
        return CompileResult.Fail(errorText, CopyDiagnostics(diagnostics));
      }

      return CompileResult.Ok(
          uasm,
          CopyHeapPatches(optimizedProgram),
          CopyDiagnostics(diagnostics));
    }

    private static string FormatDiagnostics(SourceText sourceText, DiagnosticBag diagnostics)
    {
      var builder = new StringBuilder();

      foreach (var diagnostic in diagnostics.Diagnostics)
      {
        var line = sourceText.GetLineFromPosition(diagnostic.Span.Start);
        var lineIndex = GetLineIndex(sourceText, line);
        var column = diagnostic.Span.Start - line.Start + 1;

        builder.AppendFormat(
            "{0} {1} (line {2}, col {3}): {4}\n",
            diagnostic.Severity,
            diagnostic.Code,
            lineIndex + 1,
            column,
            diagnostic.Message);

        if (!string.IsNullOrWhiteSpace(diagnostic.Hint))
          builder.AppendFormat("  hint: {0}\n", diagnostic.Hint);
      }

      return TrimTrailingLineBreaks(builder.ToString());
    }

    private static int GetLineIndex(SourceText sourceText, TextLine targetLine)
    {
      for (var index = 0; index < sourceText.Lines.Count; index++)
      {
        if (ReferenceEquals(sourceText.Lines[index], targetLine))
          return index;
      }

      return 0;
    }

    private static string TrimTrailingLineBreaks(string text)
    {
      if (string.IsNullOrEmpty(text))
        return string.Empty;

      var end = text.Length;
      while (end > 0 &&
             (text[end - 1] == '\r' || text[end - 1] == '\n'))
      {
        end--;
      }

      if (end == text.Length)
        return text;

      return text.Substring(0, end);
    }

    private static IReadOnlyList<DiagnosticItem> CopyDiagnostics(DiagnosticBag diagnostics)
    {
      if (diagnostics.Diagnostics.Count == 0)
        return Array.Empty<DiagnosticItem>();

      return new List<DiagnosticItem>(diagnostics.Diagnostics).ToArray();
    }

    private static IReadOnlyList<HeapPatchEntry> CopyHeapPatches(
        Assembly.AssemblyProgram program)
    {
      if (program.HeapPatches.Count == 0)
        return Array.Empty<HeapPatchEntry>();

      return new List<HeapPatchEntry>(program.HeapPatches).ToArray();
    }
  }
}
