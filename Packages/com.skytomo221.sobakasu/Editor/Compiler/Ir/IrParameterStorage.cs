using System;
using Skytomo221.Sobakasu.Compiler.Binder;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrParameterStorage : IrStorage { public ParameterSymbol Parameter { get; } public IrParameterStorage(ParameterSymbol parameter) : base(parameter?.Type ?? throw new ArgumentNullException(nameof(parameter))) { Parameter = parameter; } } }
