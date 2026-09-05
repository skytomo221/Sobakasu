

using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class BoundWildcardPattern : BoundPattern
  {
    public BoundWildcardPattern(TextSpan span)
        : base(span)
    {
    }
  }
}
