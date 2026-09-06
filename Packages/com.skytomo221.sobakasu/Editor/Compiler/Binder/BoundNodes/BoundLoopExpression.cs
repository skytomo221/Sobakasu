using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundLoopExpression : BoundExpression
    {
        public LoopSymbol Loop { get; }
        public BoundBlockExpression Body { get; }
        public override TypeSymbol Type { get; }

        public BoundLoopExpression(
            LoopSymbol loop,
            BoundBlockExpression body,
            TypeSymbol type)
        {
            Loop = loop ?? throw new ArgumentNullException(nameof(loop));
            Body = body ?? throw new ArgumentNullException(nameof(body));
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }
    }
}
