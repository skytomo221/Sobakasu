namespace Skytomo221.Sobakasu.Compiler.Lexer
{
  public readonly struct Diagnostic
  {
    public readonly DiagnosticSeverity Severity;
    public readonly TextSpan Span;
    public readonly string Message;

    public Diagnostic(DiagnosticSeverity severity, TextSpan span, string message)
    {
      Severity = severity;
      Span = span;
      Message = message;
    }

    public override string ToString() => $"{Severity}: {Message} @ {Span}";
  }
}
