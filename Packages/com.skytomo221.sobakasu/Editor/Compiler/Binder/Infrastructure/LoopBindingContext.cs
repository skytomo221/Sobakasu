using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class LoopBindingContext
  {
    public LoopBindingContext(LoopSymbol symbol)
    {
      Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
    }
  
    public LoopSymbol Symbol { get; }
    public bool HasReachableBreak { get; set; }
    public LoopBreakKind BreakKind { get; set; }
    public TypeSymbol BreakType { get; set; }
  }
}
