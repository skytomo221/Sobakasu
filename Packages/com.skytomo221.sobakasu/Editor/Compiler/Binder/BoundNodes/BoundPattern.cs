

using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal abstract class BoundPattern : BoundNode
    {
        protected BoundPattern(TextSpan span)
        {
            Span = span;
        }

        public TextSpan Span { get; }
    }
}
