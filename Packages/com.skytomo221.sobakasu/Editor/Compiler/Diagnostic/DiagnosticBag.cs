using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Diagnostic
{
    public class DiagnosticBag
    {
        private readonly List<Diagnostic> _diagnostics = new();

        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
        public string SourcePath { get; set; } = string.Empty;

        public bool HasErrors
        {
            get
            {
                foreach (var diagnostic in _diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public void Report(in Diagnostic diagnostic)
        {
            if (string.IsNullOrEmpty(diagnostic.SourcePath) &&
                !string.IsNullOrEmpty(SourcePath))
            {
                _diagnostics.Add(new Diagnostic(
                    diagnostic.Severity,
                    diagnostic.Code,
                    diagnostic.Span,
                    diagnostic.Message,
                    diagnostic.Hint,
                    SourcePath));
                return;
            }

            _diagnostics.Add(diagnostic);
        }

        public void AddRange(DiagnosticBag bag)
            => _diagnostics.AddRange(bag.Diagnostics);
    }
}

