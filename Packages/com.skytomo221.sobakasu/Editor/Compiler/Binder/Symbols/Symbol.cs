using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal enum SymbolKind
  {
    Module,
    Namespace,
    Type,
    MethodGroup,
    Method,
    Event,
    NetworkReceive,
    Function,
    FunctionGroup,
    Parameter,
    Local,
    State,
    Constant,
    AggregateField,
    EnumVariant
  }

  internal abstract class Symbol
  {
    protected Symbol(string name)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }
    public abstract SymbolKind Kind { get; }
  }
}
