using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{
  // ============================================================
  // Types / Symbols
  // ============================================================

  public abstract class TypeSymbol
  {
    public string Name { get; }
    protected TypeSymbol(string name) => Name = name;
    public override string ToString() => Name;

    // Builtins (start small; extend as you add lowering/codegen)
    public static readonly TypeSymbol Error = new BuiltinTypeSymbol("<error>");
    public static readonly TypeSymbol Void = new BuiltinTypeSymbol("void");
    public static readonly TypeSymbol Bool = new BuiltinTypeSymbol("bool");
    public static readonly TypeSymbol Int = new BuiltinTypeSymbol("int");
    public static readonly TypeSymbol Float = new BuiltinTypeSymbol("float");
    public static readonly TypeSymbol String = new BuiltinTypeSymbol("string");

    private sealed class BuiltinTypeSymbol : TypeSymbol
    {
      public BuiltinTypeSymbol(string name) : base(name) { }
    }

    public static bool IsNumeric(TypeSymbol t) => ReferenceEquals(t, Int) || ReferenceEquals(t, Float);
  }
}
