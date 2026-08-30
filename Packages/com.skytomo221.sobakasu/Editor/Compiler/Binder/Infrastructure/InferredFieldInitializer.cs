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
  internal sealed class InferredFieldInitializer
  {
    public AggregateInitializerFieldSyntax Syntax { get; }
    public AggregateFieldSymbol TemplateField { get; }
    public BoundExpression Expression { get; }
  
    public InferredFieldInitializer(AggregateInitializerFieldSyntax syntax, AggregateFieldSymbol templateField, BoundExpression expression)
    {
      Syntax = syntax;
      TemplateField = templateField;
      Expression = expression;
    }
  }
}
