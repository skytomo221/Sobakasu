using System;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundLiteralExpression : BoundExpression
    {
        public object Value { get; }
        public override TypeSymbol Type { get; }
        public TextSpan Span { get; }

        public BoundLiteralExpression(object value, TypeSymbol type, TextSpan span)
        {
            Value = value;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Span = span;
        }
    }
}
