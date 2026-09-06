

using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundInvalidPattern : BoundPattern
    {
        public BoundInvalidPattern(TextSpan span)
            : base(span)
        {
        }
    }
}
