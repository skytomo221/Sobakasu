using System.Text;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Desugar;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Optimizer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;

namespace Skytomo221.Sobakasu.Compiler
{
  public static class SobakasuCompiler
  {
    public readonly struct CompileResult
    {
      public readonly bool Success;
      public readonly string Uasm;
      public readonly string ErrorText;

      public CompileResult(bool success, string uasm, string errorText)
      {
        Success = success;
        Uasm = uasm;
        ErrorText = errorText;
      }

      public static CompileResult Ok(string uasm) => new(true, uasm, "");
      public static CompileResult Fail(string errorText) => new(false, "", errorText);
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
        return CompileResult.Fail(errorText);
      }

      var desugarer = new SobakasuDesugarer();
      var desugaredProgram = desugarer.Desugar(boundProgram);
      diagnostics.AddRange(desugarer.Diagnostics);

      if (diagnostics.Diagnostics.Count > 0)
      {
        var errorText = FormatDiagnostics(text, diagnostics);
        return CompileResult.Fail(errorText);
      }

      var irLowerer = new SobakasuIrLowerer();
      var irProgram = irLowerer.Lower(desugaredProgram);
      diagnostics.AddRange(irLowerer.Diagnostics);

      if (diagnostics.Diagnostics.Count > 0)
      {
        var errorText = FormatDiagnostics(text, diagnostics);
        return CompileResult.Fail(errorText);
      }

      var optimizer = new SobakasuOptimizer();
      var optimizedProgram = optimizer.Optimize(irProgram);

      var uasmAssembler = new SobakasuUasmAssembler();
      var uasm = uasmAssembler.Assemble(optimizedProgram);
      diagnostics.AddRange(uasmAssembler.Diagnostics);

      if (diagnostics.Diagnostics.Count > 0)
      {
        var errorText = FormatDiagnostics(text, diagnostics);
        return CompileResult.Fail(errorText);
      }

      return CompileResult.Ok(uasm);
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
  }
}
