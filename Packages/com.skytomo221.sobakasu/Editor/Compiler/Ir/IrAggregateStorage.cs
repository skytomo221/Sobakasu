using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrAggregateStorage : IrStorage { public IReadOnlyList<IrStorage> Leaves { get; } public IrAggregateStorage(TypeSymbol type, IReadOnlyList<IrStorage> leaves) : base(type) { Leaves = leaves ?? throw new ArgumentNullException(nameof(leaves)); } } }
