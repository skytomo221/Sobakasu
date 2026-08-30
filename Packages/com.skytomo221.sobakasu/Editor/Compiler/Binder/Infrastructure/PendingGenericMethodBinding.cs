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
  internal sealed class PendingGenericMethodBinding
  {
    public FunctionDeclarationSyntax Syntax { get; }
    public FunctionSymbol Function { get; }
    public GenericImplTemplate Template { get; }
    public IReadOnlyDictionary<TypeSymbol, TypeSymbol> Substitutions { get; }
  
    public PendingGenericMethodBinding(FunctionDeclarationSyntax syntax, FunctionSymbol function, GenericImplTemplate template, IReadOnlyDictionary<TypeSymbol, TypeSymbol> substitutions)
    {
      Syntax = syntax;
      Function = function;
      Template = template;
      Substitutions = substitutions;
    }
  }
}
