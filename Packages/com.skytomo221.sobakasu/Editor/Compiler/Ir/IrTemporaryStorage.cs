using Skytomo221.Sobakasu.Compiler.Binder;
namespace Skytomo221.Sobakasu.Compiler.Ir { internal sealed class IrTemporaryStorage : IrStorage { public int Id { get; } public IrTemporaryStorage(int id, TypeSymbol type) : base(type) { Id = id; } } }
