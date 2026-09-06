using System;
using Skytomo221.Sobakasu.Compiler.Binder;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrReturnValueStorage : IrStorage { public string Name { get; } public IrReturnValueStorage(string name) : base(TypeSymbol.Object) { Name = name ?? throw new ArgumentNullException(nameof(name)); } } }
