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
  internal sealed class GenericImplTemplate
  {
    public TypeSymbol Definition { get; }
    public TypeSymbol OpenTarget { get; }
    public IReadOnlyList<TypeSymbol> Parameters { get; }
    public StandardLibraryModule Module { get; }
    public List<GenericMethodTemplate> Methods { get; } = new();
  
    public GenericImplTemplate(TypeSymbol definition, TypeSymbol openTarget, IReadOnlyList<TypeSymbol> parameters, StandardLibraryModule module)
    {
      Definition = definition;
      OpenTarget = openTarget;
      Parameters = parameters;
      Module = module;
    }
  }
}
