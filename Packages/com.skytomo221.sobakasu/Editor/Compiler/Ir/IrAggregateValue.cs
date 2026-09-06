using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrAggregateValue : IrValue { public IReadOnlyList<IrValue> Leaves { get; } public IrAggregateValue(TypeSymbol type, IReadOnlyList<IrValue> leaves) : base(type) { Leaves = leaves ?? throw new ArgumentNullException(nameof(leaves)); } } }
