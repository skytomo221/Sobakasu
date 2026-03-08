using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Diagnostic
{
  public readonly struct Diagnostic
  {
    public DiagnosticSeverity Severity { get; }
    public string Code { get; }
    public TextSpan Span { get; }
    public string Message { get; }
    public string Hint { get; }

    public Diagnostic(
        DiagnosticSeverity severity,
        string code,
        TextSpan span,
        string message,
        string hint = "")
    {
      Severity = severity;
      Code = code;
      Span = span;
      Message = message;
      Hint = hint;
    }
  }
}
