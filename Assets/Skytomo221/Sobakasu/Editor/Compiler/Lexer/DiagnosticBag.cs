using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Lexer
{
  public sealed class DiagnosticBag
  {
    private readonly List<Diagnostic> _list = new();

    public void ReportError(TextSpan span, string message)
        => _list.Add(new Diagnostic(DiagnosticSeverity.Error, span, message));

    public void ReportWarning(TextSpan span, string message)
        => _list.Add(new Diagnostic(DiagnosticSeverity.Warning, span, message));

    public IReadOnlyList<Diagnostic> Diagnostics => _list;
    public bool HasErrors
    {
      get
      {
        foreach (var d in _list)
          if (d.Severity == DiagnosticSeverity.Error) return true;
        return false;
      }
    }
  }
}
