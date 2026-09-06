using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Text;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrConstantValue : IrValue { public object Value { get; } public TextSpan? Span { get; } public IrConstantValue(object value, TypeSymbol type, TextSpan? span = null) : base(type) { Value = value; Span = span; } } }
