

using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class LoopSymbol
    {
        public LoopSymbol(string label, bool isWhile, TextSpan sourceSpan)
        {
            Label = label;
            IsWhile = isWhile;
            SourceSpan = sourceSpan;
        }

        public string Label { get; }
        public bool IsWhile { get; }
        public TextSpan SourceSpan { get; }
    }
}
